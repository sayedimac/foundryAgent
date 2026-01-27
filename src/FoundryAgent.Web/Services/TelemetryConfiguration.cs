using System.Diagnostics;
using FoundryAgent.Web.Models;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace FoundryAgent.Web.Services;

/// <summary>
/// Configures OpenTelemetry tracing for the Foundry Agent application.
/// Demonstrates observability best practices for AI agent applications.
/// </summary>
public static class TelemetryConfiguration
{
    public const string ServiceName = "FoundryAgent.Web";
    public const string ServiceVersion = "1.0.0";

    /// <summary>
    /// Activity source for custom tracing in the application.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new(ServiceName, ServiceVersion);

    /// <summary>
    /// Configures OpenTelemetry tracing for the application.
    /// </summary>
    public static IServiceCollection AddFoundryTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Foundry").Get<FoundryOptions>();

        if (options?.EnableTelemetry != true)
        {
            return services;
        }

        // Enable experimental Azure SDK observability
        AppContext.SetSwitch("Azure.Experimental.EnableActivitySource", true);

        // Enable content recording for AI traces (use with caution in production)
        AppContext.SetSwitch("Azure.Experimental.TraceGenAIMessageContent", true);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(
                    serviceName: ServiceName,
                    serviceVersion: ServiceVersion)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment"] = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development",
                    ["host.name"] = Environment.MachineName
                }))
            .WithTracing(tracing =>
            {
                tracing
                    // Add ASP.NET Core instrumentation
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    // Add HTTP client instrumentation for outgoing calls
                    .AddHttpClientInstrumentation(options =>
                    {
                        options.RecordException = true;
                    })
                    // Add our custom activity source
                    .AddSource(ServiceName)
                    // Add Microsoft Agent Framework sources
                    .AddSource("Microsoft.Agents.AI.*")
                    // Add Azure SDK sources
                    .AddSource("Azure.AI.Projects.*")
                    .AddSource("Azure.AI.Agents.Persistent.*")
                    .AddSource("Azure.AI.Inference.*");

                // Configure exporter based on configuration
                if (!string.IsNullOrEmpty(options?.OtlpEndpoint))
                {
                    tracing.AddOtlpExporter(otlpOptions =>
                    {
                        otlpOptions.Endpoint = new Uri(options.OtlpEndpoint);
                        otlpOptions.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.Grpc;
                    });
                }
                else
                {
                    // Default to console exporter for development
                    tracing.AddConsoleExporter();
                }
            });

        return services;
    }
}

/// <summary>
/// Extension methods for tracing agent operations.
/// </summary>
public static class TracingExtensions
{
    /// <summary>
    /// Starts a new activity for an agent operation.
    /// </summary>
    public static Activity? StartAgentActivity(
        this ActivitySource source,
        string operationName,
        string? agentType = null,
        string? threadId = null)
    {
        var activity = source.StartActivity(
            name: operationName,
            kind: ActivityKind.Internal);

        if (activity != null)
        {
            activity.SetTag("agent.operation", operationName);

            if (!string.IsNullOrEmpty(agentType))
            {
                activity.SetTag("agent.type", agentType);
            }

            if (!string.IsNullOrEmpty(threadId))
            {
                activity.SetTag("agent.thread_id", threadId);
            }
        }

        return activity;
    }

    /// <summary>
    /// Records tool call information on the current activity.
    /// </summary>
    public static void RecordToolCall(
        this Activity? activity,
        string toolName,
        string? arguments = null,
        string? result = null)
    {
        if (activity == null) return;

        activity.AddEvent(new ActivityEvent(
            "tool_call",
            tags: new ActivityTagsCollection
            {
                ["tool.name"] = toolName,
                ["tool.arguments"] = arguments ?? string.Empty,
                ["tool.result_length"] = result?.Length ?? 0
            }));
    }

    /// <summary>
    /// Records agent response metrics on the current activity.
    /// </summary>
    public static void RecordAgentResponse(
        this Activity? activity,
        int tokenCount,
        TimeSpan duration,
        bool success)
    {
        if (activity == null) return;

        activity.SetTag("agent.token_count", tokenCount);
        activity.SetTag("agent.duration_ms", duration.TotalMilliseconds);
        activity.SetTag("agent.success", success);
    }
}
