using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Ryn.Core;
using Ryn.Core.Internal;
using Ryn.Ipc;

namespace Ryn.Plugins.Shell;

public sealed class PtyCommands : IDisposable
{
    private readonly IRynWebView _webView;
    private readonly ConcurrentDictionary<int, IPtySession> _sessions = new();

    public PtyCommands(IRynWebView webView)
    {
        _webView = webView;
    }

    [RynCommand("shell.pty")]
    public int Pty(string command, string argsJson)
    {
        var resolvedCommand = ShellCommands.ValidateAndResolveCommand(command);

        string[] args;
        if (string.IsNullOrEmpty(argsJson) || argsJson == "{}")
        {
            args = [resolvedCommand];
        }
        else
        {
            var parsed = JsonSerializer.Deserialize(argsJson, ShellJsonContext.Default.StringArray);
            if (parsed is null || parsed.Length == 0)
            {
                args = [resolvedCommand];
            }
            else
            {
                args = new string[parsed.Length + 1];
                args[0] = resolvedCommand;
                Array.Copy(parsed, 0, args, 1, parsed.Length);
            }
        }

        IPtySession session;

        if (OperatingSystem.IsWindows())
        {
            session = CreateWindowsSession(resolvedCommand, args);
        }
        else
        {
            session = CreateUnixSession(resolvedCommand, args);
        }

        var childPid = session.ChildPid;
        _sessions[childPid] = session;

        var pidStr = childPid.ToString(CultureInfo.InvariantCulture);
        var token = session.Cts.Token;
        _ = Task.Run(() => ReadLoop(session, pidStr, token), CancellationToken.None);

        return childPid;
    }

    private PtySessionUnix CreateUnixSession(string resolvedCommand, string[] args)
    {
        var masterFd = PtyNative.ForkWithPty(resolvedCommand, args, out var childPid);
        var pidStr = childPid.ToString(CultureInfo.InvariantCulture);

#pragma warning disable CA2000 // Batcher + CTS are owned by PtySessionUnix, disposed in exit/kill/Dispose
        var stdoutBatcher = new EventBatcher(_webView, $"shell.pty.stdout.{pidStr}");
        var cts = new CancellationTokenSource();
#pragma warning restore CA2000

        return new PtySessionUnix(childPid, masterFd, stdoutBatcher, cts);
    }

    private PtySessionWindows CreateWindowsSession(string resolvedCommand, string[] args)
    {
        var commandLine = QuoteWindowsCommandLine(resolvedCommand, args);

        var conPty = PtyNativeWindows.CreateConPty(commandLine, 80, 24);
        var pidStr = conPty.ProcessId.ToString(CultureInfo.InvariantCulture);

#pragma warning disable CA2000 // Batcher + CTS are owned by PtySessionWindows, disposed in exit/kill/Dispose
        var stdoutBatcher = new EventBatcher(_webView, $"shell.pty.stdout.{pidStr}");
        var cts = new CancellationTokenSource();
#pragma warning restore CA2000

        return new PtySessionWindows(conPty, stdoutBatcher, cts);
    }

    [RynCommand("shell.ptyWrite")]
    public bool PtyWrite(int pid, string base64Data)
    {
        if (!_sessions.TryGetValue(pid, out var session))
            return false;

        var bytes = Convert.FromBase64String(base64Data);
        var offset = 0;
        while (offset < bytes.Length)
        {
            int written;
            if (session is PtySessionWindows winSession)
                written = PtyNativeWindows.Write(
                    winSession.ConPty.InputWriteHandle, bytes, offset, bytes.Length - offset);
            else
                written = PtyNative.Write(((PtySessionUnix)session).MasterFd, bytes, offset, bytes.Length - offset);

            if (written <= 0) return false;
            offset += written;
        }
        return true;
    }

    [RynCommand("shell.ptyResize")]
    public bool PtyResize(int pid, int cols, int rows)
    {
        if (cols <= 0 || rows <= 0 || cols > 500 || rows > 500)
            return false;

        if (!_sessions.TryGetValue(pid, out var session))
            return false;

        if (session is PtySessionWindows winSession)
            return PtyNativeWindows.Resize(winSession.ConPty.PseudoConsole, (ushort)cols, (ushort)rows);

        return PtyNative.SetWindowSize(((PtySessionUnix)session).MasterFd, (ushort)rows, (ushort)cols);
    }

    [RynCommand("shell.ptyMetrics")]
    public string PtyMetrics()
    {
        var entries = new List<ProcessMetrics>();
        foreach (var kvp in _sessions)
        {
            var pid = kvp.Key;
            var s = kvp.Value;
            entries.Add(new ProcessMetrics(
                pid,
                s.StdoutBatcher.AddedCount,
                s.StdoutBatcher.FlushedCount,
                s.StdoutBatcher.DroppedCount));
        }
        return JsonSerializer.Serialize(entries, ShellJsonContext.Default.ListProcessMetrics);
    }

