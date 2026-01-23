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

// Register AgentService as singleton
builder.Services.AddSingleton<AgentService>();

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
