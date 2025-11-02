// Internal model used to represent queued debugger events for tool consumption.
public class DebugEventModel
{
    public string? Type { get; set; }            // stopped | continued | thread | output | terminated | exited
    public string? Reason { get; set; }          // breakpoint | exception | step | entry | etc.
    public string? Message { get; set; }         // additional message (e.g., exception text)
    public string? File { get; set; }
    public int? Line { get; set; }
    public int? ThreadId { get; set; }
    public int? ExitCode { get; set; }
    public string? Category { get; set; }        // stdout | stderr | console
    public string? Output { get; set; }          // output line when Category present
    public SourceSnippetModel? Snippet { get; set; } // optional inline snippet (exception events)
}
