using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Azure.AI.Projects;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ChatResponse = FoundryAgent.Web.Models.ChatResponse;
using FoundryOptions = FoundryAgent.Web.Models.FoundryOptions;

namespace FoundryAgent.Web.Services;

/// <summary>
/// Modern Agent Service using AIProjectClient from Microsoft Agent Framework.
/// This service demonstrates the recommended approach for building Azure AI Foundry agents.
/// 
/// This is used for the self-hosted "GitHub Agent" that runs in the application with custom C# tools.
/// 
/// Features showcased:
/// - AIProjectClient for agent management (recommended over PersistentAgentsClient)
/// - Function Tools (custom functions the agent can call)
/// - OpenTelemetry instrumentation for observability
/// - Streaming responses
/// - Multi-turn conversations
/// </summary>
public class ModernAgentService
{
    private readonly AIProjectClient _projectClient;
    private readonly FoundryOptions _options;
    private readonly ILogger<ModernAgentService> _logger;
    private readonly ActivitySource _activitySource;

    // Define custom function tools that the agent can call
    private readonly List<AITool> _tools;

    public ModernAgentService(
        IOptions<FoundryOptions> options,
        ILogger<ModernAgentService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _activitySource = new ActivitySource("FoundryAgent.Web", "1.0.0");

        if (string.IsNullOrWhiteSpace(_options.ProjectEndpoint))
            throw new InvalidOperationException("Foundry:ProjectEndpoint is required.");
        if (string.IsNullOrWhiteSpace(_options.DeploymentName))
            throw new InvalidOperationException("Foundry:DeploymentName is required.");

        // Create AIProjectClient - the modern, recommended approach
        var credential = _options.UseDefaultAzureCredential
            ? new DefaultAzureCredential()
            : new AzureCliCredential() as Azure.Core.TokenCredential;

        _projectClient = new AIProjectClient(
            endpoint: new Uri(_options.ProjectEndpoint),
            tokenProvider: credential);

        // Define function tools using AIFunctionFactory
        _tools = CreateFunctionTools();

        _logger.LogInformation("ModernAgentService initialized with endpoint: {Endpoint}", _options.ProjectEndpoint);
    }

    /// <summary>
    /// Creates function tools that demonstrate various agent capabilities.
    /// </summary>
    private List<AITool> CreateFunctionTools()
    {
        return
        [
            // Weather function - demonstrates basic function calling
            AIFunctionFactory.Create(GetWeather),
            
            // Calculator function - demonstrates structured input
            AIFunctionFactory.Create(Calculate),
            
            // Time function - demonstrates simple stateless functions
            AIFunctionFactory.Create(GetCurrentTime),
            
            // Search products function - demonstrates business logic integration
            AIFunctionFactory.Create(SearchProducts),
        ];
    }

    #region Function Tool Implementations

    [Description("Get the current weather for a specified location.")]
    private static string GetWeather(
        [Description("The city and country, e.g., 'Seattle, USA' or 'London, UK'")] string location)
    {
        // In a real app, this would call a weather API
        var conditions = new[] { "sunny", "cloudy", "rainy", "partly cloudy", "windy" };
        var temps = new[] { 15, 18, 22, 25, 28, 30 };
        var random = new Random();

        return JsonSerializer.Serialize(new
        {
            location,
            temperature = temps[random.Next(temps.Length)],
            unit = "celsius",
            condition = conditions[random.Next(conditions.Length)],
            humidity = random.Next(40, 90),
            timestamp = DateTime.UtcNow
        });
    }

