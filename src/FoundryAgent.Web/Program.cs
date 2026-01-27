using FoundryAgent.Web.Models;
using FoundryAgent.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

// Configure Foundry options
builder.Services.Configure<FoundryOptions>(
    builder.Configuration.GetSection("Foundry"));

// Add OpenTelemetry tracing for observability
builder.Services.AddFoundryTelemetry(builder.Configuration);

// Register the modern agent service (uses AIProjectClient)
builder.Services.AddSingleton<ModernAgentService>();

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