    [RynCommand("shell.ptyKill")]
    public bool PtyKill(int pid)
    {
        if (!_sessions.TryRemove(pid, out var session))
            return false;

        CleanupSession(session, pid);
        return true;
    }

    private void ReadLoop(IPtySession session, string pidStr, CancellationToken token)
    {
        var buf = new byte[4096];

        try
        {
            while (!token.IsCancellationRequested)
            {
                int bytesRead;
                if (session is PtySessionWindows winSession)
                    bytesRead = PtyNativeWindows.Read(winSession.ConPty.OutputReadHandle, buf, buf.Length);
                else
                    bytesRead = PtyNative.Read(((PtySessionUnix)session).MasterFd, buf, buf.Length);

                if (bytesRead <= 0)
                    break; // EOF or error — child exited

                var chunk = Convert.ToBase64String(buf, 0, bytesRead);
                var json = JsonSerializer.Serialize(chunk, ShellJsonContext.Default.String);
                session.StdoutBatcher.Add(json);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on cancellation
        }

        // If we still own this session (wasn't killed), handle exit
        if (_sessions.TryRemove(session.ChildPid, out _))
        {
            session.StdoutBatcher.FlushNow();

            int exitCode;
            if (session is PtySessionWindows winSession)
                exitCode = PtyNativeWindows.WaitForExit(winSession.ConPty.ProcessHandle);
            else
                exitCode = PtyNative.WaitForExit(session.ChildPid);

            var exitCodeStr = exitCode.ToString(CultureInfo.InvariantCulture);
            _webView.EmitEvent($"shell.pty.exit.{pidStr}", exitCodeStr);

            session.Dispose();
        }
    }

    private void CleanupSession(IPtySession session, int pid)
    {
        session.Cts.Cancel();

        if (session is PtySessionWindows winSession)
            PtyNativeWindows.Kill(winSession.ConPty.ProcessHandle);
        else
            _ = PtyNative.Kill(session.ChildPid, 9); // SIGKILL

        session.StdoutBatcher.FlushNow();

        int exitCode;
        if (session is PtySessionWindows winSess)
            exitCode = PtyNativeWindows.WaitForExit(winSess.ConPty.ProcessHandle);
        else
            exitCode = PtyNative.WaitForExit(session.ChildPid);

        var pidStr = pid.ToString(CultureInfo.InvariantCulture);
        _webView.EmitEvent($"shell.pty.exit.{pidStr}",
            exitCode.ToString(CultureInfo.InvariantCulture));

        session.Dispose();
    }

    private static string QuoteWindowsCommandLine(string command, string[] args)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(QuoteWindowsArg(command));
        for (var i = 1; i < args.Length; i++)
        {
            sb.Append(' ');
            sb.Append(QuoteWindowsArg(args[i]));
        }
        return sb.ToString();
    }

    private static string QuoteWindowsArg(string arg)
    {
        if (arg.Length > 0 && !arg.AsSpan().ContainsAny(' ', '\t', '"'))
            return arg;

        var sb = new System.Text.StringBuilder(arg.Length + 2);
        sb.Append('"');
        var backslashes = 0;
        foreach (var c in arg)
        {
            if (c == '\\')
            {
                backslashes++;
            }
            else if (c == '"')
            {
                sb.Append('\\', backslashes * 2 + 1);
                sb.Append('"');
                backslashes = 0;
            }
            else
            {
                sb.Append('\\', backslashes);
                sb.Append(c);
                backslashes = 0;
            }
        }
        sb.Append('\\', backslashes * 2);
        sb.Append('"');
        return sb.ToString();
    }

    public void Dispose()
    {
        foreach (var kvp in _sessions)
        {
            if (_sessions.TryRemove(kvp.Key, out var session))
                CleanupSession(session, kvp.Key);
        }
    }
}

/// <summary>
/// Common interface for Unix and Windows PTY sessions.
/// </summary>
internal interface IPtySession : IDisposable
{
    public int ChildPid { get; }
    public EventBatcher StdoutBatcher { get; }
    public CancellationTokenSource Cts { get; }
}

internal sealed record PtySessionUnix(int ChildPid, int MasterFd, EventBatcher StdoutBatcher, CancellationTokenSource Cts) : IPtySession
{
    public void Dispose()
    {
        Cts.Dispose();
        StdoutBatcher.Dispose();
        _ = PtyNative.Close(MasterFd);
    }
}

internal sealed class PtySessionWindows : IPtySession
{
    public PtyNativeWindows.ConPtySession ConPty { get; }
    public int ChildPid => ConPty.ProcessId;
    public EventBatcher StdoutBatcher { get; }
    public CancellationTokenSource Cts { get; }

    internal PtySessionWindows(PtyNativeWindows.ConPtySession conPty, EventBatcher stdoutBatcher, CancellationTokenSource cts)
    {
        ConPty = conPty;
        StdoutBatcher = stdoutBatcher;
        Cts = cts;
    }

