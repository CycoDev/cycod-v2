# StreamingMarkdownRenderer - Feature Support

## ✅ Fully Supported Features

### Block Elements
1. **Headings (H1-H6)** - All 6 levels with color-coded styling
   - H1: Magenta + Bold
   - H2: Default + Bold
   - H3: Cyan + Bold
   - H4: Green + Bold
   - H5: Yellow + Italic
   - H6: Gray + Italic

2. **Code Blocks** - Fenced code blocks with syntax highlighting
   - Supports 16+ languages: Python, JavaScript, TypeScript, C#, Go, Rust, Java, C++, PHP, Swift, HTML, XML, CSS, JSON, SQL, Shell
   - Language-specific keyword highlighting
   - String and comment highlighting
   - Proper handling of split ``` markers across chunks

3. **Tables** - Markdown tables with box-drawing characters
   - Auto-calculated column widths
   - Bold headers
   - Unicode separators (│, ─, ┼)

4. **Horizontal Rules** - All variants supported
   - `---` (dashes)
   - `***` (asterisks)
   - `___` (underscores)
   - Renders as 80-character gray line

5. **Blockquotes** - Single-level quotes
   - Gray italic styling
   - Vertical bar prefix (│)
   - 2-space indentation

### Inline Elements
6. **Bold** - Double asterisks or underscores
   - `**bold**` → Bold text
   - `__bold__` → Bold text

7. **Italic** - Single asterisk or underscore
   - `*italic*` → Italic text
   - `_italic_` → Italic text

8. **Strikethrough** - Double tildes ✨ NEW!
   - `~~strikethrough~~` → Strikethrough text

9. **Inline Code** - Backticks
   - `` `code` `` → Yellow background

10. **Nested Formatting** - Combinations work correctly
    - `***bold italic***`
    - `**_bold italic_**`
    - `~~**bold strikethrough**~~`

## ⚠️ Partially Supported Features

### 1. **Lists**
- **Unordered lists**: Show with `-` bullet character
- **Ordered lists**: Show with `1. 2. 3.` numbering
- **Task lists**: Show literal `[x]` and `[ ]` instead of checkboxes
  - Could be enhanced to use: `✓` (checked) and `☐` (unchecked)

### 2. **Blockquotes**
- **Single-level quotes**: ✅ Working
- **Nested quotes**: ❌ Shows `> >` literally instead of additional indentation
  - Could be enhanced to detect and indent nested levels

### 3. **Links**
- Currently shows link text with URL in parentheses: `text (url)`
- Could be enhanced to:
  - Underline link text
  - Color URLs differently
  - Hide URL and only show text (like terminal hyperlinks)

## ❌ Not Supported Features

### 1. **Images**
- Image syntax `![alt](url)` is not specifically handled
- Would need special rendering (show alt text with indicator)

### 2. **Double-backtick Inline Code**
- ``` ``code with `backticks` inside`` ``` not handled
- Currently only single backtick pairs work

### 3. **HTML Tags**
- Raw HTML is not stripped or rendered
- Shows literally in output

### 4. **Footnotes**
- `[^1]` footnote syntax not supported

### 5. **Definition Lists**
- Not supported (extension feature)

### 6. **Subscript/Superscript**
- `H~2~O` and `x^2^` syntax not supported

### 7. **Emoji Shortcuts**
- `:smile:` emoji codes not converted

## 🔧 Technical Implementation Details

### State Machine
- **ParserState.None**: Processing normal text
- **ParserState.InCodeBlock**: Buffering code block until closing ```
- **ParserState.InTable**: Buffering table rows
- **ParserState.InBlockBuffer**: Generic block buffering

### Chunk Boundary Handling
- **Code blocks**: Keeps last 2 characters when buffering to handle split ```
- **Normal text**: Waits for newlines before rendering (except for long lines >100 chars)
- **Headings**: Waits for complete line (newline) before rendering
- **Tables**: Waits for complete rows (newline-delimited)

### Performance
- Renders simple text immediately (typewriter effect)
- Buffers complex structures until complete
- Simulated chunk size: 32-64 characters
- Simulated delay: 15-50ms between chunks

## 📝 Comparison with AnsiRenderer

The **AnsiRenderer** (non-streaming) uses Markdig and supports:
- All the above features PLUS:
- Task lists (via Markdig extension)
- More complex table structures
- Full AST-based parsing

The **StreamingMarkdownRenderer** prioritizes:
- Progressive rendering (no waiting for complete document)
- LLM streaming compatibility
- Core markdown features only

## 🎯 Recommended Enhancements

If you want to improve the StreamingMarkdownRenderer, consider adding (in priority order):

1. **Task list checkboxes** - Easy win, big visual improvement
   - Replace `[x]` with ✓
   - Replace `[ ]` with ☐

2. **Link styling** - Underline and/or color links
   - Detect `[text](url)` pattern
   - Apply underline ANSI code

3. **Nested blockquotes** - Better quote rendering
   - Count `>` characters
   - Indent proportionally

4. **Image indicators** - Show that images exist
   - Detect `![alt](url)`
   - Render as `[Image: alt]` or similar

5. **HTML stripping** - Remove raw HTML tags
   - Strip `<tag>` patterns
   - Preserve text content
