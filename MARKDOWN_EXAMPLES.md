# Markdown Support in Foundry Agent

The chat application now supports full Markdown rendering for agent responses!

## Supported Features

### Text Formatting
- **Bold text** using `**bold**`
- *Italic text* using `*italic*`
- ~~Strikethrough~~ using `~~text~~`
- `Inline code` using backticks

### Headings
All heading levels (H1-H6) are supported

### Lists
Unordered lists:
- Item 1
- Item 2
  - Nested item
  - Another nested item

Ordered lists:
1. First item
2. Second item
3. Third item

### Code Blocks
Syntax highlighting is supported for many languages:

```javascript
function greet(name) {
    console.log(`Hello, ${name}!`);
}
```

```python
def greet(name):
    print(f"Hello, {name}!")
```

```csharp
public void Greet(string name)
{
    Console.WriteLine($"Hello, {name}!");
}
```

### Blockquotes
> This is a blockquote
> It can span multiple lines

### Links
[Visit Microsoft](https://www.microsoft.com)

### Tables
| Feature | Supported |
|---------|-----------|
| Bold    | ✅        |
| Tables  | ✅        |
| Code    | ✅        |

### Horizontal Rules
---

### Line Breaks
GitHub-flavored markdown (GFM) is enabled, so single line breaks
are preserved.

## Implementation Details

- **Client-side**: Uses [Marked.js](https://marked.js.org/) v14.1.2 for markdown parsing
- **Syntax highlighting**: Uses [Highlight.js](https://highlightjs.org/) v11.9.0
- **Server-side**: Markdig NuGet package added (optional for server-side processing)

## Try It Out!

Send a message to the agent and ask it to respond with markdown formatting. For example:
- "Give me a code example in Python"
- "Create a table comparing features"
- "Explain something with headings and lists"
