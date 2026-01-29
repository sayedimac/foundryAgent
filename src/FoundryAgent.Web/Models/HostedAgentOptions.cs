namespace FoundryAgent.Web.Models;

/// <summary>
/// Configuration options for pre-deployed Azure AI Foundry Agent Applications.
/// These agents are created and configured in the Foundry portal and accessed via REST APIs.
/// </summary>
public class HostedAgentOptions
{
    /// <summary>
    /// The name of the agent application (e.g., "GitHub").
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// The Responses API endpoint for the hosted agent.
    /// Format: https://{resource}.services.ai.azure.com/api/projects/{project}/applications/{app}/protocols/openai/responses
    /// </summary>
    public string ResponsesApiEndpoint { get; set; } = string.Empty;

    /// <summary>
    /// The API version to use (e.g., "2025-11-15-preview").
    /// </summary>
    public string ApiVersion { get; set; } = "2025-11-15-preview";

    /// <summary>
    /// Whether this hosted agent is enabled.
    /// </summary>
    public bool Enabled => !string.IsNullOrWhiteSpace(ResponsesApiEndpoint);
}
