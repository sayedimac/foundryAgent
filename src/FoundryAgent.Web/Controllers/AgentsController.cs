using FoundryAgent.Web.Models;
using FoundryAgent.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace FoundryAgent.Web.Controllers;

/// <summary>
/// Controller for managing and introspecting agents.
/// Provides endpoints for listing capabilities, health status, and agent information.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AgentsController : ControllerBase
{
    private readonly ModernAgentService _agentService;
    private readonly ILogger<AgentsController> _logger;

    public AgentsController(ModernAgentService agentService, ILogger<AgentsController> logger)
    {
        _agentService = agentService;
        _logger = logger;
    }

    /// <summary>
    /// Get information about available agents and their capabilities.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetAgentInfo()
    {
        return Ok(new
        {
            name = "Foundry Agent Demo",
            version = "1.0.0",
            description = "Demonstrates Azure AI Foundry Agent SDK capabilities",
            capabilities = _agentService.GetCapabilities(),
            endpoints = new[]
            {
                new { method = "POST", path = "/api/chat", description = "Send a message to the agent" },
                new { method = "POST", path = "/api/chat/stream", description = "Stream a response from the agent" },
                new { method = "POST", path = "/api/chat/upload", description = "Send files to the agent" },
                new { method = "GET", path = "/api/chat/capabilities", description = "Get agent capabilities" },
                new { method = "GET", path = "/api/agents", description = "Get agent information" },
                new { method = "GET", path = "/api/mcp/discover", description = "Discover MCP tools" },
                new { method = "GET", path = "/health", description = "Health check endpoint" }
            }
        });
    }

    /// <summary>
    /// Get SDK and feature information for documentation purposes.
    /// </summary>
    [HttpGet("features")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult GetFeatures()
    {
        return Ok(new
        {
            sdk = new
            {
                name = "Azure AI Foundry Agent SDK",
                packages = new object[]
                {
                    new { name = "Azure.AI.Projects", version = "1.2.0-beta.5", purpose = "Core AI Project client" },
                    new { name = "Microsoft.Agents.AI.AzureAI", version = "1.0.0-preview", purpose = "Microsoft Agent Framework integration" },
                    new { name = "Azure.Identity", version = "1.17.1", purpose = "Azure authentication" }
                }
            },
            features = new object[]
            {
                new
                {
                    name = "AIProjectClient",
                    description = "Modern, recommended approach for creating and managing agents",
                    status = "Recommended",
                    note = "Replaces PersistentAgentsClient (legacy)"
                },
                new
                {
                    name = "Function Tools",
                    description = "Define custom functions that the agent can call",
                    status = "Enabled",
                    note = "Examples: GetWeather, Calculate, SearchProducts"
                },
                new
                {
                    name = "OpenTelemetry Tracing",
                    description = "Observability and distributed tracing support",
                    status = "Enabled",
                    note = "Exporters: Console, OTLP, Application Insights"
                },
                new
                {
                    name = "Streaming Responses",
                    description = "Real-time streaming of agent responses via SSE",
                    status = "Enabled",
                    note = "Endpoint: /api/chat/stream"
                },
                new
                {
                    name = "Code Interpreter",
                    description = "Execute Python code for data analysis and visualization",
                    status = "Available",
                    note = "Requires appropriate model deployment"
                },
                new
                {
                    name = "Bing Grounding",
                    description = "Web search capability for real-time information",
                    status = "Available",
                    note = "Requires BingConnectionId configuration"
                },
                new
                {
                    name = "Azure AI Search",
                    description = "Enterprise knowledge base integration",
                    status = "Available",
                    note = "Requires AzureAISearchConnectionId configuration"
                }
            },
            documentation = new
            {
                agentFramework = "https://learn.microsoft.com/agent-framework/",
                azureAIFoundry = "https://learn.microsoft.com/azure/ai-foundry/",
                bestPractices = "https://learn.microsoft.com/azure/ai-foundry/agents/",
                samples = "https://github.com/microsoft/agent-framework/tree/main/dotnet/samples"
            }
        });
    }

    /// <summary>
    /// Demo endpoint to test various agent capabilities.
    /// </summary>
    [HttpPost("demo")]
    [ProducesResponseType(typeof(ChatResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ChatResponse>> RunDemo(
        [FromQuery] string scenario = "weather",
        CancellationToken cancellationToken = default)
    {
        var demoMessages = new Dictionary<string, string>
        {
            ["weather"] = "What's the weather like in Seattle, USA?",
            ["calculate"] = "Calculate 25 * 4 + 10",
            ["time"] = "What is the current date and time?",
            ["search"] = "Search for AI-related products in the catalog",
            ["multi-tool"] = "First tell me the current time, then calculate 100 / 4, and finally get the weather in London, UK."
        };

        if (!demoMessages.TryGetValue(scenario.ToLowerInvariant(), out var message))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Scenario",
                Detail = $"Unknown scenario '{scenario}'. Available: {string.Join(", ", demoMessages.Keys)}",
                Status = StatusCodes.Status400BadRequest
            });
        }

        _logger.LogInformation("Running demo scenario: {Scenario}", scenario);

        try
        {
            var response = await _agentService.RunAsync(message, cancellationToken: cancellationToken);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running demo scenario: {Scenario}", scenario);
            return StatusCode(500, new ProblemDetails
            {
                Title = "Demo Error",
                Detail = $"Error running demo: {ex.Message}",
                Status = StatusCodes.Status500InternalServerError
            });
        }
    }
}
