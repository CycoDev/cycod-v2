using System.Runtime.InteropServices;

// Locates the netcoredbg executable. netcoredbg is required; no vsdbg fallback.
public static class DebugAdapterLocator
{
    private static string? _cachedPath;

    public static string FindNetcoredbg()
    {
        if (_cachedPath != null) return _cachedPath;

        var exeName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "netcoredbg.exe" : "netcoredbg";
        var searchPaths = new List<string>
        {
            // User local installation paths
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "netcoredbg", exeName),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "bin", exeName),

            // System-wide
            Path.Combine("/usr", "local", "netcoredbg", exeName),
            Path.Combine("/usr", "local", "bin", exeName),
            Path.Combine("/usr", "bin", exeName),
            Path.Combine("/bin", exeName),

            // macOS Homebrew (if installed)
            Path.Combine("/opt", "homebrew", "bin", exeName),
        };

        foreach (var path in searchPaths)
        {
            if (File.Exists(path)) return Cache(path);
        }

        // Check PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pathEnv))
        {
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                try
                {
                    var candidate = Path.Combine(dir, exeName);
                    if (File.Exists(candidate)) return Cache(candidate);
                }
                catch { }
            }
        }

        throw new FileNotFoundException(GetInstallInstructions(exeName));
    }

    private static string Cache(string path)
    {
        _cachedPath = Path.GetFullPath(path);
        return _cachedPath;
    }

    private static string GetInstallInstructions(string exeName)
    {
        return $"netcoredbg executable '{exeName}' not found.\n\n" +
            "Install netcoredbg:\n" +
            "  - Download from https://github.com/Samsung/netcoredbg/releases\n" +
            "  - Extract and place in ~/.local/bin/ or /usr/local/bin/ (ensure executable bit)\n\n" +
            "Example (Linux):\n" +
            "  wget <tarball>; tar -xf ...; mv netcoredbg ~/.local/bin/\n\n" +
            "After installation, re-run StartDebugSession.";
    }
}
