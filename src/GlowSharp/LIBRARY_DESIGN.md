# GlowSharp.Streaming - Library Design

## Overview

A standalone .NET library for progressive markdown rendering, perfect for displaying LLM responses with real-time formatting.

## Project Structure

```
GlowSharp.Streaming/
├── GlowSharp.Streaming.csproj    # Class library project
├── StreamingMarkdownRenderer.cs   # Core renderer (refactored)
├── SyntaxHighlighter.cs           # Code highlighting
├── MarkdownStyle.cs               # Style configuration
└── README.md                      # Library documentation

GlowSharp.Streaming.Samples/
├── GlowSharp.Streaming.Samples.csproj
├── ConsoleApp.cs                  # Console rendering example
├── LlmIntegration.cs              # LLM streaming example
└── AspNetCore.cs                  # ASP.NET Core example
```

## Public API Design

### Option 1: Callback-Based API (Simplest)

```csharp
using GlowSharp.Streaming;

// Create renderer with callback
var renderer = new StreamingMarkdownRenderer(
    onChunkRendered: chunk => Console.Write(chunk),
    style: MarkdownStyle.Dark
);

// Feed markdown chunks as they arrive
await foreach (var chunk in llmResponseStream)
{
    renderer.AppendChunk(chunk);
}

// Flush remaining buffered content
renderer.Flush();
```

### Option 2: Event-Based API

```csharp
using GlowSharp.Streaming;

var renderer = new StreamingMarkdownRenderer(MarkdownStyle.Dark);

// Subscribe to events
renderer.ChunkRendered += (sender, e) =>
{
    Console.Write(e.RenderedText);
};

// Feed chunks
await foreach (var chunk in llmResponseStream)
{
    renderer.AppendChunk(chunk);
}

renderer.Flush();
```

### Option 3: IAsyncEnumerable API (Modern Async)

```csharp
using GlowSharp.Streaming;

var renderer = new StreamingMarkdownRenderer(MarkdownStyle.Dark);

// Get rendered chunks as async stream
await foreach (var renderedChunk in renderer.RenderAsync(llmResponseStream))
{
    Console.Write(renderedChunk);
}
```

### Option 4: Fluent Builder API

```csharp
using GlowSharp.Streaming;

var renderer = StreamingMarkdownRenderer.Create()
    .WithStyle(MarkdownStyle.Dark)
    .WithSyntaxHighlighting(enabled: true)
    .OnChunkRendered(chunk => Console.Write(chunk))
    .Build();

await foreach (var chunk in llmResponseStream)
{
    renderer.AppendChunk(chunk);
}

renderer.Flush();
```

## Recommended API (Combination Approach)

Support multiple usage patterns for maximum flexibility:

