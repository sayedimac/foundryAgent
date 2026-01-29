using FoundryAgent.Web.Models;
using FoundryAgent.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure Foundry options for SDK-based agent
builder.Services.Configure<FoundryOptions>(
    builder.Configuration.GetSection("Foundry"));

// Configure Hosted Agent options for pre-deployed agents
builder.Services.Configure<HostedAgentOptions>(
    builder.Configuration.GetSection("HostedAgent"));

// Add OpenTelemetry tracing for observability
builder.Services.AddFoundryTelemetry(builder.Configuration);

// Register the modern agent service (uses AIProjectClient - custom C# tools)
builder.Services.AddSingleton<ModernAgentService>();

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
