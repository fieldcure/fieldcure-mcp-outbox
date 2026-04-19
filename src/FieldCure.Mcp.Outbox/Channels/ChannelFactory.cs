using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Channels;

/// <summary>
/// Creates channel instances from stored metadata plus runtime-resolved secrets.
/// </summary>
public static class ChannelFactory
{
    public static IChannel Create(
        ChannelMetadata metadata,
        ChannelResolvedSecrets? secrets,
        IHttpClientFactory httpClientFactory)
    {
        return metadata.Type switch
        {
            "slack" => CreateSlack(metadata, secrets, httpClientFactory),
            "telegram" => CreateTelegram(metadata, secrets),
            "smtp" => CreateSmtp(metadata, secrets),
            "kakaotalk" => CreateKakaoTalk(metadata, secrets, httpClientFactory),
            "microsoft" => CreateMicrosoft(metadata, secrets, httpClientFactory),
            "discord" => CreateDiscord(metadata, secrets, httpClientFactory),
            _ => throw new ArgumentException($"Unknown channel type: {metadata.Type}"),
        };
    }

    static SlackChannel CreateSlack(ChannelMetadata metadata, ChannelResolvedSecrets? secrets, IHttpClientFactory httpClientFactory)
    {
        var botToken = secrets?.BotToken ?? metadata.Token
            ?? throw new InvalidOperationException($"Bot token not found for channel '{metadata.Id}'.");

        return new SlackChannel(
            metadata.Id,
            metadata.Name,
            metadata.DefaultChannel ?? metadata.Name,
            botToken,
            httpClientFactory.CreateClient());
    }

    static TelegramChannel CreateTelegram(ChannelMetadata metadata, ChannelResolvedSecrets? secrets)
    {
        var apiId = metadata.ApiId
            ?? throw new InvalidOperationException($"API ID not found for channel '{metadata.Id}'.");
        var apiHash = secrets?.ApiHash ?? metadata.ApiHash
            ?? throw new InvalidOperationException($"API hash not found for channel '{metadata.Id}'.");
        var phone = metadata.Phone
            ?? throw new InvalidOperationException($"Phone number not found for channel '{metadata.Id}'.");

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox", "sessions");

        var sessionPath = Path.Combine(dataDir, $"{metadata.Id}.session");

        return new TelegramChannel(metadata.Id, metadata.Name, apiId, apiHash, phone, sessionPath);
    }

    static SmtpChannel CreateSmtp(ChannelMetadata metadata, ChannelResolvedSecrets? secrets)
    {
        var password = secrets?.Password ?? metadata.Password
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

    static KakaoTalkChannel CreateKakaoTalk(ChannelMetadata metadata, ChannelResolvedSecrets? secrets, IHttpClientFactory httpClientFactory)
    {
        var apiKey = secrets?.ApiKey ?? metadata.ApiKey
            ?? throw new InvalidOperationException($"API key not found for channel '{metadata.Id}'.");

        var tokenPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox", "tokens", $"{metadata.Id}.json");

        return new KakaoTalkChannel(
            metadata.Id,
            metadata.Name,
            apiKey,
            secrets?.ClientSecret ?? metadata.ClientSecret,
            tokenPath,
            httpClientFactory.CreateClient());
    }

    static MicrosoftChannel CreateMicrosoft(ChannelMetadata metadata, ChannelResolvedSecrets? secrets, IHttpClientFactory httpClientFactory)
    {
        var clientId = metadata.ClientId
            ?? throw new InvalidOperationException($"Client ID not found for channel '{metadata.Id}'.");

        var clientSecret = secrets?.ClientSecret ?? metadata.ClientSecret
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

    static DiscordChannel CreateDiscord(ChannelMetadata metadata, ChannelResolvedSecrets? secrets, IHttpClientFactory httpClientFactory)
    {
        var webhookUrl = secrets?.WebhookUrl ?? metadata.WebhookUrl
            ?? throw new InvalidOperationException($"Webhook URL not found for channel '{metadata.Id}'.");

        return new DiscordChannel(
            metadata.Id,
            metadata.Name,
            webhookUrl,
            httpClientFactory.CreateClient());
    }
}
