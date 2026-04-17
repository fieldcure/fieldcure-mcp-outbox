using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Setup;

/// <summary>
/// Interactive setup flow for adding a Telegram channel.
/// </summary>
public static class TelegramSetup
{
    /// <summary>
    /// Prompts for API credentials, authenticates with Telegram, and registers the channel.
    /// </summary>
    /// <param name="store">The channel store for persistence.</param>
    /// <param name="credentials">The credential manager for storing API credentials.</param>
    /// <param name="name">Optional display name override.</param>
    public static async Task RunAsync(ChannelStore store, string? name)
    {
        ConsoleHelper.PrintHeader("Add Telegram Channel");

        var apiId = ConsoleHelper.ReadLine("API ID");
        if (string.IsNullOrWhiteSpace(apiId))
        {
            ConsoleHelper.PrintError("API ID is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        var apiHash = ConsoleHelper.ReadMasked("API Hash");
        if (string.IsNullOrWhiteSpace(apiHash))
        {
            ConsoleHelper.PrintError("API Hash is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        var phone = ConsoleHelper.ReadLine("Phone (e.g. 8210xxxxxxxx)");
        if (string.IsNullOrWhiteSpace(phone))
        {
            ConsoleHelper.PrintError("Phone number is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        // Determine channel ID
        var existingChannels = await store.LoadAsync();
        var telegramCount = existingChannels.Count(c => c.Type == "telegram");
        var id = $"telegram_{telegramCount + 1}";
        var displayName = name ?? "Telegram";

        // Prepare session directory
        var sessionsDir = Path.Combine(store.DataDirectory, "sessions");
        Directory.CreateDirectory(sessionsDir);
        var sessionPath = Path.Combine(sessionsDir, $"{id}.session");

        // Authenticate with Telegram
        Console.WriteLine();
        Console.WriteLine("Connecting to Telegram...");

        // Suppress WTelegramClient verbose logging in CLI mode
        WTelegram.Helpers.Log = (_, _) => { };

        string? verificationCode = null;
        string? twoFactorPassword = null;

        using var client = new WTelegram.Client(what => what switch
        {
            "api_id" => apiId,
            "api_hash" => apiHash,
            "phone_number" => phone,
            "verification_code" => verificationCode ??= PromptVerificationCode(),
            "password" => twoFactorPassword ??= PromptTwoFactorPassword(),
            "session_pathname" => sessionPath,
            _ => null,
        });

        await client.LoginUserIfNeeded();

        ConsoleHelper.PrintSuccess("Session saved.");

        // Mask phone for storage
        var maskedPhone = phone.Length > 4
            ? phone[..^4] + "****"
            : phone;

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "telegram",
            Name = displayName,
            ApiId = apiId,
            ApiHash = apiHash,
            Phone = phone,
        });

        ConsoleHelper.PrintSuccess($"Channel '{id}' added.");
        ConsoleHelper.WaitForKey();
    }

    /// <summary>
    /// Prompts the user for the SMS verification code.
    /// </summary>
    static string PromptVerificationCode()
    {
        Console.WriteLine("Verification code sent via SMS.");
        return ConsoleHelper.ReadLine("Code");
    }

    /// <summary>
    /// Prompts the user for the two-factor authentication password.
    /// </summary>
    static string PromptTwoFactorPassword()
    {
        return ConsoleHelper.ReadMasked("Two-factor password");
    }
}
