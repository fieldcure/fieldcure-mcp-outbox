using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Setup;

public static class SlackSetup
{
    public static async Task RunAsync(ChannelStore store, CredentialManager credentials, string? name)
    {
        ConsoleHelper.PrintHeader("Add Slack Channel");

        var channelName = name ?? ConsoleHelper.ReadLine("Channel name");
        if (string.IsNullOrWhiteSpace(channelName))
        {
            ConsoleHelper.PrintError("Channel name is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        var botToken = ConsoleHelper.ReadMasked("Bot Token (xoxb-...)");
        if (string.IsNullOrWhiteSpace(botToken))
        {
            ConsoleHelper.PrintError("Bot Token is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        var id = $"slack_{channelName}";

        credentials.Store($"FieldCure.Outbox:{id}", botToken);

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "slack",
            Name = channelName,
            DefaultChannel = channelName,
        });

        Console.WriteLine();
        ConsoleHelper.PrintSuccess($"Channel '{id}' added.");
        Console.WriteLine();
        Console.WriteLine("Note: You must invite the bot to the channel before sending messages.");
        Console.WriteLine("      Type /invite @YourBotName in the Slack channel.");
        ConsoleHelper.WaitForKey();
    }
}
