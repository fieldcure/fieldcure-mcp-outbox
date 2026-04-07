using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Setup;

/// <summary>
/// Interactive setup flow for adding a Discord channel.
/// </summary>
public static class DiscordSetup
{
    static readonly string[] WebhookUrlPrefixes =
    [
        "https://discord.com/api/webhooks/",
        "https://discordapp.com/api/webhooks/",
    ];

    /// <summary>
    /// Prompts for a Webhook URL and registers a new Discord channel.
    /// </summary>
    /// <param name="store">The channel store for persistence.</param>
    /// <param name="credentials">The credential manager for storing the webhook URL.</param>
    /// <param name="name">Optional display name override.</param>
    public static async Task RunAsync(ChannelStore store, CredentialManager credentials, string? name)
    {
        ConsoleHelper.PrintHeader("Add Discord Channel");

        var channelName = name ?? ConsoleHelper.ReadLine("Channel name");
        if (string.IsNullOrWhiteSpace(channelName))
        {
            ConsoleHelper.PrintError("Channel name is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        var webhookUrl = ConsoleHelper.ReadMasked("Webhook URL (https://discord.com/api/webhooks/...)");
        if (string.IsNullOrWhiteSpace(webhookUrl))
        {
            ConsoleHelper.PrintError("Webhook URL is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        if (!WebhookUrlPrefixes.Any(p => webhookUrl.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            ConsoleHelper.PrintError("Webhook URL must start with https://discord.com/api/webhooks/ or https://discordapp.com/api/webhooks/");
            ConsoleHelper.WaitForKey();
            return;
        }

        var id = $"discord_{channelName}";

        credentials.Store($"FieldCure.Outbox:{id}", webhookUrl);

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "discord",
            Name = channelName,
        });

        Console.WriteLine();
        ConsoleHelper.PrintSuccess($"Channel '{id}' added.");
        ConsoleHelper.WaitForKey();
    }
}