    [Description("Perform a mathematical calculation. Supports basic arithmetic operations.")]
    private static string Calculate(
        [Description("The mathematical expression to evaluate, e.g., '2 + 2', '10 * 5', '100 / 4'")] string expression)
    {
        try
        {
            // Simple expression parser for demo purposes
            var result = EvaluateSimpleExpression(expression);
            return JsonSerializer.Serialize(new
            {
                expression,
                result,
                success = true
            });
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                expression,
                error = ex.Message,
                success = false
            });
        }
    }

    [Description("Get the current date and time in various formats.")]
    private static string GetCurrentTime(
        [Description("The timezone to use, e.g., 'UTC', 'PST', 'EST'. Defaults to UTC.")] string? timezone = "UTC")
    {
        var now = DateTime.UtcNow;
        return JsonSerializer.Serialize(new
        {
            utc = now.ToString("o"),
            local = now.ToLocalTime().ToString("o"),
            date = now.ToString("yyyy-MM-dd"),
            time = now.ToString("HH:mm:ss"),
            dayOfWeek = now.DayOfWeek.ToString(),
            timezone = timezone ?? "UTC"
        });
    }

    [Description("Search for products in the catalog.")]
    private static string SearchProducts(
        [Description("The search query for products")] string query,
        [Description("Maximum number of results to return")] int maxResults = 5)
    {
        // Demo product catalog
        var products = new[]
        {
            new { id = "1", name = "Azure SDK for .NET", category = "Software", price = 0.0 },
            new { id = "2", name = "Visual Studio Enterprise", category = "IDE", price = 250.0 },
            new { id = "3", name = "GitHub Copilot", category = "AI", price = 19.0 },
            new { id = "4", name = "Azure DevOps", category = "DevOps", price = 30.0 },
            new { id = "5", name = "Power BI Pro", category = "Analytics", price = 10.0 }
        };

        var results = products
            .Where(p => p.name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                       p.category.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(maxResults)
            .ToList();

        return JsonSerializer.Serialize(new
        {
            query,
            resultCount = results.Count,
            products = results
        });
    }

    private static double EvaluateSimpleExpression(string expression)
    {
        // Very simple expression evaluator for demo
        expression = expression.Replace(" ", "");

        if (expression.Contains('+'))
        {
            var parts = expression.Split('+');
            return double.Parse(parts[0]) + double.Parse(parts[1]);
        }
        if (expression.Contains('-'))
        {
            var parts = expression.Split('-');
            return double.Parse(parts[0]) - double.Parse(parts[1]);
        }
        if (expression.Contains('*'))
        {
            var parts = expression.Split('*');
            return double.Parse(parts[0]) * double.Parse(parts[1]);
        }
        if (expression.Contains('/'))
        {
            var parts = expression.Split('/');
            return double.Parse(parts[0]) / double.Parse(parts[1]);
        }

        return double.Parse(expression);
    }

    #endregion

    /// <summary>
    /// Run a chat interaction with the agent using the modern AIProjectClient approach.
    /// </summary>
    public async Task<ChatResponse> RunAsync(
        string input,
        string? threadId = null,
        string? agentType = null,
        CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("AgentRun");
        activity?.SetTag("agent.type", agentType ?? "default");
        activity?.SetTag("input.length", input.Length);

        try
        {
            // Create the agent with function tools
            var agent = await _projectClient.CreateAIAgentAsync(
                name: "FoundryDemoAgent",
                model: _options.DeploymentName,
                instructions: _options.Instructions ?? GetDefaultInstructions(agentType),
                tools: _tools,
                cancellationToken: cancellationToken);

            _logger.LogInformation("Created agent: {AgentName}", agent.Name);
            activity?.SetTag("agent.name", agent.Name);

            // Run the agent with the user input
            var response = await agent.RunAsync(input, cancellationToken: cancellationToken);

            // Get the response text
            var responseText = response?.ToString() ?? string.Empty;

            activity?.SetTag("response.length", responseText.Length);

            return new ChatResponse
            {
                Response = responseText,
                AgentId = agent.Name,
                TraceId = activity?.TraceId.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error running agent");
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Run a streaming chat interaction with the agent.
    /// </summary>
    public async IAsyncEnumerable<string> RunStreamingAsync(
        string input,
        string? threadId = null,
        string? agentType = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("AgentRunStreaming");
        activity?.SetTag("agent.type", agentType ?? "default");
        activity?.SetTag("streaming", true);

        // Create the agent
        var agent = await _projectClient.CreateAIAgentAsync(
            name: "FoundryDemoAgent",
            model: _options.DeploymentName,
            instructions: _options.Instructions ?? GetDefaultInstructions(agentType),
            tools: _tools,
            cancellationToken: cancellationToken);

        _logger.LogInformation("Created streaming agent: {AgentName}", agent.Name);

        // Stream the response
        await foreach (var update in agent.RunStreamingAsync(input, cancellationToken: cancellationToken))
        {
            if (update != null)
            {
                yield return update.ToString() ?? string.Empty;
            }
        }
    }

    private string GetDefaultInstructions(string? agentType)
    {
        return agentType?.ToLowerInvariant() switch
        {
            "code-interpreter" => """
                You are a helpful coding assistant with access to a code interpreter.
                When asked to write code, execute it and show the results.
                You can create charts, process data, and perform calculations.
                """,
            "bing-search" => """
                You are a helpful research assistant with access to Bing web search.
                When asked about current events, news, or topics that require up-to-date information,
                use the Bing search tool to find accurate and recent information.
                Always cite your sources.
                """,
            "ai-search" => """
                You are a helpful enterprise assistant with access to the organization's knowledge base.
                Use the Azure AI Search tool to find relevant documents and information.
                Provide accurate answers based on the search results and cite the source documents.
                """,
            _ => """
                You are a helpful AI assistant powered by Azure AI Foundry.
                You have access to several tools:
                - GetWeather: Get current weather for any location
                - Calculate: Perform mathematical calculations
                - GetCurrentTime: Get the current date and time
                - SearchProducts: Search the product catalog
                
                Use these tools when appropriate to help answer user questions.
                Be concise and helpful in your responses.
                """
        };
    }

    /// <summary>
    /// Get information about available agent capabilities.
    /// </summary>
    public object GetCapabilities()
    {
        return new
        {
            agentTypes = new[]
            {
                new { id = "default", name = "Default Agent", description = "General-purpose agent with function tools" },
                new { id = "code-interpreter", name = "Code Interpreter", description = "Agent that can execute Python code" },
                new { id = "bing-search", name = "Bing Search", description = "Agent with web search capabilities" },
                new { id = "ai-search", name = "AI Search", description = "Agent connected to Azure AI Search" }
            },
            tools = _tools.Select(t => new
            {
                name = t switch
                {
                    AIFunction f => f.Name,
                    _ => "unknown"
                },
                description = t switch
                {
                    AIFunction f => f.Description,
                    _ => "Unknown tool"
                }
            }),
            features = new[]
            {
                "Function Tools",
                "OpenTelemetry Tracing",
                "Streaming Responses",
                "Multi-turn Conversations"
            }
        };
    }
}
