using System.Diagnostics;
using System.Text.Json;
using Azure;
using Azure.AI.Agents.Persistent;
using Azure.Identity;
using Microsoft.Extensions.Options;
using FoundryAgent.Web.Models;

namespace FoundryAgent.Web.Services;

/// <summary>
/// MCP Agent Service using PersistentAgentsClient with Model Context Protocol (MCP) tools.
/// This service demonstrates connecting to remote MCP servers like Microsoft Learn and GitHub.
/// 
/// Based on: https://github.com/MicrosoftLearning/mslearn-ai-agents/blob/main/Instructions/03c-use-agent-tools-with-mcp.md
/// 
/// Features:
/// - Connect to remote MCP servers (Microsoft Learn, GitHub, etc.)
/// - Automatic MCP tool approval handling
/// - Multi-turn conversations
/// </summary>
public class McpAgentService
{
    private readonly PersistentAgentsClient _agentClient;
    private readonly FoundryOptions _options;
    private readonly ILogger<McpAgentService> _logger;
    private readonly ActivitySource _activitySource;

    public McpAgentService(
        IOptions<FoundryOptions> options,
        ILogger<McpAgentService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _activitySource = new ActivitySource("FoundryAgent.MCP", "1.0.0");

        if (string.IsNullOrWhiteSpace(_options.ProjectEndpoint))
            throw new InvalidOperationException("Foundry:ProjectEndpoint is required.");
        if (string.IsNullOrWhiteSpace(_options.DeploymentName))
            throw new InvalidOperationException("Foundry:DeploymentName is required.");

        // Create credentials
        var credential = _options.UseDefaultAzureCredential
            ? new DefaultAzureCredential()
            : new AzureCliCredential() as Azure.Core.TokenCredential;

        // Create PersistentAgentsClient
        _agentClient = new PersistentAgentsClient(_options.ProjectEndpoint, credential);

        _logger.LogInformation("McpAgentService initialized with endpoint: {Endpoint}", _options.ProjectEndpoint);
        _logger.LogInformation("MCP servers configured: {Count}", _options.McpServers?.Length ?? 0);
    }

    /// <summary>
    /// Creates MCP tool definitions from configuration.
    /// </summary>
    private List<ToolDefinition> CreateMcpTools()
    {
        var tools = new List<ToolDefinition>();

        foreach (var serverConfig in _options.McpServers ?? [])
        {
            if (!serverConfig.Enabled)
            {
                _logger.LogInformation("Skipping disabled MCP server: {Label}", serverConfig.ServerLabel);
                continue;
            }

            // Create MCP tool definition
            var mcpTool = new MCPToolDefinition(
                serverLabel: serverConfig.ServerLabel,
                serverUrl: serverConfig.ServerUrl);

            // Add allowed tools if specified
            foreach (var toolName in serverConfig.AllowedTools ?? [])
            {
                mcpTool.AllowedTools.Add(toolName);
            }

            tools.Add(mcpTool);
            _logger.LogInformation("Added MCP tool: {Label} -> {Url}", serverConfig.ServerLabel, serverConfig.ServerUrl);
        }

        return tools;
    }

