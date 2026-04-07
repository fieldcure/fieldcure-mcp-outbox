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

    /// <summary>
    /// Runs the interactive channel setup flow for the given type.
    /// </summary>
    /// <param name="args">CLI arguments: type [--name name].</param>
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
        var credentials = new CredentialManager();

        try
        {
            switch (type)
            {
                case "slack":
                    await SlackSetup.RunAsync(store, credentials, name);
                    break;
                case "telegram":
                    await TelegramSetup.RunAsync(store, credentials, name);
                    break;
                case "gmail":
                case "naver":
                    await SmtpSetup.RunAsync(store, credentials, providerShortcut: type, name);
                    break;
                case "microsoft":
                    await MicrosoftSetup.RunAsync(store, credentials, name);
                    break;
                case "smtp":
                    await SmtpSetup.RunAsync(store, credentials, providerShortcut: null, name);
                    break;
                case "kakaotalk":
                    await KakaoTalkSetup.RunAsync(store, credentials, name);
                    break;
                case "discord":
                    await DiscordSetup.RunAsync(store, credentials, name);
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

    /// <summary>
    /// Lists all configured channels in a tabular format.
    /// </summary>
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

    /// <summary>
    /// Removes a channel by ID, cleaning up credentials and session files.
    /// </summary>
    /// <param name="args">CLI arguments: channel-id.</param>
    public static async Task<int> RunRemoveAsync(string[] args)
    {
        if (args.Length == 0)
        {
            Console.Error.WriteLine("Usage: fieldcure-mcp-outbox remove <channel-id>");
            return 1;
        }

        var channelId = args[0];
        var store = new ChannelStore();
        var credentials = new CredentialManager();

        var channel = await store.GetByIdAsync(channelId);
        if (channel == null)
        {
            Console.Error.WriteLine($"Channel not found: {channelId}");
            return 1;
        }

        // Delete credentials based on channel type
        DeleteChannelCredentials(credentials, channel);

        // Delete session/token files
        DeleteChannelFiles(store, channel);

        await store.RemoveAsync(channelId);

        Console.WriteLine($"Channel '{channelId}' removed.");
        return 0;
    }

    /// <summary>
    /// Deletes stored credentials for a channel based on its type.
    /// </summary>
    internal static void DeleteChannelCredentials(CredentialManager credentials, ChannelMetadata channel)
    {
        switch (channel.Type)
        {
            case "slack":
                credentials.Delete($"FieldCure.Outbox:{channel.Id}");
                break;
            case "telegram":
                credentials.Delete($"FieldCure.Outbox:{channel.Id}:api");
                break;
            case "smtp":
                credentials.Delete($"FieldCure.Outbox:{channel.Id}");
                break;
            case "kakaotalk":
                credentials.Delete($"FieldCure.Outbox:{channel.Id}:api_key");
                credentials.Delete($"FieldCure.Outbox:{channel.Id}:client_secret");
                break;
            case "microsoft":
                credentials.Delete($"FieldCure.Outbox:{channel.Id}:client_id");
                credentials.Delete($"FieldCure.Outbox:{channel.Id}:client_secret");
                break;
            case "discord":
                credentials.Delete($"FieldCure.Outbox:{channel.Id}");
                break;
        }
    }

    /// <summary>
    /// Deletes session or token files associated with a channel.
    /// </summary>
    internal static void DeleteChannelFiles(ChannelStore store, ChannelMetadata channel)
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
                var tokenPath = Path.Combine(store.DataDirectory, "tokens", $"{channel.Id}.json");
                if (File.Exists(tokenPath))
                    File.Delete(tokenPath);
                break;
            }
        }
    }
}
