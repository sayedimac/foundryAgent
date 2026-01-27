using FoundryAgent.Web.Models;
using FoundryAgent.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace FoundryAgent.Web.Controllers;

/// <summary>
/// Controller for chat interactions with Azure AI Foundry agents.
/// Demonstrates multiple agent capabilities including function tools, streaming, and telemetry.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly ModernAgentService _agentService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(ModernAgentService agentService, ILogger<ChatController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// Send a message to the agent and receive a response.
    /// </summary>
    /// <param name="request">The chat request containing the message and optional thread ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The agent's response.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ChatResponse>> Post(
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
                "Processing chat request. ThreadId: {ThreadId}, AgentType: {AgentType}",
                request.ThreadId,
                request.AgentType ?? "default");

            var response = await _agentService.RunAsync(
                request.Message,
                request.ThreadId,
                request.AgentType,
                cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Processing Error",
                Detail = "An error occurred processing your request",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Send a message to the agent and receive a streaming response.
    /// Uses Server-Sent Events (SSE) for real-time streaming.
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
            _logger.LogInformation("Starting streaming chat for message: {MessagePrefix}...",
                request.Message.Length > 50 ? request.Message[..50] : request.Message);

            await foreach (var chunk in _agentService.RunStreamingAsync(
                request.Message,
                request.ThreadId,
                request.AgentType,
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
            _logger.LogInformation("Streaming request was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during streaming response");
            await Response.WriteAsync($"data: {{\"error\": \"{ex.Message}\"}}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Upload files and send a message to the agent with file attachments.
    /// Demonstrates Code Interpreter capability for analyzing files.
    /// </summary>
    [HttpPost("upload")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50 MB limit
    public async Task<ActionResult<ChatResponse>> Upload(
        [FromForm] string message,
        [FromForm] string? threadId,
        [FromForm] string? agentType,
        [FromForm] List<IFormFile>? files,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message) && (files == null || files.Count == 0))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Request",
                Detail = "Message or files are required",
                Status = StatusCodes.Status400BadRequest
            });
        }

        try
        {
            var fileContents = new List<(string fileName, byte[] content)>();

            if (files != null && files.Count > 0)
            {
                foreach (var file in files)
                {
                    if (file.Length > 0)
                    {
                        using var memoryStream = new MemoryStream();
                        await file.CopyToAsync(memoryStream, cancellationToken);
                        fileContents.Add((file.FileName, memoryStream.ToArray()));
                        _logger.LogInformation("Received file: {FileName} ({Size} bytes)", file.FileName, file.Length);
                    }
                }
            }

            // For now, we'll include file info in the message
            // In a full implementation, you would upload files to the agent
            var enrichedMessage = message ?? "Please analyze the attached file(s).";
            if (fileContents.Count > 0)
            {
                enrichedMessage += $"\n\n[Note: {fileContents.Count} file(s) attached: {string.Join(", ", fileContents.Select(f => f.fileName))}]";
            }

            var response = await _agentService.RunAsync(
                enrichedMessage,
                threadId,
                agentType ?? "code-interpreter",
                cancellationToken);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing upload request");
            return StatusCode(500, new ProblemDetails
            {
                Title = "Upload Error",
                Detail = "An error occurred processing your upload",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }

    /// <summary>
    /// Get information about available agent capabilities.
    /// </summary>
    [HttpGet("capabilities")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetCapabilities()
    {
        return Ok(_agentService.GetCapabilities());
    }
}
