using FoundryAgent.Web.Bots;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services
    .AddControllers()
    .AddNewtonsoftJson();

// Bot Framework configuration
builder.Services.AddSingleton<BotFrameworkAuthentication>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    return new ConfigurationBotFrameworkAuthentication(configuration);
});

builder.Services.AddSingleton<IBotFrameworkHttpAdapter>(sp =>
{
    var auth = sp.GetRequiredService<BotFrameworkAuthentication>();
    var logger = sp.GetRequiredService<ILogger<CloudAdapter>>();
    var adapter = new CloudAdapter(auth, logger);
    adapter.OnTurnError = async (turnContext, exception) =>
    {
        logger.LogError(exception, "Unhandled error");
        await turnContext.SendActivityAsync("The bot encountered an error or bug.");
        await turnContext.SendActivityAsync("To continue to run this bot, please fix the bot source code.");
    };
    return adapter;
});

builder.Services.AddSingleton<IBot, EchoBot>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
