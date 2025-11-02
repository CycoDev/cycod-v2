using System.Text.Json.Serialization;

namespace Cycod.Debugging.Protocol;

// Core protocol message base types
public abstract class ProtocolMessage
{
    [JsonPropertyName("seq")] public int Seq { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
}

public class Request : ProtocolMessage
{
    [JsonPropertyName("command")] public string? Command { get; set; }
    [JsonPropertyName("arguments")] public object? Arguments { get; set; }
    public Request() { Type = "request"; }
}

public class Response : ProtocolMessage
{
    [JsonPropertyName("request_seq")] public int RequestSeq { get; set; }
    [JsonPropertyName("success")] public bool Success { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
    [JsonPropertyName("command")] public string? Command { get; set; }
    [JsonPropertyName("body")] public object? Body { get; set; }
    public Response() { Type = "response"; }
}

public class Event : ProtocolMessage
{
    [JsonPropertyName("event")] public string? EventType { get; set; }
    [JsonPropertyName("body")] public object? Body { get; set; }
    public Event() { Type = "event"; }
}

// Request argument models (subset needed for initial implementation)
public class InitializeRequestArguments
{
    [JsonPropertyName("clientID")] public string? ClientID { get; set; } = "cycod";
    [JsonPropertyName("adapterID")] public string? AdapterID { get; set; } = "netcoredbg";
    [JsonPropertyName("linesStartAt1")] public bool LinesStartAt1 { get; set; } = true;
    [JsonPropertyName("columnsStartAt1")] public bool ColumnsStartAt1 { get; set; } = true;
    [JsonPropertyName("pathFormat")] public string? PathFormat { get; set; } = "path";
}

public class LaunchRequestArguments
{
    [JsonPropertyName("program")] public string? Program { get; set; }
    [JsonPropertyName("cwd")] public string? Cwd { get; set; }
    [JsonPropertyName("stopAtEntry")] public bool StopAtEntry { get; set; }
}

public class Source
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("path")] public string? Path { get; set; }
}

public class SourceBreakpoint
{
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("condition")] public string? Condition { get; set; }
}

public class SetBreakpointsArguments
{
    [JsonPropertyName("source")] public Source? Source { get; set; }
    [JsonPropertyName("breakpoints")] public SourceBreakpoint[]? Breakpoints { get; set; }
}

public class ContinueArguments
{
    [JsonPropertyName("threadId")] public int ThreadId { get; set; }
}

public class NextArguments
{
    [JsonPropertyName("threadId")] public int ThreadId { get; set; }
}

public class StepInArguments
{
    [JsonPropertyName("threadId")] public int ThreadId { get; set; }
}

public class StepOutArguments
{
    [JsonPropertyName("threadId")] public int ThreadId { get; set; }
}

public class StackTraceArguments
{
    [JsonPropertyName("threadId")] public int ThreadId { get; set; }
    [JsonPropertyName("levels")] public int? Levels { get; set; }
}

public class ScopesArguments
{
    [JsonPropertyName("frameId")] public int FrameId { get; set; }
}

public class VariablesArguments
{
    [JsonPropertyName("variablesReference")] public int VariablesReference { get; set; }
}

public class SetVariableArguments
{
    [JsonPropertyName("variablesReference")] public int VariablesReference { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }
}

// Response bodies / event bodies (subset)
public class StackTraceResponseBody
{
    [JsonPropertyName("stackFrames")] public StackFrame[] StackFrames { get; set; } = System.Array.Empty<StackFrame>();
    [JsonPropertyName("totalFrames")] public int TotalFrames { get; set; }
}

public class StackFrame
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("source")] public Source? Source { get; set; }
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("column")] public int Column { get; set; }
}

public class ScopesResponseBody
{
    [JsonPropertyName("scopes")] public Scope[] Scopes { get; set; } = System.Array.Empty<Scope>();
}

public class Scope
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("variablesReference")] public int VariablesReference { get; set; }
    [JsonPropertyName("expensive")] public bool Expensive { get; set; }
}

public class VariablesResponseBody
{
    [JsonPropertyName("variables")] public Variable[] Variables { get; set; } = System.Array.Empty<Variable>();
}

public class Variable
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }
    [JsonPropertyName("type")] public string? Type { get; set; }
    [JsonPropertyName("variablesReference")] public int VariablesReference { get; set; }
}

public class SetBreakpointsResponseBody
{
    [JsonPropertyName("breakpoints")] public BreakpointResult[] Breakpoints { get; set; } = System.Array.Empty<BreakpointResult>();
}

public class BreakpointResult
{
    [JsonPropertyName("verified")] public bool Verified { get; set; }
    [JsonPropertyName("line")] public int Line { get; set; }
    [JsonPropertyName("message")] public string? Message { get; set; }
}

public class StoppedEventBody
{
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("threadId")] public int ThreadId { get; set; }
    [JsonPropertyName("text")] public string? Text { get; set; }
}

public class ThreadEventBody
{
    [JsonPropertyName("reason")] public string? Reason { get; set; }
    [JsonPropertyName("threadId")] public int ThreadId { get; set; }
}

public class OutputEventBody
{
    [JsonPropertyName("category")] public string? Category { get; set; }
    [JsonPropertyName("output")] public string? Output { get; set; }
}

public class ExitedEventBody
{
    [JsonPropertyName("exitCode")] public int ExitCode { get; set; }
}