```csharp
namespace GlowSharp.Streaming;

/// <summary>
/// Progressive markdown renderer for streaming content (e.g., LLM responses).
/// Renders simple text immediately, buffers complex structures until complete.
/// </summary>
public class StreamingMarkdownRenderer
{
    // === Constructor Overloads ===

    /// <summary>
    /// Create renderer with default settings.
    /// </summary>
    public StreamingMarkdownRenderer()

    /// <summary>
    /// Create renderer with custom style.
    /// </summary>
    public StreamingMarkdownRenderer(MarkdownStyle style)

    /// <summary>
    /// Create renderer with callback for each rendered chunk.
    /// </summary>
    public StreamingMarkdownRenderer(
        Action<string> onChunkRendered,
        MarkdownStyle? style = null)

    // === Primary API ===

    /// <summary>
    /// Process a chunk of markdown text.
    /// If using callback, rendered text is sent via callback.
    /// Otherwise, output goes to internal buffer (call GetRendered()).
    /// </summary>
    public void AppendChunk(string chunk)

    /// <summary>
    /// Flush any remaining buffered content.
    /// Call when stream ends.
    /// </summary>
    public void Flush()

    /// <summary>
    /// Get all rendered output so far (if not using callback).
    /// </summary>
    public string GetRendered()

    /// <summary>
    /// Reset the renderer to initial state.
    /// </summary>
    public void Reset()

    // === Event API ===

    /// <summary>
    /// Event fired when a chunk is rendered.
    /// </summary>
    public event EventHandler<ChunkRenderedEventArgs>? ChunkRendered;

    // === Async Enumerable API ===

    /// <summary>
    /// Render markdown from async stream, yielding rendered chunks.
    /// </summary>
    public async IAsyncEnumerable<string> RenderAsync(
        IAsyncEnumerable<string> markdownStream,
        [EnumeratorCancellation] CancellationToken ct = default)

    // === Configuration ===

    /// <summary>
    /// Current style settings.
    /// </summary>
    public MarkdownStyle Style { get; set; }

    /// <summary>
    /// Enable/disable syntax highlighting for code blocks.
    /// </summary>
    public bool EnableSyntaxHighlighting { get; set; } = true;

    /// <summary>
    /// Get rendering statistics.
    /// </summary>
    public RenderingStats GetStats()
}

/// <summary>
/// Event args for ChunkRendered event.
/// </summary>
public class ChunkRenderedEventArgs : EventArgs
{
    public string RenderedText { get; }
    public MarkdownElementType ElementType { get; }
    public int ChunkNumber { get; }
}

/// <summary>
/// Type of markdown element that was rendered.
/// </summary>
public enum MarkdownElementType
{
    Text,
    Heading,
    CodeBlock,
    Table,
    List,
    Blockquote,
    HorizontalRule
}

/// <summary>
/// Rendering statistics.
/// </summary>
public class RenderingStats
{
    public int TotalChunksProcessed { get; }
    public int CodeBlocksRendered { get; }
    public int TablesRendered { get; }
    public TimeSpan TotalProcessingTime { get; }
    public long BytesProcessed { get; }
}

/// <summary>
/// Style configuration for markdown rendering.
/// </summary>
public class MarkdownStyle
{
    // Predefined styles
    public static MarkdownStyle Dark { get; }
    public static MarkdownStyle Light { get; }
    public static MarkdownStyle Minimal { get; }

    // Customization
    public HeadingStyle H1 { get; set; }
    public HeadingStyle H2 { get; set; }
    public TextStyle Code { get; set; }
    public CodeBlockStyle CodeBlock { get; set; }
    // ... etc
}

/// <summary>
/// Fluent builder for StreamingMarkdownRenderer.
/// </summary>
public class StreamingMarkdownRendererBuilder
{
    public static StreamingMarkdownRendererBuilder Create();

    public StreamingMarkdownRendererBuilder WithStyle(MarkdownStyle style);
    public StreamingMarkdownRendererBuilder WithDarkStyle();
    public StreamingMarkdownRendererBuilder WithLightStyle();
    public StreamingMarkdownRendererBuilder WithSyntaxHighlighting(bool enabled);
    public StreamingMarkdownRendererBuilder OnChunkRendered(Action<string> callback);
    public StreamingMarkdownRendererBuilder OnEvent(EventHandler<ChunkRenderedEventArgs> handler);

    public StreamingMarkdownRenderer Build();
}
```

## Usage Examples

### Example 1: Console App with OpenAI

```csharp
using GlowSharp.Streaming;
using OpenAI;

var client = new OpenAIClient("api-key");
var renderer = new StreamingMarkdownRenderer(
    onChunkRendered: chunk => Console.Write(chunk),
    style: MarkdownStyle.Dark
);

var chatRequest = new ChatRequest
{
    Messages = new[] { new ChatMessage("user", "Explain async/await in C#") },
    Stream = true
};

await foreach (var response in client.StreamChatCompletionsAsync(chatRequest))
{
    var content = response.Choices[0].Delta.Content;
    if (!string.IsNullOrEmpty(content))
    {
        renderer.AppendChunk(content);
    }
}

renderer.Flush();
```

### Example 2: Blazor Real-Time Display

```csharp
@page "/llm-chat"
@using GlowSharp.Streaming
@inject ILlmService LlmService

<div class="markdown-output">
    @((MarkupString)renderedMarkdown)
</div>

@code {
    private string renderedMarkdown = "";
    private StreamingMarkdownRenderer? renderer;

    protected override async Task OnInitializedAsync()
    {
        // Create renderer that updates UI on each chunk
        renderer = new StreamingMarkdownRenderer(
            onChunkRendered: chunk =>
            {
                renderedMarkdown += chunk;
                InvokeAsync(StateHasChanged);
            },
            style: MarkdownStyle.Light
        );

        // Stream from LLM
        await foreach (var chunk in LlmService.StreamResponseAsync("Hello"))
        {
            renderer.AppendChunk(chunk);
        }

        renderer.Flush();
    }
}
```

### Example 3: ASP.NET Core SignalR

