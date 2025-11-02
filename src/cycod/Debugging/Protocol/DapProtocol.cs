// DAP protocol constants used by debugging tools.
namespace Cycod.Debugging.Protocol;

public static class DapProtocol
{
    // Requests
    public const string InitializeCommand = "initialize";
    public const string LaunchCommand = "launch";
    public const string ConfigurationDoneCommand = "configurationDone";
    public const string SetBreakpointsCommand = "setBreakpoints";
    public const string ContinueCommand = "continue";
    public const string NextCommand = "next"; // step over
    public const string StepInCommand = "stepIn";
    public const string StepOutCommand = "stepOut";
    public const string StackTraceCommand = "stackTrace";
    public const string ScopesCommand = "scopes";
    public const string VariablesCommand = "variables";
    public const string SetVariableCommand = "setVariable";

    // Events
    public const string InitializedEvent = "initialized";
    public const string StoppedEvent = "stopped";
    public const string ContinuedEvent = "continued";
    public const string ThreadEvent = "thread";
    public const string OutputEvent = "output";
    public const string TerminatedEvent = "terminated";
    public const string ExitedEvent = "exited";

    // Thread event reasons
    public const string ThreadReasonStarted = "started";
    public const string ThreadReasonExited = "exited";
}
