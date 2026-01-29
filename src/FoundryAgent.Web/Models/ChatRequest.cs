namespace FoundryAgent.Web.Models;

/// <summary>
/// Request model for chat interactions with the agent.
/// </summary>
public class ChatRequest
{
    /// <summary>
    /// The user message to send to the agent.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Optional thread ID for multi-turn conversations.
    /// If not provided, a new conversation thread will be created.
    /// </summary>
    public string? ThreadId { get; set; }

    /// <summary>
    /// Enable streaming response mode.
    /// </summary>
    public bool Stream { get; set; } = false;

    /// <summary>
    /// Optional agent type override for this request.
    /// Supported values: "default", "code-interpreter", "bing-search", "ai-search", "mcp"
    /// </summary>
    public string? AgentType { get; set; }

    /// <summary>
    /// Whether to automatically approve MCP tool calls.
    /// Only applies when AgentType is "mcp".
    /// </summary>
    public bool AutoApproveMcpTools { get; set; } = true;
}
