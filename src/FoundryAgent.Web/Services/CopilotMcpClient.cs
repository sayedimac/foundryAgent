using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FoundryAgent.Web.Services;

/// <summary>
/// Minimal JSON-RPC client for the GitHub Copilot MCP endpoint.
/// 
/// Requires an auth token (Copilot/MCP) via configuration or environment variable.
/// </summary>
public sealed class CopilotMcpClient
{
    private const string DefaultMcpEndpoint = "https://api.githubcopilot.com/mcp/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<CopilotMcpClient> _logger;
    private readonly string _endpoint;

    public CopilotMcpClient(HttpClient httpClient, IConfiguration config, ILogger<CopilotMcpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        // GitHub Copilot MCP requires the Accept header to include both JSON and SSE.
        // Without this, the gateway returns 400: "Accept must contain both 'application/json' and 'text/event-stream'".
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));

        _endpoint = config["CopilotMcp:Endpoint"]
            ?? Environment.GetEnvironmentVariable("COPILOT_MCP_ENDPOINT")
            ?? DefaultMcpEndpoint;

        var token = config["CopilotMcp:Token"]
            ?? Environment.GetEnvironmentVariable("COPILOT_MCP_TOKEN")
            ?? Environment.GetEnvironmentVariable("GITHUB_COPILOT_MCP_TOKEN");

        if (!string.IsNullOrWhiteSpace(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public bool HasAuthHeader => _httpClient.DefaultRequestHeaders.Authorization != null;

    public async Task<string> CallToolAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken)
    {
        // JSON-RPC request expected by MCP gateway
        var request = new
        {
            jsonrpc = "2.0",
            id = 1,
            method = "tools/call",
            @params = new
            {
                name = toolName,
                arguments = arguments
            }
        };

        var json = JsonSerializer.Serialize(request);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        // Don’t log auth headers or tokens; only status + truncated body.
        _logger.LogInformation("MCP tools/call {Tool} => {Status}", toolName, (int)response.StatusCode);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"MCP call failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
        }

        return body;
    }
}
