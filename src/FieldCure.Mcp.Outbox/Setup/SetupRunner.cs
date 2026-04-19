using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Setup;

/// <summary>
/// Dispatches CLI commands for adding, listing, and removing channels.
/// </summary>
public static class SetupRunner
{
    static readonly HashSet<string> ValidTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "slack", "telegram", "gmail", "naver", "smtp", "kakaotalk", "microsoft", "discord"
    };

    public static async Task<int> RunAddAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: fieldcure-mcp-outbox add <type> [--name <name>]");
            Console.Error.WriteLine($"Types: {string.Join(", ", ValidTypes)}");
            return 1;
        }

        var type = args[0].ToLowerInvariant();
        if (!ValidTypes.Contains(type))
        {
            Console.Error.WriteLine($"Unknown channel type: {type}");
            Console.Error.WriteLine($"Valid types: {string.Join(", ", ValidTypes)}");
            return 1;
        }

        string? name = null;
        for (var i = 1; i < args.Length - 1; i++)
        {
            if (args[i] == "--name")
            {
                name = args[i + 1];
                break;
            }
        }

        var store = new ChannelStore();
        var tokenStore = new OAuthTokenStore(store.DataDirectory);

        try
        {
            switch (type)
            {
                case "slack":
                    await SlackSetup.RunAsync(store, name);
                    break;
                case "telegram":
                    await TelegramSetup.RunAsync(store, name);
                    break;
                case "gmail":
                case "naver":
                    await SmtpSetup.RunAsync(store, providerShortcut: type, name);
                    break;
                case "microsoft":
                    await MicrosoftSetup.RunAsync(store, tokenStore, name);
                    break;
                case "smtp":
                    await SmtpSetup.RunAsync(store, providerShortcut: null, name);
                    break;
                case "kakaotalk":
                    await KakaoTalkSetup.RunAsync(store, tokenStore, name);
                    break;
                case "discord":
                    await DiscordSetup.RunAsync(store, name);
                    break;
            }

            return 0;
        }
        catch (Exception ex)
        {
            ConsoleHelper.PrintError(ex.Message);
            ConsoleHelper.WaitForKey();
            return 1;
        }
    }

    public static async Task<int> RunListAsync()
    {
        var store = new ChannelStore();
        var channels = await store.LoadAsync();

        if (channels.Count == 0)
        {
            Console.WriteLine("No channels configured.");
            return 0;
        }

        Console.WriteLine($"{"ID",-30} {"Type",-12} {"Name",-20} {"Created"}");
        Console.WriteLine(new string('-', 80));

        foreach (var ch in channels.OrderBy(c => c.Id))
        {
            Console.WriteLine($"{ch.Id,-30} {ch.Type,-12} {ch.Name,-20} {ch.CreatedAt:yyyy-MM-dd HH:mm}");
        }

        return 0;
    }

    public static async Task<int> RunRemoveAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: fieldcure-mcp-outbox remove <channel-id>");
            return 1;
        }

        var channelId = args[0];
        var store = new ChannelStore();

        var channel = await store.GetByIdAsync(channelId);
        if (channel == null)
        {
            Console.Error.WriteLine($"Channel not found: {channelId}");
            return 1;
        }

        await DeleteChannelFilesAsync(store, channel);
        await store.RemoveAsync(channelId);

        Console.WriteLine($"Channel '{channelId}' removed.");
        return 0;
    }

    internal static async Task DeleteChannelFilesAsync(ChannelStore store, ChannelMetadata channel)
    {
        switch (channel.Type)
        {
            case "telegram":
            {
                var sessionPath = Path.Combine(store.DataDirectory, "sessions", $"{channel.Id}.session");
                if (File.Exists(sessionPath))
                    File.Delete(sessionPath);
                break;
            }
            case "kakaotalk":
            case "microsoft":
            {
                var tokenStore = new OAuthTokenStore(store.DataDirectory);
                await tokenStore.RemoveAsync(channel.Id);
                break;
            }
        }
    }
}
