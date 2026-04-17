using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Channels;

/// <summary>
/// Creates channel instances from stored metadata (including credentials).
/// All secrets are stored in channels.json alongside channel configuration.
/// </summary>
public static class ChannelFactory
{
    public static IChannel Create(
        ChannelMetadata metadata,
        IHttpClientFactory httpClientFactory)
    {
        return metadata.Type switch
        {
            "slack" => CreateSlack(metadata, httpClientFactory),
            "telegram" => CreateTelegram(metadata),
            "smtp" => CreateSmtp(metadata),
            "kakaotalk" => CreateKakaoTalk(metadata, httpClientFactory),
            "microsoft" => CreateMicrosoft(metadata, httpClientFactory),
            "discord" => CreateDiscord(metadata, httpClientFactory),
            _ => throw new ArgumentException($"Unknown channel type: {metadata.Type}"),
        };
    }

    static SlackChannel CreateSlack(ChannelMetadata metadata, IHttpClientFactory httpClientFactory)
    {
        var botToken = metadata.Token
            ?? throw new InvalidOperationException($"Bot token not found for channel '{metadata.Id}'.");

        return new SlackChannel(
            metadata.Id,
            metadata.Name,
            metadata.DefaultChannel ?? metadata.Name,
            botToken,
            httpClientFactory.CreateClient());
    }

    static TelegramChannel CreateTelegram(ChannelMetadata metadata)
    {
        var apiId = metadata.ApiId
            ?? throw new InvalidOperationException($"API ID not found for channel '{metadata.Id}'.");
        var apiHash = metadata.ApiHash
            ?? throw new InvalidOperationException($"API hash not found for channel '{metadata.Id}'.");
        var phone = metadata.Phone
            ?? throw new InvalidOperationException($"Phone number not found for channel '{metadata.Id}'.");

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox", "sessions");

        var sessionPath = Path.Combine(dataDir, $"{metadata.Id}.session");

        return new TelegramChannel(metadata.Id, metadata.Name, apiId, apiHash, phone, sessionPath);
    }

    static SmtpChannel CreateSmtp(ChannelMetadata metadata)
    {
        var password = metadata.Password
            ?? throw new InvalidOperationException($"Password not found for channel '{metadata.Id}'.");

        var from = metadata.From
            ?? throw new InvalidOperationException($"'from' address not set for channel '{metadata.Id}'.");

        var username = metadata.Provider == "naver" && from.Contains('@')
            ? from[..from.IndexOf('@')]
            : from;

        return new SmtpChannel(
            metadata.Id,
            metadata.Name,
            from,
            metadata.Host ?? throw new InvalidOperationException($"SMTP host not set for channel '{metadata.Id}'."),
            metadata.Port ?? 587,
            metadata.Tls ?? true,
            username,
            password);
    }

    static KakaoTalkChannel CreateKakaoTalk(ChannelMetadata metadata, IHttpClientFactory httpClientFactory)
    {
        var apiKey = metadata.ApiKey
            ?? throw new InvalidOperationException($"API key not found for channel '{metadata.Id}'.");

        var tokenPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox", "tokens", $"{metadata.Id}.json");

        return new KakaoTalkChannel(
            metadata.Id,
            metadata.Name,
            apiKey,
            metadata.ClientSecret,
            tokenPath,
            httpClientFactory.CreateClient());
    }

    static MicrosoftChannel CreateMicrosoft(ChannelMetadata metadata, IHttpClientFactory httpClientFactory)
    {
        var clientId = metadata.ClientId
            ?? throw new InvalidOperationException($"Client ID not found for channel '{metadata.Id}'.");

        var clientSecret = metadata.ClientSecret
            ?? throw new InvalidOperationException($"Client secret not found for channel '{metadata.Id}'.");

        var tokenPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox", "tokens", $"{metadata.Id}.json");

        return new MicrosoftChannel(
            metadata.Id,
            metadata.Name,
            clientId,
            clientSecret,
            tokenPath,
            httpClientFactory.CreateClient());
    }

    static DiscordChannel CreateDiscord(ChannelMetadata metadata, IHttpClientFactory httpClientFactory)
    {
        var webhookUrl = metadata.WebhookUrl
            ?? throw new InvalidOperationException($"Webhook URL not found for channel '{metadata.Id}'.");

        return new DiscordChannel(
            metadata.Id,
            metadata.Name,
            webhookUrl,
            httpClientFactory.CreateClient());
    }
}
