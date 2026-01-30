using FoundryAgent.Web.Models;
using FoundryAgent.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure LocalHostedAgent options (GitHub Agent - self-hosted with SDK)
builder.Services.Configure<FoundryOptions>(
    builder.Configuration.GetSection("LocalHostedAgent"));

// Configure FoundryHostedAgent options (Travel Agent - hosted in Foundry portal)
builder.Services.Configure<HostedAgentOptions>(
    builder.Configuration.GetSection("FoundryHostedAgent"));

// Add OpenTelemetry tracing for observability
builder.Services.AddFoundryTelemetry(builder.Configuration);

// Register the modern agent service (uses AIProjectClient - custom C# tools)
builder.Services.AddSingleton<ModernAgentService>();

// Register the MCP agent service (uses Model Context Protocol servers)
builder.Services.AddSingleton<McpAgentService>();

// Register the hosted agent service (uses Responses API - portal-configured tools)
builder.Services.AddHttpClient<HostedAgentService>();

// Legacy services (kept for backward compatibility)
builder.Services.AddHttpClient<CopilotMcpClient>();
builder.Services.AddSingleton<McpGitHubService>();

// Health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

// Log startup information
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Foundry Agent Demo starting...");
logger.LogInformation("Environment: {Environment}", app.Environment.EnvironmentName);

app.Run();
