using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Channels;

/// <summary>
/// Creates channel instances from stored metadata and credentials.
/// </summary>
public static class ChannelFactory
{
    /// <summary>
    /// Creates an <see cref="IChannel"/> instance for the given channel metadata.
    /// </summary>
    /// <param name="metadata">The channel metadata describing the channel type and configuration.</param>
    /// <param name="credentials">The credential manager for retrieving stored secrets.</param>
    /// <param name="httpClientFactory">The HTTP client factory for channels that require HTTP access.</param>
    public static IChannel Create(
        ChannelMetadata metadata,
        CredentialManager credentials,
        IHttpClientFactory httpClientFactory)
    {
        return metadata.Type switch
        {
            "slack" => CreateSlack(metadata, credentials, httpClientFactory),
            "telegram" => CreateTelegram(metadata, credentials),
            "smtp" => CreateSmtp(metadata, credentials),
            "kakaotalk" => CreateKakaoTalk(metadata, credentials, httpClientFactory),
            _ => throw new ArgumentException($"Unknown channel type: {metadata.Type}"),
        };
    }

    /// <summary>
    /// Creates a Slack channel from metadata and credentials.
    /// </summary>
    static SlackChannel CreateSlack(ChannelMetadata metadata, CredentialManager credentials,
        IHttpClientFactory httpClientFactory)
    {
        var botToken = credentials.Retrieve($"FieldCure.Outbox:{metadata.Id}")
            ?? throw new InvalidOperationException($"Bot token not found for channel '{metadata.Id}'.");

        return new SlackChannel(
            metadata.Id,
            metadata.Name,
            metadata.DefaultChannel ?? metadata.Name,
            botToken,
            httpClientFactory.CreateClient());
    }

    /// <summary>
    /// Creates a Telegram channel from metadata and credentials.
    /// </summary>
    static TelegramChannel CreateTelegram(ChannelMetadata metadata, CredentialManager credentials)
    {
        var apiCredential = credentials.Retrieve($"FieldCure.Outbox:{metadata.Id}:api")
            ?? throw new InvalidOperationException($"API credentials not found for channel '{metadata.Id}'.");

        var parts = apiCredential.Split(':', 2);
        if (parts.Length != 2)
            throw new InvalidOperationException($"Invalid API credential format for channel '{metadata.Id}'.");

        var phone = credentials.Retrieve($"FieldCure.Outbox:{metadata.Id}:phone")
            ?? throw new InvalidOperationException($"Phone number not found for channel '{metadata.Id}'.");

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox", "sessions");

        var sessionPath = Path.Combine(dataDir, $"{metadata.Id}.session");

        return new TelegramChannel(metadata.Id, metadata.Name, parts[0], parts[1], phone, sessionPath);
    }

    /// <summary>
    /// Creates an SMTP channel from metadata and credentials.
    /// </summary>
    static SmtpChannel CreateSmtp(ChannelMetadata metadata, CredentialManager credentials)
    {
        var password = credentials.Retrieve($"FieldCure.Outbox:{metadata.Id}")
            ?? throw new InvalidOperationException($"Password not found for channel '{metadata.Id}'.");

        return new SmtpChannel(
            metadata.Id,
            metadata.Name,
            metadata.From ?? throw new InvalidOperationException($"'from' address not set for channel '{metadata.Id}'."),
            metadata.Host ?? throw new InvalidOperationException($"SMTP host not set for channel '{metadata.Id}'."),
            metadata.Port ?? 587,
            metadata.Tls ?? true,
            metadata.From,
            password);
    }

    /// <summary>
    /// Creates a KakaoTalk channel from metadata and credentials.
    /// </summary>
    static KakaoTalkChannel CreateKakaoTalk(ChannelMetadata metadata, CredentialManager credentials,
        IHttpClientFactory httpClientFactory)
    {
        var apiKey = credentials.Retrieve($"FieldCure.Outbox:{metadata.Id}:api_key")
            ?? throw new InvalidOperationException($"API key not found for channel '{metadata.Id}'.");

        var clientSecret = credentials.Retrieve($"FieldCure.Outbox:{metadata.Id}:client_secret");

        var tokenPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox", "tokens", $"{metadata.Id}.json");

        return new KakaoTalkChannel(
            metadata.Id,
            metadata.Name,
            apiKey,
            clientSecret,
            tokenPath,
            httpClientFactory.CreateClient());
    }
}