    public void Dispose()
    {
        Cts.Dispose();
        StdoutBatcher.Dispose();
        ConPty.Dispose();
    }
}

/// <summary>
/// Platform-specific PTY operations via P/Invoke to libc.
/// macOS and Linux only.
/// </summary>
internal static partial class PtyNative
{
    // TIOCSWINSZ differs per platform
    private static readonly ulong TIOCSWINSZ = OperatingSystem.IsMacOS()
        ? 0x80087467UL
        : 0x5414UL; // Linux

    [StructLayout(LayoutKind.Sequential)]
    internal struct WinSize
    {
        public ushort ws_row;
        public ushort ws_col;
        public ushort ws_xpixel;
        public ushort ws_ypixel;
    }

    // forkpty lives in libutil on some systems, but on macOS (and glibc) it's in libc.
    // Using "libutil" as primary with "libc" fallback would complicate things;
    // on modern macOS/Linux forkpty is resolved from libc.
    [DllImport("libc", EntryPoint = "forkpty", SetLastError = true)]
    private static extern int forkpty(out int master, IntPtr name, IntPtr termp, IntPtr winp);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    private static extern nint libc_read(int fd, byte[] buf, nint count);

    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    private static extern nint libc_write(int fd, byte[] buf, nint count);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int libc_close(int fd);

    [DllImport("libc", EntryPoint = "kill", SetLastError = true)]
    private static extern int libc_kill(int pid, int sig);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int libc_ioctl(int fd, ulong request, ref WinSize ws);

    [DllImport("libc", EntryPoint = "waitpid", SetLastError = true)]
    private static extern int libc_waitpid(int pid, out int status, int options);

    [DllImport("libc", EntryPoint = "execvp", SetLastError = true,
        BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int libc_execvp(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string file,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string?[] argv);

    [DllImport("libc", EntryPoint = "_exit")]
    private static extern void libc_exit(int status);

    /// <summary>
    /// Forks a child process with a PTY. Returns the master fd in the parent.
    /// Throws on failure.
    /// </summary>
    internal static int ForkWithPty(string command, string[] args, out int childPid)
    {
        // Build null-terminated argv for the native shim
        var argv = new string?[args.Length + 1];
        Array.Copy(args, argv, args.Length);
        argv[args.Length] = null;

        // Try the native shim first (no managed work after fork)
        try
        {
            var result = ryn_pty_spawn(command, argv, out var masterFd, out childPid);
            if (result == 0)
                return masterFd;
        }
        catch (EntryPointNotFoundException) { }
        catch (DllNotFoundException) { }

        // Fallback to managed forkpty if native shim is not linked
        int fallbackMasterFd;
        var pid = forkpty(out fallbackMasterFd, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);

        if (pid < 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            throw new InvalidOperationException($"forkpty failed with errno {errno}");
        }

        if (pid == 0)
        {
            _ = libc_execvp(command, argv);
            libc_exit(127);
        }

        childPid = pid;
        return fallbackMasterFd;
    }

    [DllImport("ryn-pty", EntryPoint = "ryn_pty_spawn", SetLastError = true,
        BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int ryn_pty_spawn(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string command,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPUTF8Str)] string?[] argv,
        out int masterFd,
        out int childPid);

    internal static int Read(int fd, byte[] buf, int count)
    {
        return (int)libc_read(fd, buf, count);
    }

    internal static unsafe int Write(int fd, byte[] buf, int offset, int count)
    {
        fixed (byte* ptr = buf)
        {
            return (int)libc_write_ptr(fd, ptr + offset, count);
        }
    }

    [DllImport("libc", EntryPoint = "write", SetLastError = true)]
    private static extern unsafe nint libc_write_ptr(int fd, byte* buf, nint count);

    internal static int Close(int fd)
    {
        return libc_close(fd);
    }

    internal static int Kill(int pid, int signal)
    {
        return libc_kill(pid, signal);
    }

    internal static bool SetWindowSize(int fd, ushort rows, ushort cols)
    {
        var ws = new WinSize
        {
            ws_row = rows,
            ws_col = cols,
            ws_xpixel = 0,
            ws_ypixel = 0,
        };
        return libc_ioctl(fd, TIOCSWINSZ, ref ws) == 0;
    }

    /// <summary>
    /// Waits for a child process to exit and returns the exit code.
    /// Returns -1 if the wait fails.
    /// </summary>
    internal static int WaitForExit(int pid)
    {
        var result = libc_waitpid(pid, out var status, 0);
        if (result < 0)
            return -1;

        // WIFEXITED: (status & 0x7f) == 0 means normal exit
        if ((status & 0x7F) == 0)
            return (status >> 8) & 0xFF; // WEXITSTATUS

        // Killed by signal — return negative signal number
        return -(status & 0x7F);
    }
}
