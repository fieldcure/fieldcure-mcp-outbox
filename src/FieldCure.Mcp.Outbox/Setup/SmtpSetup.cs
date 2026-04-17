using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Setup;

/// <summary>
/// Interactive setup flow for adding an SMTP email channel.
/// </summary>
public static class SmtpSetup
{
    static readonly (string Key, string Label, string Host)[] Providers =
    [
        ("gmail", "Gmail", "smtp.gmail.com"),
        ("naver", "Naver", "smtp.naver.com"),
    ];

    /// <summary>
    /// Prompts for SMTP settings and credentials, then registers the channel.
    /// </summary>
    /// <param name="store">The channel store for persistence.</param>
    /// <param name="credentials">The credential manager for storing the password.</param>
    /// <param name="providerShortcut">
    /// null = show provider selection menu (add smtp),
    /// "gmail"/"outlook"/etc. = skip menu and use preset directly (add gmail shortcut).
    /// </param>
    /// <param name="name">Optional display name override.</param>
    public static async Task RunAsync(
        ChannelStore store, string? providerShortcut, string? name)
    {
        ConsoleHelper.PrintHeader("Add SMTP Channel");

        string provider;
        bool isCustom;
        string displayLabel;

        if (providerShortcut != null)
        {
            // Shortcut: add gmail, add outlook, etc.
            var index = Array.FindIndex(Providers, p => p.Key == providerShortcut);
            if (index < 0)
            {
                ConsoleHelper.PrintError($"Unknown SMTP provider: {providerShortcut}");
                ConsoleHelper.WaitForKey();
                return;
            }

            provider = Providers[index].Key;
            displayLabel = Providers[index].Label;
            isCustom = false;
        }
        else
        {
            // Menu: add smtp
            Console.WriteLine("Select provider:");
            for (var i = 0; i < Providers.Length; i++)
            {
                var (_, label, host) = Providers[i];
                Console.WriteLine($"  {i + 1}. {label,-16} ({host})");
            }
            Console.WriteLine($"  {Providers.Length + 1}. Custom");
            Console.WriteLine();

            var choiceStr = ConsoleHelper.ReadLineWithDefault("Choice", "1");
            if (!int.TryParse(choiceStr, out var choice) || choice < 1 || choice > Providers.Length + 1)
                choice = 1;

            isCustom = choice == Providers.Length + 1;
            provider = isCustom ? "smtp" : Providers[choice - 1].Key;
            displayLabel = isCustom ? "SMTP" : Providers[choice - 1].Label;
        }

        string host2, username, password, from;
        int port;
        bool tls;

        if (isCustom)
        {
            Console.WriteLine();
            host2 = ConsoleHelper.ReadLine("Host");
            if (string.IsNullOrWhiteSpace(host2))
            {
                ConsoleHelper.PrintError("Host is required.");
                ConsoleHelper.WaitForKey();
                return;
            }

            var portStr = ConsoleHelper.ReadLineWithDefault("Port", "587");
            port = int.TryParse(portStr, out var p) ? p : 587;

            var tlsInput = ConsoleHelper.ReadLineWithDefault("Use TLS (y/n)", "y");
            tls = tlsInput.StartsWith("y", StringComparison.OrdinalIgnoreCase);

            username = ConsoleHelper.ReadLine("Username");
            password = ConsoleHelper.ReadMasked("Password");
            from = username;
        }
        else
        {
            var preset = SmtpPresets.Get(provider)!;

            Console.WriteLine();
            from = ConsoleHelper.ReadLine($"{displayLabel} address");
            if (string.IsNullOrWhiteSpace(from))
            {
                ConsoleHelper.PrintError("Email address is required.");
                ConsoleHelper.WaitForKey();
                return;
            }

            if (provider == "gmail")
                Console.WriteLine("  Hint: App password format is [xxxx xxxx xxxx xxxx]");
            else if (provider == "naver")
                Console.WriteLine("  Hint: App password format is [XXXXXXXXXXXX] (12 uppercase letters and digits)");
            password = ConsoleHelper.ReadMasked("App password");
            if (string.IsNullOrWhiteSpace(password))
            {
                ConsoleHelper.PrintError("App password is required.");
                ConsoleHelper.WaitForKey();
                return;
            }

            host2 = preset.Host;
            port = preset.Port;
            tls = true;
            username = from;
        }

        // Generate channel ID
        var existingChannels = await store.LoadAsync();
        var smtpCount = existingChannels.Count(c => c.Type == "smtp" && c.Provider == provider);
        var id = $"smtp_{provider}_{smtpCount + 1}";
        var displayName = name ?? displayLabel;

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "smtp",
            Name = displayName,
            Provider = provider,
            From = from,
            Host = host2,
            Port = port,
            Tls = tls,
            Password = password,
        });

        Console.WriteLine();
        ConsoleHelper.PrintSuccess($"Channel '{id}' added.");
        ConsoleHelper.WaitForKey();
    }
}
