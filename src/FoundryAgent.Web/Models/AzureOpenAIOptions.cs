namespace FoundryAgent.Web.Models;

public class AzureOpenAIOptions
{
    public string? Endpoint { get; set; }
    public string? Deployment { get; set; }
    public string? Key { get; set; }
    public string? Instructions { get; set; }
    public bool UseAzureCliCredential { get; set; } = true;
}
