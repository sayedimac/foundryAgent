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

// GitHub Copilot MCP client (JSON-RPC over HTTPS)
builder.Services.AddHttpClient<CopilotMcpClient>();

// Register MCP GitHub service
builder.Services.AddSingleton<McpGitHubService>();

// Register AgentService as singleton
builder.Services.AddSingleton<AgentService>();

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

app.Run();
