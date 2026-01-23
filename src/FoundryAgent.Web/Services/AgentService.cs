using Azure.AI.Projects;
using Azure.Core;
using Azure.Identity;
using FoundryAgent.Web.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Options;

namespace FoundryAgent.Web.Services;

public class AgentService
{
    private readonly AIAgent _agent;

    public AgentService(IOptions<FoundryOptions> options)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ProjectEndpoint))
            throw new InvalidOperationException("Foundry:ProjectEndpoint is required.");
        if (string.IsNullOrWhiteSpace(opts.DeploymentName))
            throw new InvalidOperationException("Foundry:DeploymentName is required.");

        var endpoint = new Uri(opts.ProjectEndpoint);
        TokenCredential credential = opts.UseDefaultAzureCredential
            ? new DefaultAzureCredential()
            : new AzureCliCredential();

        var aiProjectClient = new AIProjectClient(endpoint, credential);

        // Create agent synchronously in constructor for simplicity
        // In production, consider lazy initialization or async factory pattern
        _agent = aiProjectClient.CreateAIAgentAsync(
            name: "FoundryAgent",
            model: opts.DeploymentName,
            instructions: opts.Instructions ?? "You are a helpful assistant."
        ).GetAwaiter().GetResult();
    }

    public async Task<string> RunAsync(string input, CancellationToken cancellationToken = default)
    {
        var response = await _agent.RunAsync(input);
        return response.Text;
    }

    public async IAsyncEnumerable<string> RunStreamingAsync(string input, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in _agent.RunStreamingAsync(input))
        {
            if (cancellationToken.IsCancellationRequested)
                yield break;

            if (!string.IsNullOrEmpty(update.Text))
            {
                yield return update.Text;
            }
        }
    }
}
