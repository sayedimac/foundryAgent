# Integrating MCP into Your Foundry Agent

## Easiest Approach: Use Function Calling with MCP Servers

Since you already have the Microsoft Agent Framework, here's the simplest integration:

### Option 1: Add MCP Tools as Functions (Recommended for now)

Create an MCP service that connects to your MCP servers and exposes them as functions to your agent.

```csharp
// Services/McpService.cs
using System.Text.Json;

namespace FoundryAgent.Web.Services;

public class McpService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<McpService> _logger;

    public McpService(HttpClient httpClient, ILogger<McpService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // Call your local MCP server
    public async Task<string> CallMcpToolAsync(string serverUrl, string toolName, object parameters)
    {
        try
        {
            var request = new
            {
                jsonrpc = "2.0",
                id = Guid.NewGuid().ToString(),
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = parameters
                }
            };

            var response = await _httpClient.PostAsJsonAsync(serverUrl, request);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonDocument>();
            return result?.RootElement.GetProperty("result").ToString() ?? string.Empty;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling MCP tool {ToolName}", toolName);
            throw;
        }
    }
}
```

### Option 2: Modify AgentService to Support MCP Tools

Update your `AgentService.cs` to detect MCP commands and route them:

```csharp
public async Task<(string response, string threadId)> RunAsync(
    string input,
    string? threadId = null,
    CancellationToken cancellationToken = default)
{
    // Check if input contains MCP command (e.g., "@github list repos")
    if (input.StartsWith("@"))
    {
        return await HandleMcpCommand(input, threadId, cancellationToken);
    }

    // Regular agent processing
    // ... existing code
}

private async Task<(string response, string threadId)> HandleMcpCommand(
    string input,
    string? threadId,
    CancellationToken cancellationToken)
{
    // Parse MCP command
    // "@github list repos" -> server: github, action: list repos
    var parts = input.TrimStart('@').Split(' ', 2);
    var server = parts[0];
    var command = parts.Length > 1 ? parts[1] : string.Empty;

    // Call your MCP server at http://localhost:5049/mcp
    var mcpResponse = await _mcpService.CallMcpToolAsync(
        "http://localhost:5049/mcp",
        command,
        new { }
    );

    return (mcpResponse, threadId ?? Guid.NewGuid().ToString());
}
```

### Option 3: Use System Instructions with Tool Descriptions

Add MCP tools to your agent's system instructions:

```csharp
// In AgentService constructor
var instructions = $@"You are a helpful assistant with access to the following tools:

**GitHub Tools (via @github)**:
- @github list repos - List user repositories
- @github create issue - Create an issue
- @github search code - Search code

**Azure Tools (via @azure)**:
- @azure list resources - List Azure resources
- @azure get metrics - Get resource metrics

When a user asks about GitHub or Azure, use the appropriate @ command.

{opts.Instructions ?? ""}";

_agent = _client.Administration.CreateAgent(
    name: "FoundryAgent",
    model: opts.DeploymentName,
    instructions: instructions,
    tools: new List<ToolDefinition> { new CodeInterpreterToolDefinition() }
);
```

## Quick Implementation Steps:

1. **Register HttpClient for MCP**:
```csharp
// In Program.cs
builder.Services.AddHttpClient<McpService>();
builder.Services.AddSingleton<McpService>();
```

2. **Create MCP command parser** in your agent
3. **Test with**: "@github list my repos"

## Future: Full MCP Client Integration

When Microsoft releases the official MCP client package, you can upgrade to:
- Automatic tool discovery from MCP servers
- Native function calling integration
- Streaming support for MCP tools

Would you like me to implement any of these options?
