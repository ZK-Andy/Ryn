using System.Reflection;
using System.Runtime.InteropServices;

namespace Ryn.Interop;

public static class NativeLibraryResolver
{
    private static readonly HashSet<string> _registeredAssemblies = new(StringComparer.Ordinal);
    private static readonly HashSet<string> _knownLibraries = new(StringComparer.Ordinal)
    {
        "saucer-bindings",
        "ryn-pty",
    };

    public static void Register()
    {
        RegisterForAssembly(typeof(NativeLibraryResolver).Assembly);
    }

    public static void RegisterForAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        var name = assembly.FullName ?? assembly.GetName().Name ?? "";
        lock (_registeredAssemblies)
        {
            if (!_registeredAssemblies.Add(name))
                return;
        }
        NativeLibrary.SetDllImportResolver(assembly, ResolveLibrary);
    }

    private static nint ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!_knownLibraries.Contains(libraryName))
            return nint.Zero;

        var rid = RuntimeInformation.RuntimeIdentifier;
        var extension = GetPlatformExtension();
        var prefix = GetPlatformPrefix();
        var baseDir = AppContext.BaseDirectory;

        string[] searchPaths =
        [
            Path.Combine(baseDir, "runtimes", rid, "native", $"{prefix}{libraryName}{extension}"),
            Path.Combine(baseDir, $"{prefix}{libraryName}{extension}"),
            $"{prefix}{libraryName}{extension}",
        ];

        foreach (var path in searchPaths)
        {
            if (NativeLibrary.TryLoad(path, out var handle))
                return handle;
        }

        return nint.Zero;
    }

    private static string GetPlatformExtension()
    {
        if (OperatingSystem.IsWindows())
        {
            return ".dll";
        }

        if (OperatingSystem.IsMacOS())
        {
            return ".dylib";
        }

        return ".so";
    }

    private static string GetPlatformPrefix()
    {
        if (OperatingSystem.IsWindows())
        {
            return string.Empty;
        }

        return "lib";
    }
}
