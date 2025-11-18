using System;
using System.IO;
using System.Text.Json;
using CycoTui.Sample.Auth;

namespace CycoTui.Sample.Auth;

internal static class AuthFileStore
{
    private const string AuthFileName = "auth.json";

    private static string GetAuthDirectory()
    {
        // Reuse user scope directory (~/.cycod) if exists else create
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var dir = Path.Combine(home, ".cycod");
        Directory.CreateDirectory(dir);
        return dir;
    }

    public static string GetAuthFilePath()
    {
        return Path.Combine(GetAuthDirectory(), AuthFileName);
    }

    public static AzureAuthRecord? Load()
    {
        try
        {
            var path = GetAuthFilePath();
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AzureAuthRecord>(json);
        }
        catch (Exception ex)
        {
            ConsoleHelpers.WriteWarning($"Failed to load auth file: {ex.Message}");
            return null;
        }
    }

    public static void Save(AzureAuthRecord record)
    {
        try
        {
            var path = GetAuthFilePath();
            var json = JsonSerializer.Serialize(record, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(path, json);
            TryRestrictPermissions(path);
        }
        catch (Exception ex)
        {
            ConsoleHelpers.WriteWarning($"Failed to save auth file: {ex.Message}");
        }
    }

    public static void Delete()
    {
        try
        {
            var path = GetAuthFilePath();
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception ex)
        {
            ConsoleHelpers.WriteWarning($"Failed to delete auth file: {ex.Message}");
        }
    }

    private static void TryRestrictPermissions(string path)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                // best-effort chmod 600
                System.Diagnostics.Process.Start("chmod", $"600 \"{path}\"");
            }
        }
        catch { }
    }
}
