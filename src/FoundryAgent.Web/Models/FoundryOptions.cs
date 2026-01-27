namespace FoundryAgent.Web.Models;

/// <summary>
/// Configuration options for Azure AI Foundry integration.
/// Supports multiple agent features including Bing grounding, Azure AI Search, and Code Interpreter.
/// </summary>
public class FoundryOptions
{
    /// <summary>
    /// The Azure AI Foundry project endpoint.
    /// Format: https://{resource}.services.ai.azure.com/api/projects/{project-name}
    /// </summary>
    public string ProjectEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// The model deployment name (e.g., "gpt-4o", "gpt-4o-mini").
    /// </summary>
    public string DeploymentName { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use DefaultAzureCredential (true) or AzureCliCredential (false).
    /// </summary>
    public bool UseDefaultAzureCredential { get; set; } = true;

    /// <summary>
    /// Default system instructions for the agent.
    /// </summary>
    public string? Instructions { get; set; }

    /// <summary>
    /// Bing connection ID for grounding search (optional).
    /// Required for BingGroundingToolDefinition.
    /// </summary>
    public string? BingConnectionId { get; set; }

    /// <summary>
    /// Azure AI Search connection ID (optional).
    /// Required for AzureAISearchToolDefinition.
    /// </summary>
    public string? AzureAISearchConnectionId { get; set; }

    /// <summary>
    /// Azure AI Search index name (optional).
    /// </summary>
    public string? AzureAISearchIndexName { get; set; }

    /// <summary>
    /// Enable OpenTelemetry tracing for observability.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;

    /// <summary>
    /// OTLP endpoint for exporting traces (optional).
    /// If not set, traces will be exported to console.
    /// </summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>
    /// Application Insights connection string (optional).
    /// If set, traces will be exported to Application Insights.
    /// </summary>
    public string? ApplicationInsightsConnectionString { get; set; }
}