```csharp
using GlowSharp.Streaming;
using Microsoft.AspNetCore.SignalR;

public class LlmHub : Hub
{
    private readonly ILlmService _llmService;

    public LlmHub(ILlmService llmService)
    {
        _llmService = llmService;
    }

    public async Task StreamMarkdown(string prompt)
    {
        var renderer = new StreamingMarkdownRenderer(
            onChunkRendered: async chunk =>
            {
                // Send rendered chunk to connected client
                await Clients.Caller.SendAsync("ReceiveMarkdown", chunk);
            },
            style: MarkdownStyle.Dark
        );

        await foreach (var chunk in _llmService.StreamAsync(prompt))
        {
            renderer.AppendChunk(chunk);
        }

        renderer.Flush();
    }
}
```

### Example 4: Event-Based with Rich Telemetry

```csharp
using GlowSharp.Streaming;

var renderer = new StreamingMarkdownRenderer(MarkdownStyle.Dark);

renderer.ChunkRendered += (sender, e) =>
{
    Console.Write(e.RenderedText);

    // Log telemetry
    if (e.ElementType == MarkdownElementType.CodeBlock)
    {
        logger.LogInformation("Code block rendered at chunk {ChunkNumber}", e.ChunkNumber);
    }
};

await foreach (var chunk in llmStream)
{
    renderer.AppendChunk(chunk);
}

renderer.Flush();

var stats = renderer.GetStats();
Console.WriteLine($"Processed {stats.TotalChunksProcessed} chunks in {stats.TotalProcessingTime}");
```

### Example 5: Async Enumerable with LINQ

```csharp
using GlowSharp.Streaming;

var renderer = new StreamingMarkdownRenderer(MarkdownStyle.Dark);

// Use async LINQ to process rendered chunks
var renderedChunks = renderer.RenderAsync(llmResponseStream)
    .Where(chunk => !string.IsNullOrWhiteSpace(chunk))
    .Select(chunk => chunk.TrimEnd());

await foreach (var chunk in renderedChunks)
{
    await File.AppendAllTextAsync("output.txt", chunk);
    Console.Write(chunk);
}
```

### Example 6: Buffer Mode (No Callback)

```csharp
using GlowSharp.Streaming;

// Create renderer without callback - buffers output internally
var renderer = new StreamingMarkdownRenderer(MarkdownStyle.Dark);

// Process all chunks
foreach (var chunk in markdownChunks)
{
    renderer.AppendChunk(chunk);
}

renderer.Flush();

// Get complete rendered output
string result = renderer.GetRendered();
Console.WriteLine(result);
```

## NuGet Package Configuration

### GlowSharp.Streaming.csproj

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>

    <!-- Package metadata -->
    <PackageId>GlowSharp.Streaming</PackageId>
    <Version>1.0.0</Version>
    <Authors>Your Name</Authors>
    <Description>Progressive markdown renderer for streaming content (LLM responses, real-time editing)</Description>
    <PackageTags>markdown;streaming;llm;rendering;ansi;terminal;progressive</PackageTags>
    <PackageProjectUrl>https://github.com/yourname/glowsharp</PackageProjectUrl>
    <PackageLicenseExpression>MIT</PackageLicenseExpression>
    <RepositoryUrl>https://github.com/yourname/glowsharp</RepositoryUrl>

    <!-- Generate XML documentation -->
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>

  <ItemGroup>
    <!-- Zero external dependencies! -->
  </ItemGroup>
</Project>
```

**Key Design Decision**: **Zero external dependencies**
- No Markdig (we don't use AST parsing in streaming mode)
- No Spectre.Console (we generate ANSI codes directly)
- Pure .NET 8.0+ with no external packages

## API Design Principles

### 1. **Progressive by Default**
Rendering happens immediately when possible, buffering only when necessary.

### 2. **Flexible Output**
Support callback, events, async enumerable, and internal buffering.

### 3. **Zero Dependencies**
Self-contained library with no external packages.

### 4. **Thread-Safe**
Renderer can be used from multiple threads with proper synchronization.

### 5. **Cancellation Support**
All async operations support CancellationToken.

### 6. **Telemetry-Friendly**
Expose events and stats for monitoring and debugging.

## Advanced Features

### Custom Output Destinations

```csharp
// Write to file
var fileRenderer = new StreamingMarkdownRenderer(
    onChunkRendered: chunk => File.AppendAllText("output.txt", chunk)
);

// Send over network
var networkRenderer = new StreamingMarkdownRenderer(
    onChunkRendered: async chunk => await websocket.SendAsync(Encoding.UTF8.GetBytes(chunk))
);

// Multiple outputs (tee)
var teeRenderer = new StreamingMarkdownRenderer(
    onChunkRendered: chunk =>
    {
        Console.Write(chunk);
        File.AppendAllText("log.txt", chunk);
        telemetry.Track(chunk);
    }
);
```

### Custom Syntax Highlighters

```csharp
public interface ISyntaxHighlighter
{
    string Highlight(string code, string language);
}

