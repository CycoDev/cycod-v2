using System;
using System.CommandLine;
using System.Threading;

namespace CycoTui.Sample;

internal static class Program
{
    private static readonly CancellationTokenSource _cts = new();

    static int Main(string[] args)
    {
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; _cts.Cancel(); };

        // Create root command
        var rootCommand = new RootCommand("cycocli - Interactive TUI and command-line tool");

        // Add 'chat' command for interactive mode
        var chatCommand = new Command("chat", "Start interactive chat mode");
        chatCommand.SetHandler(() =>
        {
            InteractiveMode.Run(_cts.Token);
        });
        rootCommand.AddCommand(chatCommand);

        // Add other commands (example: test command for non-interactive mode)
        var testCommand = new Command("hello", "Test command (non-interactive)");
        testCommand.SetHandler(() =>
        {
            Environment.Exit(CommandMode.Run(args));
        });
        rootCommand.AddCommand(testCommand);

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
