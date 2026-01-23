using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using Microsoft.Extensions.Options;
using OpenAI;
using FoundryAgent.Web.Models;

namespace FoundryAgent.Web.Services;

public class AgentService
{
    private readonly AIAgent _agent;

    public AgentService(IOptions<AzureOpenAIOptions> options)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.Endpoint))
            throw new InvalidOperationException("AzureOpenAI:Endpoint is required.");
        if (string.IsNullOrWhiteSpace(opts.Deployment))
            throw new InvalidOperationException("AzureOpenAI:Deployment is required.");

        var endpoint = new Uri(opts.Endpoint);
        var credential = CreateCredential(opts);

        var chatClient = new AzureOpenAIClient(endpoint, credential)
            .GetChatClient(opts.Deployment);

        _agent = chatClient.AsAIAgent(instructions: opts.Instructions ?? "You are a helpful assistant.");
    }

    private static object CreateCredential(AzureOpenAIOptions opts)
    {
        if (!string.IsNullOrWhiteSpace(opts.Key))
        {
            return new ApiKeyCredential(opts.Key);
        }

        return new AzureCliCredential();
    }

    public Task<string> RunAsync(string input, CancellationToken cancellationToken = default)
        => _agent.RunAsync(input, cancellationToken);
}
