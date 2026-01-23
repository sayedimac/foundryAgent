namespace FoundryAgent.Web.Models;

public class FoundryOptions
{
    public string ProjectEndpoint { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public bool UseDefaultAzureCredential { get; set; } = true;
    public string? Instructions { get; set; }
}
