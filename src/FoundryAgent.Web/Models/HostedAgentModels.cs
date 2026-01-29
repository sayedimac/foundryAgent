using System.Text.Json.Serialization;

namespace FoundryAgent.Web.Models;

/// <summary>
/// Request model for hosted agent chat interactions.
/// </summary>
public class HostedAgentRequest
{
    /// <summary>
    /// The user message to send to the hosted agent.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Previous response ID for multi-turn conversations.
    /// </summary>
    public string? PreviousResponseId { get; set; }

    /// <summary>
    /// Enable streaming response mode.
    /// </summary>
    public bool Stream { get; set; } = false;
}

/// <summary>
/// Response from the hosted agent Responses API.
/// </summary>
public class HostedAgentResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("output")]
    public List<ResponseOutputItem> Output { get; set; } = [];

    [JsonPropertyName("usage")]
    public ResponseUsage? Usage { get; set; }
}

public class ResponseOutputItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("role")]
    public string? Role { get; set; }

    [JsonPropertyName("content")]
    public List<ResponseContentItem>? Content { get; set; }
}

public class ResponseContentItem
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string? Text { get; set; }

    [JsonPropertyName("annotations")]
    public List<ResponseAnnotation>? Annotations { get; set; }
}

public class ResponseAnnotation
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("start_index")]
    public int? StartIndex { get; set; }

    [JsonPropertyName("end_index")]
    public int? EndIndex { get; set; }
}

public class ResponseUsage
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; set; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; set; }

    [JsonPropertyName("total_tokens")]
    public int TotalTokens { get; set; }
}

/// <summary>
/// Streaming chunk from the hosted agent.
/// </summary>
public class StreamingChunk
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("response_id")]
    public string? ResponseId { get; set; }

    [JsonPropertyName("item_id")]
    public string? ItemId { get; set; }

    [JsonPropertyName("output_index")]
    public int? OutputIndex { get; set; }

    [JsonPropertyName("content_index")]
    public int? ContentIndex { get; set; }

    [JsonPropertyName("delta")]
    public string? Delta { get; set; }
}
