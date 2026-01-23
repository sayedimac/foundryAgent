using Microsoft.Bot.Builder;
using Microsoft.Bot.Schema;

namespace FoundryAgent.Web.Bots;

public class EchoBot : ActivityHandler
{
    protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
    {
        var text = turnContext.Activity.Text?.Trim();
        var replyText = string.IsNullOrEmpty(text) ? "Echo: (no text)" : $"Echo: {text}";
        await turnContext.SendActivityAsync(MessageFactory.Text(replyText), cancellationToken);
    }

    protected override Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
    {
        var welcomeText = "Hello and welcome!";
        foreach (var member in membersAdded)
        {
            if (member.Id != turnContext.Activity.Recipient.Id)
            {
                turnContext.SendActivityAsync(MessageFactory.Text(welcomeText), cancellationToken);
            }
        }
        return Task.CompletedTask;
    }
}
