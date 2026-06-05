using System.Reflection;

namespace Ryn.Cli;

internal static class RynCliVersion
{
    /// <summary>
    /// The CLI's own version, taken from MinVer's <see cref="AssemblyInformationalVersionAttribute"/> with the
    /// trailing <c>+&lt;commit&gt;</c> build metadata stripped. Under MinVer the plain <c>AssemblyVersion</c> is
    /// <c>{Major}.0.0.0</c>, so this is the only place the real SemVer lives.
    ///
    /// The CLI and the <c>Ryn</c> NuGet packages are built and released in lock-step from the same git tags,
    /// so this value doubles as the package version a scaffolded app should reference — no hand-maintained
    /// version literal to keep in sync.
    /// </summary>
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var informational = typeof(RynCliVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
            return typeof(RynCliVersion).Assembly.GetName().Version?.ToString() ?? "unknown";

        var plus = informational.IndexOf('+', StringComparison.Ordinal);
        return plus >= 0 ? informational[..plus] : informational;
    }
}