var renderer = new StreamingMarkdownRenderer(MarkdownStyle.Dark);
renderer.SetSyntaxHighlighter(new CustomHighlighter());
```

### Middleware Pipeline

```csharp
var renderer = new StreamingMarkdownRenderer(MarkdownStyle.Dark)
    .Use(new ProfanityFilterMiddleware())
    .Use(new LinkSanitizerMiddleware())
    .Use(new EmojiConverterMiddleware());
```

## Testing Strategy

```csharp
[Fact]
public void AppendChunk_SimpleText_RendersImmediately()
{
    var output = new StringBuilder();
    var renderer = new StreamingMarkdownRenderer(
        onChunkRendered: chunk => output.Append(chunk)
    );

    renderer.AppendChunk("Hello ");
    renderer.AppendChunk("World");

    Assert.Equal("Hello World", output.ToString());
}

[Fact]
public void AppendChunk_CodeBlock_BuffersUntilComplete()
{
    var chunks = new List<string>();
    var renderer = new StreamingMarkdownRenderer(
        onChunkRendered: chunks.Add
    );

    renderer.AppendChunk("```py");
    Assert.Empty(chunks); // Nothing rendered yet

    renderer.AppendChunk("thon\ndef");
    Assert.Empty(chunks); // Still buffering

    renderer.AppendChunk(" foo():\n    pass\n```");
    Assert.Single(chunks); // Code block rendered
    Assert.Contains("def foo", chunks[0]);
}

[Fact]
public async Task RenderAsync_CancellationToken_StopsProcessing()
{
    var renderer = new StreamingMarkdownRenderer(MarkdownStyle.Dark);
    var cts = new CancellationTokenSource();

    var stream = GetInfiniteStream();
    cts.CancelAfter(TimeSpan.FromMilliseconds(100));

    await Assert.ThrowsAsync<OperationCanceledException>(async () =>
    {
        await foreach (var chunk in renderer.RenderAsync(stream, cts.Token))
        {
            // Should be cancelled
        }
    });
}
```

## Migration from Current Code

### What to Keep
- `StreamingMarkdownRenderer.cs` - Core logic (refactor public API)
- `SyntaxHighlighter.cs` - Keep as-is (rename to internal if needed)
- `ParserState` enum - Keep as internal implementation detail

### What to Refactor
- Remove `StyleConfig` → Replace with `MarkdownStyle` (simplified)
- Remove dependency on `Spectre.Console.Color` → Use our own `AnsiColor` enum
- Add callback/event support
- Add `IAsyncEnumerable` support
- Add builder pattern
- Add telemetry/stats

### What to Remove
- No dependency on `Markdig` (not used in streaming mode)
- No dependency on `Spectre.Console` (generate ANSI directly)
- No `Program.cs` CLI logic

## Distribution

### NuGet Package
```bash
dotnet pack GlowSharp.Streaming.csproj -c Release
dotnet nuget push GlowSharp.Streaming.1.0.0.nupkg --source nuget.org
```

### Installation
```bash
dotnet add package GlowSharp.Streaming
```

### Versioning
- **1.0.0**: Initial release with callback, event, and async enumerable APIs
- **1.1.0**: Add custom syntax highlighter support
- **1.2.0**: Add middleware pipeline
- **2.0.0**: Add HTML output mode (not just ANSI)

## Platform Support

| Platform | Support |
|----------|---------|
| .NET 8.0+ | ✅ Full support |
| .NET 6.0/7.0 | ✅ Via multi-targeting |
| .NET Framework 4.8 | ⚠️ Limited (no IAsyncEnumerable) |
| Unity | ⚠️ Depends on Unity .NET version |
| Blazor WebAssembly | ✅ Full support (HTML mode) |
| Blazor Server | ✅ Full support |
| ASP.NET Core | ✅ Full support |
| Console Apps | ✅ Full support |

## Summary

**GlowSharp.Streaming** is a lightweight, zero-dependency library for progressive markdown rendering, perfect for:
- LLM streaming interfaces (ChatGPT, Claude, etc.)
- Real-time collaborative editing
- Live markdown preview
- Terminal-based chat applications
- Web applications with SignalR/WebSockets

**Key Differentiators**:
- ✅ Progressive rendering (typewriter effect)
- ✅ Zero external dependencies
- ✅ Multiple API styles (callback, event, async enumerable)
- ✅ Syntax highlighting for 16+ languages
- ✅ Thread-safe and cancellable
- ✅ Rich telemetry and stats
