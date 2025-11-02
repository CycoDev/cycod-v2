// Represents a source code snippet around a particular line for a stack frame.
public class SourceSnippetModel
{
    public string? File { get; set; }
    public string? FileName { get; set; }
    public int CenterLine { get; set; }
    public int StartLine { get; set; }
    public int EndLine { get; set; }
    public int Radius { get; set; }
    public bool RadiusClamped { get; set; }
    public int HighlightIndex { get; set; }
    public List<SourceSnippetLine> Lines { get; set; } = new();
}

public class SourceSnippetLine
{
    public int Line { get; set; }
    public string? Text { get; set; }
}
