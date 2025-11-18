using System;
// NOTE: Added Azure AD B2C authentication commands
using System.CommandLine;
using System.Threading;

namespace CycoTui.Sample;
using CycoTui.Sample.Auth;
using System.Linq;
using System.Collections.Generic;

internal static class Program
{
    private static readonly CancellationTokenSource _cts = new();


    static int Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; _cts.Cancel(); };

        // Create root command
        var rootCommand = new RootCommand("cycocli - Interactive TUI and command-line tool");

        // Add 'chat' command for interactive mode
        var chatCommand = new System.CommandLine.Command("chat", "Start interactive chat mode");
        chatCommand.SetHandler(() =>
        {
            InteractiveMode.Run(_cts.Token);
        });
        rootCommand.AddCommand(chatCommand);

        // Add other commands (example: test command for non-interactive mode)
        var testCommand = new System.CommandLine.Command("hello", "Test command (non-interactive)");
        testCommand.SetHandler(() =>
        {
            Environment.Exit(CommandMode.Run(args));
        });
        rootCommand.AddCommand(testCommand);

        // Azure External ID / AAD login command (no parameters)
        var loginCommand = new System.CommandLine.Command("azure-login", "Authenticate with Microsoft Entra (device code flow)");
        loginCommand.SetHandler(() =>
        {
            AzureLoginHandler.Handle();
        });
        rootCommand.AddCommand(loginCommand);

        // Keep legacy command for backwards compatibility
        var legacyLoginCommand = new System.CommandLine.Command("azureb2c-login", "[DEPRECATED] Use 'azure-login' instead");
        legacyLoginCommand.SetHandler(() =>
        {
            ConsoleHelpers.WriteLine("This command is deprecated. Use 'azure-login' instead.", ConsoleColor.Yellow, overrideQuiet: true);
        });
        rootCommand.AddCommand(legacyLoginCommand);

        // Azure logout command
        var logoutCommand = new System.CommandLine.Command("azure-logout", "Remove cached Azure authentication");
        logoutCommand.SetHandler(() =>
        {
            CycoTui.Sample.Auth.AuthFileStore.Delete();
            ConsoleHelpers.WriteLine("Logged out (auth file deleted).", ConsoleColor.Yellow, overrideQuiet: true);
        });
        rootCommand.AddCommand(logoutCommand);

        // Legacy logout command
        var legacyLogoutCommand = new System.CommandLine.Command("azureb2c-logout", "[DEPRECATED] Use 'azure-logout' instead");
        legacyLogoutCommand.SetHandler(() =>
        {
            CycoTui.Sample.Auth.AuthFileStore.Delete();
            ConsoleHelpers.WriteLine("Logged out (auth file deleted).", ConsoleColor.Yellow, overrideQuiet: true);
        });
        rootCommand.AddCommand(legacyLogoutCommand);

        // Azure status command
        var statusCommand = new System.CommandLine.Command("azure-status", "Show current Azure authentication status");
        statusCommand.SetHandler(() =>
        {
            var record = CycoTui.Sample.Auth.AuthFileStore.Load();
            if (record == null)
            {
                ConsoleHelpers.WriteLine("No auth record found.", ConsoleColor.Yellow, overrideQuiet: true);
                return;
            }
            var expired = record.ExpiresAt <= DateTimeOffset.UtcNow;
            var mode = record.UseLegacyB2c ? "Azure AD B2C (Legacy)" : "Microsoft Entra External ID";
            ConsoleHelpers.WriteLine($"Provider: {mode}\nTenant: {record.Tenant}\nUser: {record.UserName}\nEmail: {record.Email}\nObjectId: {record.ObjectId}\nExpires: {record.ExpiresAt:O} ({(expired ? "expired" : "valid")})", overrideQuiet: true);
        });
        rootCommand.AddCommand(statusCommand);

        // Legacy status command
        var legacyStatusCommand = new System.CommandLine.Command("azureb2c-status", "[DEPRECATED] Use 'azure-status' instead");
        legacyStatusCommand.SetHandler(() =>
        {
            var record = CycoTui.Sample.Auth.AuthFileStore.Load();
            if (record == null)
            {
                ConsoleHelpers.WriteLine("No auth record found.", ConsoleColor.Yellow, overrideQuiet: true);
                return;
            }
            var expired = record.ExpiresAt <= DateTimeOffset.UtcNow;
            var mode = record.UseLegacyB2c ? "Azure AD B2C (Legacy)" : "Microsoft Entra External ID";
            ConsoleHelpers.WriteLine($"Provider: {mode}\nTenant: {record.Tenant}\nUser: {record.UserName}\nEmail: {record.Email}\nObjectId: {record.ObjectId}\nExpires: {record.ExpiresAt:O} ({(expired ? "expired" : "valid")})", overrideQuiet: true);
        });
        rootCommand.AddCommand(legacyStatusCommand);

        // Smart detection: if no arguments, start interactive mode
        if (args.Length == 0)
        {
            InteractiveMode.Run(_cts.Token);
            return 0;
        }

        // Otherwise, use System.CommandLine to parse and execute commands
        return rootCommand.Invoke(args);
    }

}
