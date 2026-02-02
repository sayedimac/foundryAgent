using System.Text.Json;
using System.Text.Json.Serialization;

namespace FoundryAgent.Web.Services;

/// <summary>
/// Service for GitHub MCP tools that can be used with Azure AI Agent function calling.
/// This service wraps the GitHub MCP tools and exposes them as agent-compatible functions.
/// </summary>
public class McpGitHubService
{
    private readonly ILogger<McpGitHubService> _logger;

    public McpGitHubService(ILogger<McpGitHubService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Get GitHub MCP tool definitions that can be registered with the agent
    /// </summary>
    public List<McpToolDefinition> GetAvailableTools()
    {
        return new List<McpToolDefinition>
        {
            new McpToolDefinition
            {
                Name = "microsoft_docs_search",
                Description = "Search Microsoft documentation on MSlearn",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        query = new { type = "string", description = "GitHub repository search query (must include at least one keyword or qualifier, e.g. 'azure pushed:>=2026-01-01' or 'language:javascript stars:>100')" }
                    },
                    required = new[] { "query" }
                }
            },
            new McpToolDefinition
            {
                Name = "microsoft_docs_fetch",
                Description = "Fetch a file from a Microsoft documentation repository on MSlearn",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        owner = new { type = "string", description = "Repository owner" },
                        repo = new { type = "string", description = "Repository name" },
                        path = new { type = "string", description = "File path" }
                    },
                    required = new[] { "owner", "repo", "path" }
                }
            },
            new McpToolDefinition
            {
                Name = "microsoft_docs_contribute",
                Description = "Contribute to Microsoft Learn documentation by creating or updating content",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        owner = new { type = "string", description = "Documentation repository owner (e.g., MicrosoftDocs)" },
                        repo = new { type = "string", description = "Documentation repository name (e.g., azure-docs)" },
                        path = new { type = "string", description = "Path to the documentation file" },
                        content = new { type = "string", description = "Updated documentation content" },
                        message = new { type = "string", description = "Description of documentation changes" },
                        branch = new { type = "string", description = "Branch for the contribution" }
                    },
                    required = new[] { "owner", "repo", "path", "content", "message", "branch" }
                }
            },
            new McpToolDefinition
            {
                Name = "microsoft_docs_issues",
                Description = "List open documentation issues on Microsoft Learn repos",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        owner = new { type = "string", description = "Documentation repository owner (e.g., MicrosoftDocs)" },
                        repo = new { type = "string", description = "Documentation repository name (e.g., azure-docs)" }
                    },
                    required = new[] { "owner", "repo" }
                }
            },
            new McpToolDefinition
            {
                Name = "microsoft_docs_report_issue",
                Description = "Report an issue or suggest improvements for Microsoft Learn documentation",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        owner = new { type = "string", description = "Documentation repository owner (e.g., MicrosoftDocs)" },
                        repo = new { type = "string", description = "Documentation repository name (e.g., azure-docs)" },
                        title = new { type = "string", description = "Issue title describing the documentation problem" },
                        body = new { type = "string", description = "Detailed description of the issue or suggestion" }
                    },
                    required = new[] { "owner", "repo", "title" }
                }
            },
            new McpToolDefinition
            {
                Name = "microsoft_docs_submit_pr",
                Description = "Submit a pull request to contribute documentation updates to Microsoft Learn",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        owner = new { type = "string", description = "Documentation repository owner (e.g., MicrosoftDocs)" },
                        repo = new { type = "string", description = "Documentation repository name (e.g., azure-docs)" },
                        title = new { type = "string", description = "Pull request title describing the documentation update" },
                        body = new { type = "string", description = "Description of the documentation changes" },
                        head = new { type = "string", description = "Source branch with your changes" },
                        @base = new { type = "string", description = "Target branch (usually 'main' or 'live')" }
                    },
                    required = new[] { "owner", "repo", "title", "head", "base" }
                }
            }
        };
    }

    /// <summary>
    /// Format tools for discovery API response
    /// </summary>
    public string GetToolsAsJson()
    {
        var tools = GetAvailableTools();
        var response = new
        {
            jsonrpc = "2.0",
            id = 1,
            result = new
            {
                tools = tools.Select(t => new
                {
                    name = t.Name,
                    description = t.Description,
                    inputSchema = t.Parameters
                })
            }
        };

        return JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }
}

public class McpToolDefinition
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object Parameters { get; set; } = new { };
}
