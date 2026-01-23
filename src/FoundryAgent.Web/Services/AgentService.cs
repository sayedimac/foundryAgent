using Azure.AI.Agents.Persistent;
using Azure.Core;
using Azure.Identity;
using FoundryAgent.Web.Models;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FoundryAgent.Web.Services;

public class AgentService
{
    private readonly PersistentAgentsClient _client;
    private readonly PersistentAgent _agent;
    private readonly string _modelDeploymentName;
    private readonly McpGitHubService _mcpGitHubService;
    private readonly CopilotMcpClient _copilotMcpClient;

    private sealed record ToolOutputDto(
        [property: JsonPropertyName("tool_call_id")] string ToolCallId,
        [property: JsonPropertyName("output")] string Output);

    public AgentService(IOptions<FoundryOptions> options, McpGitHubService mcpGitHubService, CopilotMcpClient copilotMcpClient)
    {
        _mcpGitHubService = mcpGitHubService;
        _copilotMcpClient = copilotMcpClient;
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ProjectEndpoint))
            throw new InvalidOperationException("Foundry:ProjectEndpoint is required.");
        if (string.IsNullOrWhiteSpace(opts.DeploymentName))
            throw new InvalidOperationException("Foundry:DeploymentName is required.");

        _modelDeploymentName = opts.DeploymentName;
        var endpoint = opts.ProjectEndpoint;
        TokenCredential credential = opts.UseDefaultAzureCredential
            ? new DefaultAzureCredential()
            : new AzureCliCredential();

        _client = new PersistentAgentsClient(endpoint, credential);

        // Create agent with both code interpreter and MCP function tools
        var tools = new List<ToolDefinition> { new CodeInterpreterToolDefinition() };

        // Add MCP GitHub tools as function definitions
        foreach (var mcpTool in _mcpGitHubService.GetAvailableTools())
        {
            tools.Add(new FunctionToolDefinition(
                name: mcpTool.Name,
                description: mcpTool.Description,
                parameters: BinaryData.FromObjectAsJson(mcpTool.Parameters)
            ));
        }

        _agent = _client.Administration.CreateAgent(
            name: "FoundryAgent",
            model: opts.DeploymentName,
            instructions: opts.Instructions ?? "You are a helpful assistant with access to GitHub operations. When users ask about GitHub repositories, issues, files, or pull requests, use the available GitHub tools.",
            tools: tools
        );
    }

    public async Task<(string response, string threadId)> RunAsync(string input, string? threadId = null, CancellationToken cancellationToken = default)
    {
        // Get or create thread
        PersistentAgentThread thread;
        if (string.IsNullOrWhiteSpace(threadId))
        {
            thread = _client.Threads.CreateThread().Value;
        }
        else
        {
            thread = _client.Threads.GetThread(threadId).Value;
        }

        // Create message in thread
        _client.Messages.CreateMessage(
            thread.Id,
            MessageRole.User,
            input);

        // Run the agent
        ThreadRun run = _client.Runs.CreateRun(thread.Id, _agent.Id);

        // Poll for completion and handle tool calls
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            run = _client.Runs.GetRun(thread.Id, run.Id);

            // Handle required action (function calling)
            if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolOutputsAction submitToolOutputsAction)
            {
                var toolOutputs = new List<ToolOutputDto>();

                foreach (var toolCall in submitToolOutputsAction.ToolCalls)
                {
                    if (toolCall is RequiredFunctionToolCall functionToolCall)
                    {
                        var output = await HandleFunctionCallAsync(functionToolCall, cancellationToken);
                        toolOutputs.Add(new ToolOutputDto(toolCall.Id, output));
                    }
                }

                // Submit tool outputs
                if (toolOutputs.Count > 0)
                {
                    var content = RequestContent.Create(BinaryData.FromObjectAsJson(new { tool_outputs = toolOutputs }));
                    _client.Runs.SubmitToolOutputsToRun(thread.Id, run.Id, content);
                    run = _client.Runs.GetRun(thread.Id, run.Id);
                }
            }
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction);

        // Get the latest message
        var messages = _client.Messages.GetMessages(thread.Id, order: ListSortOrder.Descending);
        var latestMessage = messages.FirstOrDefault();

        var responseText = string.Empty;
        if (latestMessage != null)
        {
            foreach (var contentItem in latestMessage.ContentItems)
            {
                if (contentItem is MessageTextContent textContent)
                {
                    responseText += textContent.Text;
                }
            }
        }

        return (responseText, thread.Id);
    }

    public async Task<(string response, string threadId)> RunAsync(
        string input,
        List<(string fileName, byte[] content)> files,
        string? threadId = null,
        CancellationToken cancellationToken = default)
    {
        // Get or create thread
        PersistentAgentThread thread;
        if (string.IsNullOrWhiteSpace(threadId))
        {
            thread = _client.Threads.CreateThread().Value;
        }
        else
        {
            thread = _client.Threads.GetThread(threadId).Value;
        }

        // Upload files and create attachments
        var attachments = new List<MessageAttachment>();
        if (files.Count > 0)
        {
            foreach (var (fileName, content) in files)
            {
                // Upload file
                using var stream = new MemoryStream(content);
                var uploadedFileResponse = await _client.Files.UploadFileAsync(
                    stream,
                    PersistentAgentFilePurpose.Agents,
                    fileName);

                // Create attachment with code interpreter tool
                attachments.Add(new MessageAttachment(
                    fileId: uploadedFileResponse.Value.Id,
                    tools: new List<ToolDefinition> { new CodeInterpreterToolDefinition() }
                ));
            }
        }

        // Create message with attachments
        _client.Messages.CreateMessage(
            thread.Id,
            MessageRole.User,
            input,
            attachments: attachments);

        // Run the agent
        ThreadRun run = _client.Runs.CreateRun(thread.Id, _agent.Id);

        // Poll for completion and handle tool calls
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(500), cancellationToken);
            run = _client.Runs.GetRun(thread.Id, run.Id);

            // Handle required action (function calling)
            if (run.Status == RunStatus.RequiresAction && run.RequiredAction is SubmitToolOutputsAction submitToolOutputsAction)
            {
                var toolOutputs = new List<ToolOutputDto>();

                foreach (var toolCall in submitToolOutputsAction.ToolCalls)
                {
                    if (toolCall is RequiredFunctionToolCall functionToolCall)
                    {
                        var output = await HandleFunctionCallAsync(functionToolCall, cancellationToken);
                        toolOutputs.Add(new ToolOutputDto(toolCall.Id, output));
                    }
                }

                // Submit tool outputs
                if (toolOutputs.Count > 0)
                {
                    var content = RequestContent.Create(BinaryData.FromObjectAsJson(new { tool_outputs = toolOutputs }));
                    _client.Runs.SubmitToolOutputsToRun(thread.Id, run.Id, content);
                    run = _client.Runs.GetRun(thread.Id, run.Id);
                }
            }
        }
        while (run.Status == RunStatus.Queued || run.Status == RunStatus.InProgress || run.Status == RunStatus.RequiresAction);

        // Get the latest assistant message
        var messages = _client.Messages.GetMessages(thread.Id, order: ListSortOrder.Descending);
        var latestMessage = messages.FirstOrDefault(m => m.Role == MessageRole.Agent);

        var responseText = string.Empty;
        if (latestMessage != null)
        {
            foreach (var contentItem in latestMessage.ContentItems)
            {
                if (contentItem is MessageTextContent textContent)
                {
                    responseText += textContent.Text;
                }
            }
        }

        return (responseText, thread.Id);
    }

    private async Task<string> HandleFunctionCallAsync(RequiredFunctionToolCall functionToolCall, CancellationToken cancellationToken)
    {
        try
        {
            var functionName = functionToolCall.Name;

            if (!_copilotMcpClient.HasAuthHeader)
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    function = functionName,
                    error = "Missing Copilot MCP auth token.",
                    howToFix = "Set environment variable COPILOT_MCP_TOKEN (or config CopilotMcp:Token) to a valid Bearer token for https://api.githubcopilot.com/mcp/."
                });
            }

            // Azure SDK gives arguments as a JSON string
            var argsJson = functionToolCall.Arguments ?? "{}";

            if (string.Equals(functionName, "search_repositories", StringComparison.OrdinalIgnoreCase))
            {
                argsJson = NormalizeSearchRepositoriesArgs(argsJson);
            }

            using var doc = JsonDocument.Parse(argsJson);
            var resultJson = await _copilotMcpClient.CallToolAsync(functionName, doc.RootElement, cancellationToken);
            return resultJson;
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    private static string NormalizeSearchRepositoriesArgs(string argsJson)
    {
        // MCP GitHub "search_repositories" ultimately maps to GitHub repo search.
        // Agents sometimes send invalid sort-only queries like "sort:updated-desc".
        // This normalizes those into a valid query so the tool call succeeds.
        var node = JsonNode.Parse(argsJson) as JsonObject ?? new JsonObject();

        var query = (node["query"]?.GetValue<string>() ?? string.Empty).Trim();

        // Remove common invalid/unsupported inline sort tokens.
        // Sorting should be handled by tool parameters (if supported) or by filtering (pushed:>=...).
        if (!string.IsNullOrWhiteSpace(query))
        {
            query = query
                .Replace("sort:updated-desc", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("sort:updated-asc", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("sort:updated", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        // If the query is empty (or became empty after stripping sort tokens), choose a sensible default
        // that satisfies GitHub search syntax while matching "recently updated" intent.
        if (string.IsNullOrWhiteSpace(query))
        {
            var since = DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd");
            query = $"pushed:>={since}";
        }

        node["query"] = query;
        return node.ToJsonString(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });
    }
}
