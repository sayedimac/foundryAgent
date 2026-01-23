using FoundryAgent.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FoundryAgent.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class McpController : ControllerBase
{
    private readonly McpGitHubService _mcpService;
    private readonly ILogger<McpController> _logger;

    public McpController(McpGitHubService mcpService, ILogger<McpController> logger)
    {
        _mcpService = mcpService;
        _logger = logger;
    }

    [HttpGet("discover")]
    public ActionResult DiscoverTools()
    {
        try
        {
            var toolsJson = _mcpService.GetToolsAsJson();
            return Content(toolsJson, "application/json");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering MCP tools");
            return StatusCode(500, new { error = "Failed to discover tools" });
        }
    }
}
