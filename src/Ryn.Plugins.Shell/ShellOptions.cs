using Ryn.Ipc;

namespace Ryn.Plugins.Shell;

public sealed class ShellOptions
{
    /// <summary>
    /// Legacy binary allowlist permitting <em>any</em> arguments. Discouraged: allowlisting an
    /// interpreter (bash, sh, cmd, powershell), or a flexible tool (env, xargs, find, cat) here
    /// effectively disables the shell sandbox, since the frontend can then run arbitrary commands or
    /// read arbitrary files. Prefer <see cref="CommandScopes"/>, which also constrains arguments.
    /// </summary>
    public List<string> AllowedCommands { get; set; } = [];

    /// <summary>Argument-prefix denylist applied to every invocation (in addition to a built-in list).</summary>
    public List<string> DenyArgPrefixes { get; set; } = [];

    /// <summary>
    /// Per-binary scopes that validate each argument (Tauri-style argv templates). A command that has
    /// a scope may only run with arguments that match one of its scopes.
    /// </summary>
    public List<CommandScope> CommandScopes { get; set; } = [];

    /// <summary>
    /// URL schemes <c>shell.open</c> may launch. <c>null</c> means the safe default set
    /// (<c>http</c>, <c>https</c>, <c>mailto</c>). Bare paths and <c>file://</c> are always rejected
    /// unless explicitly added here.
    /// </summary>
    public List<string>? AllowedOpenSchemes { get; set; }

    /// <summary>Optional working directory for spawned processes. When unset, processes inherit the host cwd.</summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// When true (default), spawned processes inherit the host environment, minus any variables matched
    /// by <see cref="ScrubEnvironmentVariables"/>. When false, the child environment starts empty.
    /// </summary>
    public bool InheritEnvironment { get; set; } = true;

    /// <summary>
    /// Case-insensitive substrings of environment-variable names to remove from spawned processes, so an
    /// allowlisted tool cannot leak host secrets back to the frontend. Defaults to common secret markers.
    /// </summary>
    public List<string> ScrubEnvironmentVariables { get; set; } =
        ["TOKEN", "SECRET", "PASSWORD", "PASSWD", "APIKEY", "API_KEY", "PRIVATE_KEY", "AWS_", "AZURE_", "GCP_", "SESSION"];

    /// <summary>
    /// Maximum wall-clock time <c>shell.execute</c> waits for a command before killing its process tree and
    /// throwing. Prevents a hung or runaway child from blocking the IPC dispatch forever. A non-positive value
    /// disables the timeout (wait indefinitely). Default: 30 seconds.
    /// </summary>
    public TimeSpan ExecuteTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Per-stream cap (in characters) on the stdout/stderr that <c>shell.execute</c> buffers. Output is
    /// streamed and bounded to this size — the rest is drained and discarded (flagged via <c>truncated</c> on
    /// the result) so a chatty command cannot exhaust memory. A non-positive value disables the cap (unbounded,
    /// the previous behavior). Default: 1,048,576 characters (~1 MiB).
    /// </summary>
    public int MaxExecuteOutputChars { get; set; } = 1024 * 1024;
}
