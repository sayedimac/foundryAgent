using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.OpenAI;
using OpenAI;
using FoundryAgent.Web.Models;
using FoundryAgent.Web.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection("AzureOpenAI"));
builder.Services.AddSingleton<AgentService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapPost("/api/chat", async (ChatRequest request, AgentService agent, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(request.Input))
    {
        return Results.BadRequest(new { error = "Input is required." });
    }

    var response = await agent.RunAsync(request.Input, ct);
    return Results.Ok(new ChatResponse(response));
})
.WithName("Chat")
.Produces<ChatResponse>();

app.Run();
