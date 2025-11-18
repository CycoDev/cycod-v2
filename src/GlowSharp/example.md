---
title: Example Markdown
author: GlowSharp
---

# GlowSharp Example

This is an example markdown file to demonstrate **GlowSharp** rendering capabilities.

## Features

GlowSharp supports:

- **Bold text**
- *Italic text*
- ~~Strikethrough~~
- `inline code`
- [Links](https://github.com)

## Code Blocks

### C# Example

```csharp
public class HelloWorld
{
    public static void Main(string[] args)
    {
        // Print a greeting
        Console.WriteLine("Hello, World!");

        var message = "Welcome to GlowSharp";
        if (message != null)
        {
            Console.WriteLine(message);
        }
    }
}
```

### JavaScript Example

```javascript
function greet(name) {
    // Return a greeting
    const message = `Hello, ${name}!`;
    return message;
}

const result = greet('World');
console.log(result);
```

### Python Example

```python
def fibonacci(n):
    """Generate Fibonacci sequence"""
    if n <= 1:
        return n
    return fibonacci(n-1) + fibonacci(n-2)

# Print first 10 Fibonacci numbers
for i in range(10):
    print(f"F({i}) = {fibonacci(i)}")
```

## Block Quotes

> This is a block quote.
> It can span multiple lines.
>
> And even multiple paragraphs!

## Lists

### Unordered List

- First item
- Second item
  - Nested item 1
  - Nested item 2
- Third item

### Ordered List

1. First step
2. Second step
3. Third step

## Horizontal Rule

---

## Tables

| Feature | Supported | Notes |
|---------|-----------|-------|
| Headings | ✅ | H1-H6 |
| Code Blocks | ✅ | With syntax highlighting |
| Tables | ✅ | Pipe tables |
| Lists | ✅ | Ordered and unordered |

## Inline Code

You can use `var x = 42;` inline with text for quick code snippets.

---

**That's it!** GlowSharp makes markdown beautiful in the terminal.
