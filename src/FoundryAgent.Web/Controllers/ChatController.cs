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
            var response = await _agentService.RunAsync(request.Message, cancellationToken);
            return Ok(new ChatResponse
            {
                Response = response,
                ThreadId = request.ThreadId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing chat request");
            return StatusCode(500, new { error = "An error occurred processing your request" });
        }
    }
}
