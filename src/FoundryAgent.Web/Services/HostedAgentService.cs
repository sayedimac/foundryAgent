using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using FoundryAgent.Web.Models;
using Microsoft.Extensions.Options;

namespace FoundryAgent.Web.Services;

/// <summary>
/// Service for interacting with pre-deployed Azure AI Foundry Agent Applications.
/// Uses the Responses API endpoint to communicate with hosted agents configured in the Foundry portal.
/// 
/// Features:
/// - Calls agents with Bing grounding, Code Interpreter, etc. configured in portal
/// - Supports streaming responses via SSE
/// - Multi-turn conversations using previous_response_id
/// </summary>
public class HostedAgentService
{
    private readonly HttpClient _httpClient;
    private readonly HostedAgentOptions _options;
    private readonly TokenCredential _credential;
    private readonly ILogger<HostedAgentService> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    public HostedAgentService(
        HttpClient httpClient,
        IOptions<HostedAgentOptions> options,
        ILogger<HostedAgentService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _credential = new DefaultAzureCredential();
        _logger = logger;

        _logger.LogInformation(
            "HostedAgentService initialized for application: {AppName}, Endpoint: {Endpoint}",
            _options.ApplicationName,
            _options.ResponsesApiEndpoint);
    }

    /// <summary>
    /// Check if the hosted agent is configured and enabled.
    /// </summary>
    public bool IsEnabled => _options.Enabled;

    /// <summary>
    /// Get information about the hosted agent configuration.
    /// </summary>
    public object GetInfo() => new
    {
        enabled = _options.Enabled,
        applicationName = _options.ApplicationName,
        displayName = _options.DisplayName ?? _options.ApplicationName,
        apiVersion = _options.ApiVersion,
        endpoint = _options.Enabled ? "configured" : "not configured"
    };

    /// <summary>
    /// Send a message to the hosted agent and receive a complete response.
    /// </summary>
    public async Task<HostedAgentResponse> SendMessageAsync(
        string message,
        string? previousResponseId = null,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Hosted agent is not configured.");

        var request = await CreateRequestAsync(message, previousResponseId, stream: false, cancellationToken);

        _logger.LogDebug("Sending non-streaming request to hosted agent");

        var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Hosted agent request failed: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            throw new HttpRequestException($"Hosted agent request failed: {response.StatusCode}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var result = JsonSerializer.Deserialize<HostedAgentResponse>(content, JsonOptions);

        _logger.LogDebug("Received response from hosted agent: {ResponseId}", result?.Id);

        return result ?? throw new InvalidOperationException("Failed to deserialize response");
    }

    /// <summary>
    /// Send a message to the hosted agent and stream the response.
    /// </summary>
    public async IAsyncEnumerable<StreamingChunk> SendMessageStreamingAsync(
        string message,
        string? previousResponseId = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            throw new InvalidOperationException("Hosted agent is not configured.");

        var request = await CreateRequestAsync(message, previousResponseId, stream: true, cancellationToken);

        _logger.LogDebug("Sending streaming request to hosted agent");

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Hosted agent streaming request failed: {StatusCode} - {Error}",
                response.StatusCode, errorContent);
            throw new HttpRequestException($"Hosted agent request failed: {response.StatusCode}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null && !cancellationToken.IsCancellationRequested)
        {
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;

            var data = line[6..];
            if (data == "[DONE]")
            {
                _logger.LogDebug("Streaming complete");
                break;
            }

            StreamingChunk? chunk = null;
            try
            {
                chunk = JsonSerializer.Deserialize<StreamingChunk>(data, JsonOptions);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse streaming chunk: {Data}", data);
                continue;
            }

            if (chunk != null)
            {
                yield return chunk;
            }
        }
    }

    /// <summary>
    /// Extract the text response from a HostedAgentResponse.
    /// </summary>
    public static string ExtractResponseText(HostedAgentResponse response)
    {
        var textParts = new List<string>();

        foreach (var output in response.Output)
        {
            if (output.Type == "message" && output.Content != null)
            {
                foreach (var content in output.Content)
                {
                    if (content.Type == "output_text" && !string.IsNullOrEmpty(content.Text))
                    {
                        textParts.Add(content.Text);
                    }
                }
            }
        }

        return string.Join("\n", textParts);
    }

    /// <summary>
    /// Extract citations/annotations from a HostedAgentResponse.
    /// </summary>
    public static List<Citation> ExtractCitations(HostedAgentResponse response)
    {
        var citations = new List<Citation>();

        foreach (var output in response.Output)
        {
            if (output.Content == null) continue;

            foreach (var content in output.Content)
            {
                if (content.Annotations == null) continue;

                foreach (var annotation in content.Annotations)
                {
                    if (annotation.Type == "url_citation")
                    {
                        citations.Add(new Citation
                        {
                            Title = annotation.Title ?? "",
                            Url = annotation.Url
                        });
                    }
                }
            }
        }

        return citations;
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        string message,
        string? previousResponseId,
        bool stream,
        CancellationToken cancellationToken)
    {
        // Get Azure AD token for Cognitive Services
        var tokenResponse = await _credential.GetTokenAsync(
            new TokenRequestContext(["https://cognitiveservices.azure.com/.default"]),
            cancellationToken);

        var endpoint = _options.ResponsesApiEndpoint;
        if (!endpoint.Contains("api-version"))
        {
            endpoint += endpoint.Contains('?') ? "&" : "?";
            endpoint += $"api-version={_options.ApiVersion}";
        }

        var requestBody = new
        {
            input = message,
            previous_response_id = previousResponseId,
            stream
        };

        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResponse.Token);

        _logger.LogDebug("Created request to: {Endpoint}, Stream: {Stream}", endpoint, stream);

        return request;
    }
}
