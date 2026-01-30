using FoundryAgent.Web.Models;
using FoundryAgent.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace FoundryAgent.Web.Controllers;

/// <summary>
/// Controller for interacting with pre-deployed Azure AI Foundry Agent Applications.
/// This provides access to agents configured in the Foundry portal with built-in tools
/// like Bing grounding, Code Interpreter, Azure AI Search, etc.
/// </summary>
[ApiController]
[Route("api/hosted-agent")]
public class HostedAgentController : ControllerBase
{
    private readonly HostedAgentService _hostedAgentService;
    private readonly ILogger<HostedAgentController> _logger;

    public HostedAgentController(
        HostedAgentService hostedAgentService,
        ILogger<HostedAgentController> logger)
    {
        _hostedAgentService = hostedAgentService;
        _logger = logger;
    }

    /// <summary>
    /// Get information about the hosted agent configuration.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetInfo()
    {
        var agentInfo = _hostedAgentService.GetInfo();
        return Ok(new
        {
            name = "Foundry Hosted Agent",
            description = "Pre-deployed agent configured in Azure AI Foundry portal (e.g., Margies Travel Agent)",
            features = new[]
            {
                "Bing Search Grounding",
                "Code Interpreter",
                "Azure AI Search",
                "File Search"
            },
            configuration = agentInfo
        });
    }

    /// <summary>
    /// Send a message to the hosted agent.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ChatResponse>> Post(
        [FromBody] HostedAgentRequest request,
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

        if (!_hostedAgentService.IsEnabled)
        {
            return StatusCode(503, new ProblemDetails
            {
                Title = "Service Unavailable",
                Detail = "Hosted agent is not configured. Please set HostedAgent:ResponsesApiEndpoint in configuration.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }

        try
        {
            _logger.LogInformation(
                "Processing hosted agent request. PreviousResponseId: {PreviousResponseId}",
                request.PreviousResponseId);

            var response = await _hostedAgentService.SendMessageAsync(
                request.Message,
                request.PreviousResponseId,
                cancellationToken);

            var textResponse = HostedAgentService.ExtractResponseText(response);
            var citations = HostedAgentService.ExtractCitations(response);

            return Ok(new ChatResponse
            {
                Response = textResponse,
                ThreadId = response.Id, // Use response ID as thread ID for continuity
                AgentId = "hosted-agent",
                Citations = citations.Count > 0 ? citations : null
            });
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("401") || ex.Message.Contains("Unauthorized"))
        {
            _logger.LogError(ex, "Authorization failed for hosted agent request. Check that the Azure CLI user has access to the Foundry application.");
            return StatusCode(401, new ProblemDetails
            {
                Title = "Unauthorized",
                Detail = "Authorization failed. Please ensure: 1) You are logged in with Azure CLI (az login), 2) The FoundryHostedAgent:ResponsesApiEndpoint is correct, 3) The application exists in the Azure AI Foundry project, 4) Your user has Cognitive Services OpenAI User role on the resource.",
                Status = StatusCodes.Status401Unauthorized
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing hosted agent request");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Processing Error",
                Detail = $"An error occurred processing your request: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Send a message to the hosted agent with streaming response.
    /// Uses Server-Sent Events (SSE) for real-time streaming.
    /// </summary>
    [HttpPost("stream")]
    [Produces("text/event-stream")]
    public async Task Stream(
        [FromBody] HostedAgentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsync("Message is required", cancellationToken);
            return;
        }

        if (!_hostedAgentService.IsEnabled)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await Response.WriteAsync("Hosted agent is not configured", cancellationToken);
            return;
        }

        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers.Connection = "keep-alive";

        try
        {
            _logger.LogInformation("Starting hosted agent streaming response");

            string? responseId = null;
            var responseText = new StringBuilder();

            await foreach (var chunk in _hostedAgentService.SendMessageStreamingAsync(
                request.Message,
                request.PreviousResponseId,
                cancellationToken))
            {
                // Capture the response ID for conversation continuity
                if (!string.IsNullOrEmpty(chunk.ResponseId))
                {
                    responseId = chunk.ResponseId;
                }

                // Stream text deltas
                if (!string.IsNullOrEmpty(chunk.Delta))
                {
                    responseText.Append(chunk.Delta);
                    await Response.WriteAsync($"data: {chunk.Delta}\n\n", cancellationToken);
                    await Response.Body.FlushAsync(cancellationToken);
                }
            }

            // Send completion event with response ID
            await Response.WriteAsync($"event: complete\ndata: {{\"responseId\":\"{responseId}\"}}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);

            _logger.LogInformation("Hosted agent streaming complete. ResponseId: {ResponseId}", responseId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during hosted agent streaming");
            await Response.WriteAsync($"event: error\ndata: {ex.Message}\n\n", cancellationToken);
        }
    }
}
