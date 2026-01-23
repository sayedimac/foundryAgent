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
                Name = "search_repositories",
                Description = "Search for GitHub repositories using GitHub search syntax in 'query' (e.g., 'language:javascript pushed:>=2026-01-01'). Do not pass sort-only strings like 'sort:updated-desc' as the query.",
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
                Name = "get_file_contents",
                Description = "Get the contents of a file from a GitHub repository",
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
                Name = "create_or_update_file",
                Description = "Create or update a file in a GitHub repository",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        owner = new { type = "string", description = "Repository owner" },
                        repo = new { type = "string", description = "Repository name" },
                        path = new { type = "string", description = "File path" },
                        content = new { type = "string", description = "File content" },
                        message = new { type = "string", description = "Commit message" },
                        branch = new { type = "string", description = "Branch name" }
                    },
                    required = new[] { "owner", "repo", "path", "content", "message", "branch" }
                }
            },
            new McpToolDefinition
            {
                Name = "list_issues",
                Description = "List issues in a GitHub repository",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        owner = new { type = "string", description = "Repository owner" },
                        repo = new { type = "string", description = "Repository name" }
                    },
                    required = new[] { "owner", "repo" }
                }
            },
            new McpToolDefinition
            {
                Name = "create_issue",
                Description = "Create a new issue in a GitHub repository",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        owner = new { type = "string", description = "Repository owner" },
                        repo = new { type = "string", description = "Repository name" },
                        title = new { type = "string", description = "Issue title" },
                        body = new { type = "string", description = "Issue body" }
                    },
                    required = new[] { "owner", "repo", "title" }
                }
            },
            new McpToolDefinition
            {
                Name = "create_pull_request",
                Description = "Create a new pull request in a GitHub repository",
                Parameters = new
                {
                    type = "object",
                    properties = new
                    {
                        owner = new { type = "string", description = "Repository owner" },
                        repo = new { type = "string", description = "Repository name" },
                        title = new { type = "string", description = "PR title" },
                        body = new { type = "string", description = "PR body" },
                        head = new { type = "string", description = "Source branch" },
                        @base = new { type = "string", description = "Target branch" }
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
