using FoundryAgent.Web.Models;
using FoundryAgent.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoundryAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly AgentService _agentService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(AgentService agentService, ILogger<ChatController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ChatResponse>> Post([FromBody] ChatRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return BadRequest(new { error = "Message is required" });
        }

        try
        {
            var (response, threadId) = await _agentService.RunAsync(request.Message, request.ThreadId, cancellationToken);
            return Ok(new ChatResponse
            {
                Response = response,
                ThreadId = threadId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new { error = "An error occurred processing your request" });
        }
    }

    [HttpPost("upload")]
    public async Task<ActionResult<ChatResponse>> Upload([FromForm] string message, [FromForm] string? threadId, [FromForm] List<IFormFile>? files, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(message) && (files == null || files.Count == 0))
        {
            return BadRequest(new { error = "Message or files are required" });
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
                    }
                }
            }

            var (response, returnedThreadId) = await _agentService.RunAsync(
                message ?? "Please analyze the attached file(s).",
                fileContents,
                threadId,
                cancellationToken);

            return Ok(new ChatResponse
            {
                Response = response,
                ThreadId = returnedThreadId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request with files");
            return StatusCode(500, new { error = "An error occurred processing your request" });
        }
    }
}