    /// <summary>
    /// Run a chat interaction with the MCP-enabled agent.
    /// Following the pattern from the Microsoft Learn tutorial.
    /// </summary>
    public async Task<ChatResponse> RunAsync(
        string input,
        bool autoApprove = true,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("McpAgentRun");
        activity?.SetTag("input.length", input.Length);
        activity?.SetTag("auto_approve", autoApprove);

        try
        {
            // Create MCP tools from configuration
            var mcpTools = CreateMcpTools();

            if (mcpTools.Count == 0)
            {
                return new ChatResponse
                {
                    Response = "No MCP servers are configured or enabled. Please configure at least one MCP server in appsettings.json.",
                    AgentId = "error"
                };
            }

            // Create the agent with MCP tools
            var agent = await _agentClient.Administration.CreateAgentAsync(
                model: _options.DeploymentName,
                name: "McpAgent",
                instructions: _options.Instructions ?? GetDefaultMcpInstructions(),
                tools: mcpTools,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created MCP agent: {Id}", agent.Value.Id);
            activity?.SetTag("agent.id", agent.Value.Id);

            try
            {
                // Create a thread for conversation
                var thread = await _agentClient.Threads.CreateThreadAsync(cancellationToken: cancellationToken);
                _logger.LogInformation("Created thread: {Id}", thread.Value.Id);

                // Create message in thread
                var message = await _agentClient.Messages.CreateMessageAsync(
                    threadId: thread.Value.Id,
                    role: MessageRole.User,
                    content: input,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Created message: {Id}", message.Value.Id);

                // Run the agent
                var run = await _agentClient.Runs.CreateRunAsync(
                    thread.Value,
                    agent.Value,
                    cancellationToken: cancellationToken);

                _logger.LogInformation("Started run: {Id}, Status: {Status}", run.Value.Id, run.Value.Status);

                // Poll for completion and handle MCP approvals
                run = await WaitForRunCompletionAsync(
                    thread.Value.Id,
                    run.Value,
                    autoApprove,
                    cancellationToken);

                // Get the messages
                var messages = _agentClient.Messages.GetMessages(
                    threadId: thread.Value.Id,
                    order: ListSortOrder.Descending);

                // Get the latest assistant message
                string responseText = string.Empty;
                foreach (var msg in messages)
                {
                    if (msg.Role == MessageRole.Agent)
                    {
                        foreach (var contentItem in msg.ContentItems)
                        {
                            if (contentItem is MessageTextContent textContent)
                            {
                                responseText = textContent.Text;
                                break;
                            }
                        }
                        break;
                    }
                }

                activity?.SetTag("response.length", responseText.Length);

                // Clean up thread
                await _agentClient.Threads.DeleteThreadAsync(thread.Value.Id, cancellationToken);

                return new ChatResponse
                {
                    Response = responseText,
                    AgentId = agent.Value.Id,
                    ThreadId = thread.Value.Id,
                    TraceId = activity?.TraceId.ToString()
                };
            }
            finally
            {
                // Clean up - delete the agent
                await _agentClient.Administration.DeleteAgentAsync(agent.Value.Id, cancellationToken);
                _logger.LogInformation("Deleted agent: {Id}", agent.Value.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running MCP agent");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Wait for run completion, handling MCP tool approvals.
    /// </summary>
    private async Task<Response<ThreadRun>> WaitForRunCompletionAsync(
        string threadId,
        ThreadRun run,
        bool autoApprove,
        CancellationToken cancellationToken)
    {
        Response<ThreadRun> currentRun = Response.FromValue(run, null!);

        while (currentRun.Value.Status == RunStatus.Queued ||
               currentRun.Value.Status == RunStatus.InProgress ||
               currentRun.Value.Status == RunStatus.RequiresAction)
        {
            await Task.Delay(500, cancellationToken);
            currentRun = await _agentClient.Runs.GetRunAsync(threadId, run.Id, cancellationToken);

            _logger.LogDebug("Run status: {Status}", currentRun.Value.Status);

            // Handle MCP tool approval requests
            if (currentRun.Value.Status == RunStatus.RequiresAction &&
                currentRun.Value.RequiredAction is SubmitToolApprovalAction toolApprovalAction &&
                autoApprove)
            {
                var toolApprovals = new List<ToolApproval>();

                foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
                {
                    if (toolCall is RequiredMcpToolCall mcpToolCall)
                    {
                        _logger.LogInformation(
                            "Auto-approving MCP tool call: {Name}, Arguments: {Args}",
                            mcpToolCall.Name,
                            mcpToolCall.Arguments);

                        toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
                    }
                }

                if (toolApprovals.Count > 0)
                {
                    currentRun = await _agentClient.Runs.SubmitToolOutputsToRunAsync(
                        threadId,
                        run.Id,
                        toolApprovals: toolApprovals,
                        cancellationToken: cancellationToken);

                    _logger.LogInformation("Submitted {Count} tool approvals", toolApprovals.Count);
                }
            }
        }

        if (currentRun.Value.Status == RunStatus.Failed)
        {
            _logger.LogError("Run failed: {Error}", currentRun.Value.LastError?.Message);
        }

        return currentRun;
    }

    /// <summary>
    /// Run a streaming chat interaction with the MCP-enabled agent.
    /// </summary>
    public async IAsyncEnumerable<string> RunStreamingAsync(
        string input,
        bool autoApprove = true,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("McpAgentRunStreaming");
        activity?.SetTag("streaming", true);

        // Create MCP tools from configuration
        var mcpTools = CreateMcpTools();

        if (mcpTools.Count == 0)
        {
            yield return "No MCP servers are configured or enabled.";
            yield break;
        }

        // Create the agent with MCP tools
        var agent = await _agentClient.Administration.CreateAgentAsync(
            model: _options.DeploymentName,
            name: "McpAgent",
            instructions: _options.Instructions ?? GetDefaultMcpInstructions(),
            tools: mcpTools,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created streaming MCP agent: {Id}", agent.Value.Id);

        try
        {
            // Create a thread for conversation
            var thread = await _agentClient.Threads.CreateThreadAsync(cancellationToken: cancellationToken);

            // Create message in thread
            await _agentClient.Messages.CreateMessageAsync(
                threadId: thread.Value.Id,
                role: MessageRole.User,
                content: input,
                cancellationToken: cancellationToken);

            // Run the agent with streaming
            var runStream = _agentClient.Runs.CreateRunStreamingAsync(
                thread.Value.Id,
                agent.Value.Id,
                cancellationToken: cancellationToken);

            await foreach (var streamingUpdate in runStream)
            {
                if (streamingUpdate is MessageContentUpdate contentUpdate)
                {
                    if (!string.IsNullOrEmpty(contentUpdate.Text))
                    {
                        yield return contentUpdate.Text;
                    }
                }
                else if (streamingUpdate is RunUpdate runUpdate)
                {
                    _logger.LogDebug("Run update: {Status}", runUpdate.UpdateKind);

                    // Handle MCP approvals in streaming (if needed)
                    if (runUpdate.Value?.Status == RunStatus.RequiresAction &&
                        runUpdate.Value?.RequiredAction is SubmitToolApprovalAction toolApprovalAction &&
                        autoApprove)
                    {
                        var toolApprovals = new List<ToolApproval>();

                        foreach (var toolCall in toolApprovalAction.SubmitToolApproval.ToolCalls)
                        {
                            if (toolCall is RequiredMcpToolCall mcpToolCall)
                            {
                                _logger.LogInformation("Auto-approving MCP tool: {Name}", mcpToolCall.Name);
                                toolApprovals.Add(new ToolApproval(mcpToolCall.Id, approve: true));
                            }
                        }

                        if (toolApprovals.Count > 0)
                        {
                            await _agentClient.Runs.SubmitToolOutputsToRunAsync(
                                thread.Value.Id,
                                runUpdate.Value.Id,
                                toolApprovals: toolApprovals,
                                cancellationToken: cancellationToken);
                        }
                    }
                }
            }

            // Clean up thread
            await _agentClient.Threads.DeleteThreadAsync(thread.Value.Id, cancellationToken);
        }
        finally
        {
            // Clean up agent
            await _agentClient.Administration.DeleteAgentAsync(agent.Value.Id, cancellationToken);
        }
    }

    /// <summary>
    /// Get information about configured MCP servers.
    /// </summary>
    public object GetMcpServerInfo()
    {
        return new
        {
            servers = (_options.McpServers ?? []).Select(s => new
            {
                label = s.ServerLabel,
                url = s.ServerUrl,
                enabled = s.Enabled,
                requireApproval = s.RequireApproval,
                allowedTools = s.AllowedTools
            }),
            features = new[]
            {
                "Remote MCP Server Connection",
                "Automatic Tool Approval",
                "Streaming Responses",
                "Multi-Server Support"
            }
        };
    }

    private string GetDefaultMcpInstructions()
    {
        var serverLabels = string.Join(", ",
            (_options.McpServers ?? [])
                .Where(s => s.Enabled)
                .Select(s => s.ServerLabel));

        return $"""
            You are a helpful AI assistant powered by Azure AI Foundry with access to MCP (Model Context Protocol) tools.
            
            You have access to the following MCP servers: {serverLabels}
            
            Use the available MCP tools to:
            - Search and retrieve information from Microsoft documentation
            - Access GitHub repositories, issues, and code
            - Perform tasks that require external knowledge
            
            When using MCP tools:
            - Always provide accurate information based on tool responses
            - Cite sources when available
            - If a tool fails or returns no results, explain what happened and suggest alternatives
            
            Be helpful, concise, and accurate in your responses.
            """;
    }
}
