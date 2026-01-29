using FoundryAgent.Web.Models;
using FoundryAgent.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoundryAgent.Web.Controllers;

/// <summary>
/// Controller for MCP (Model Context Protocol) agent interactions.
/// Demonstrates connecting to remote MCP servers like Microsoft Learn and GitHub.
/// 
/// Based on: https://github.com/MicrosoftLearning/mslearn-ai-agents/blob/main/Instructions/03c-use-agent-tools-with-mcp.md
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class McpController : ControllerBase
{
    private readonly McpAgentService _mcpService;
    private readonly ILogger<McpController> _logger;

    public McpController(McpAgentService mcpService, ILogger<McpController> logger)
    {
        _mcpService = mcpService;
        _logger = logger;
    }

    /// <summary>
    /// Get information about configured MCP servers.
    /// </summary>
    [HttpGet("servers")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetMcpServers()
    {
        try
        {
            var serverInfo = _mcpService.GetMcpServerInfo();
            return Ok(serverInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting MCP server info");
            return StatusCode(500, new { error = "Failed to get MCP server info" });
        }
    }

    /// <summary>
    /// Send a message to the MCP-enabled agent and receive a response.
    /// </summary>
    /// <param name="request">The chat request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    [HttpPost("chat")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> Chat(
        [FromBody] ChatRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Message is required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            _logger.LogInformation(
                "Processing MCP chat request. AutoApprove: {AutoApprove}",
                request.AutoApproveMcpTools);

            var response = await _mcpService.RunAsync(
                request.Message,
                request.AutoApproveMcpTools,
                cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing MCP chat request");
            return StatusCode(500, new ProblemDetails
            {
                Title = "MCP Processing Error",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Send a message to the MCP-enabled agent with streaming response.
    /// Uses Server-Sent Events (SSE).
    /// </summary>
    [HttpPost("stream")]
    public async Task Stream([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { error = "Message is required" }, cancellationToken);
            return;
        }

        Response.Headers.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            _logger.LogInformation("Starting MCP streaming chat");

            await foreach (var chunk in _mcpService.RunStreamingAsync(
                request.Message,
                request.AutoApproveMcpTools,
                cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk))
                {
                    await Response.WriteAsync($"data: {chunk}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }

            await Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("MCP streaming request was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during MCP streaming response");
            await Response.WriteAsync($"data: {{\"error\": \"{ex.Message}\"}}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Test endpoint to verify MCP connectivity with a simple query.
    /// Uses Microsoft Learn MCP server by default.
    /// </summary>
    [HttpGet("test")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> Test(
        [FromQuery] string query = "Give me the Azure CLI commands to create an Azure Container App with a managed identity.",
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Running MCP test query: {Query}", query);

            var response = await _mcpService.RunAsync(
                query,
                autoApprove: true,
                cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running MCP test");
            return StatusCode(500, new ProblemDetails
            {
                Title = "MCP Test Error",
                Detail = ex.Message,
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}
