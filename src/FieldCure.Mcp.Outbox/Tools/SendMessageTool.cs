using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;
using FieldCure.Mcp.Outbox.Credentials;
using FieldCure.Mcp.Outbox.Interaction;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Outbox.Tools;

[McpServerToolType]
public static class SendMessageTool
{
    /// <summary>
    /// Sends a message through an existing configured channel, resolving any
    /// missing secrets through environment variables or MCP elicitation first.
    /// </summary>
    /// <param name="server">The active MCP server instance.</param>
    /// <param name="store">The channel metadata store.</param>
    /// <param name="tokenStore">The OAuth token store used by browser-based channels.</param>
    /// <param name="resolver">The shared secret resolver and in-memory cache.</param>
    /// <param name="httpClientFactory">Factory for HTTP-based channels.</param>
    /// <param name="channel">Channel ID to send through.</param>
    /// <param name="message">Message body text.</param>
    /// <param name="to">Recipient email address for SMTP channels.</param>
    /// <param name="subject">Email subject for SMTP channels.</param>
    /// <param name="target_channel">Optional target override such as a Slack channel name.</param>
    /// <param name="cancellationToken">Cancellation token for the send operation.</param>
    /// <returns>A JSON payload describing success or failure.</returns>
    [McpServerTool(Name = "send_message", Destructive = true)]
    [Description(
        "Sends a message through a configured channel. " +
        "For SMTP channels, 'to' (recipient email) and 'subject' parameters are required. " +
        "For Slack channels, 'target_channel' can override the default channel.")]
    public static async Task<string> SendMessage(
        McpServer server,
        ChannelStore store,
        OAuthTokenStore tokenStore,
        OutboxSecretResolver resolver,
        IHttpClientFactory httpClientFactory,
        [Description("Channel ID to send through (e.g. 'slack_dev-alerts', 'smtp_gmail_1')")]
        string channel,
        [Description("Message body text")]
        string message,
        [Description("Recipient email address (required for SMTP channels)")]
        string? to = null,
        [Description("Email subject (required for SMTP channels)")]
        string? subject = null,
        [Description("Target channel override (e.g. Slack channel name)")]
        string? target_channel = null,
        CancellationToken cancellationToken = default)
    {
        var metadata = await store.GetByIdAsync(channel);
        if (metadata == null)
            return JsonSerializer.Serialize(new { success = false, error = $"Channel not found: {channel}" }, McpJson.Tool);

        var gate = new McpServerElicitGate(server);
        var resolved = await ResolveSecretsAsync(gate, resolver, metadata, cancellationToken);
        if (resolved.error is not null)
            return JsonSerializer.Serialize(new { success = false, error = resolved.error }, McpJson.Tool);

        IChannel ch;
        try
        {
            ch = ChannelFactory.Create(metadata, resolved.secrets, httpClientFactory, tokenStore);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, McpJson.Tool);
        }

        var request = new SendRequest
        {
            Message = message,
            To = to,
            Subject = subject,
            TargetChannel = target_channel,
        };

        var result = await ch.SendAsync(request, cancellationToken);

        return JsonSerializer.Serialize(new
        {
            success = result.Success,
            error = result.Error,
            error_code = result.ErrorCode,
        }, McpJson.Tool);
    }

    /// <summary>
    /// Resolves all secrets needed for the specified channel type before the
    /// concrete channel implementation is constructed.
    /// </summary>
    /// <param name="gate">The elicitation gate for the active MCP request.</param>
    /// <param name="resolver">The shared secret resolver and in-memory cache.</param>
    /// <param name="metadata">The stored channel metadata.</param>
    /// <param name="ct">Cancellation token for the resolution flow.</param>
    /// <returns>The resolved secrets along with any user-facing error message.</returns>
    static async Task<(ChannelResolvedSecrets? secrets, string? error)> ResolveSecretsAsync(
        IElicitGate gate,
        OutboxSecretResolver resolver,
        ChannelMetadata metadata,
        CancellationToken ct)
    {
        switch (metadata.Type)
        {
            case "slack":
                return await ResolveSingleSecretAsync(
                    gate,
                    resolver,
                    metadata,
                    "BOT_TOKEN",
                    "bot_token",
                    "Bot Token",
                    "Slack bot token (xoxb-...)",
                    metadata.Token,
                    static value => new ChannelResolvedSecrets { BotToken = value },
                    ct);

            case "telegram":
                return await ResolveSingleSecretAsync(
                    gate,
                    resolver,
                    metadata,
                    "API_HASH",
                    "api_hash",
                    "API Hash",
                    "Telegram API Hash",
                    metadata.ApiHash,
                    static value => new ChannelResolvedSecrets { ApiHash = value },
                    ct);

            case "smtp":
                return await ResolveSingleSecretAsync(
                    gate,
                    resolver,
                    metadata,
                    "PASSWORD",
                    "password",
                    "Password",
                    "SMTP password or app password",
                    metadata.Password,
                    static value => new ChannelResolvedSecrets { Password = value },
                    ct);

            case "discord":
                return await ResolveSingleSecretAsync(
                    gate,
                    resolver,
                    metadata,
                    "WEBHOOK_URL",
                    "webhook_url",
                    "Webhook URL",
                    "Discord webhook URL",
                    metadata.WebhookUrl,
                    static value => new ChannelResolvedSecrets { WebhookUrl = value },
                    ct);

            case "microsoft":
                return await ResolveSingleSecretAsync(
                    gate,
                    resolver,
                    metadata,
                    "CLIENT_SECRET",
                    "client_secret",
                    "Client Secret",
                    "Azure Entra ID client secret",
                    metadata.ClientSecret,
                    static value => new ChannelResolvedSecrets { ClientSecret = value },
                    ct);

            case "kakaotalk":
            {
                var apiKeyEnv = OutboxSecretResolver.BuildEnvVarName(metadata.Id, "API_KEY");
                var clientSecretEnv = OutboxSecretResolver.BuildEnvVarName(metadata.Id, "CLIENT_SECRET");
                var values = await resolver.ResolveFieldsAsync(gate,
                [
                    new SecretFieldRequest(
                        "api_key",
                        apiKeyEnv,
                        "REST API Key",
                        "Kakao REST API Key",
                        $"Enter the Kakao REST API key for channel '{metadata.Id}'.",
                        LegacyValue: metadata.ApiKey),
                    new SecretFieldRequest(
                        "client_secret",
                        clientSecretEnv,
                        "Client Secret",
                        "Kakao client secret (optional)",
                        $"Enter the Kakao client secret for channel '{metadata.Id}' if your app uses one.",
                        Required: false,
                        LegacyValue: metadata.ClientSecret),
                ], ct);

                return values is null
                    ? (null, resolver.BuildSoftFailMessage(apiKeyEnv, clientSecretEnv))
                    : (new ChannelResolvedSecrets
                    {
                        ApiKey = values["api_key"],
                        ClientSecret = values.GetValueOrDefault("client_secret"),
                    }, null);
            }

            default:
                return (new ChannelResolvedSecrets(), null);
        }
    }

    /// <summary>
    /// Resolves a single secret field for channel types that only need one
    /// credential value at send time.
    /// </summary>
    /// <param name="gate">The elicitation gate for the active MCP request.</param>
    /// <param name="resolver">The shared secret resolver and in-memory cache.</param>
    /// <param name="metadata">The stored channel metadata.</param>
    /// <param name="envSuffix">The environment variable suffix for the secret.</param>
    /// <param name="fieldName">The logical field name used in elicitation payloads.</param>
    /// <param name="title">Short UI label shown to the user.</param>
    /// <param name="description">Longer field description shown to the user.</param>
    /// <param name="legacyValue">Legacy plaintext value already stored in the channel metadata.</param>
    /// <param name="projector">Factory that projects the resolved string into a channel secret object.</param>
    /// <param name="ct">Cancellation token for the resolution flow.</param>
    /// <returns>The resolved secret projection along with any user-facing error message.</returns>
    static async Task<(ChannelResolvedSecrets? secrets, string? error)> ResolveSingleSecretAsync(
        IElicitGate gate,
        OutboxSecretResolver resolver,
        ChannelMetadata metadata,
        string envSuffix,
        string fieldName,
        string title,
        string description,
        string? legacyValue,
        Func<string, ChannelResolvedSecrets> projector,
        CancellationToken ct)
    {
        var envVar = OutboxSecretResolver.BuildEnvVarName(metadata.Id, envSuffix);
        var values = await resolver.ResolveFieldsAsync(gate,
        [
            new SecretFieldRequest(
                fieldName,
                envVar,
                title,
                description,
                $"Enter the {title.ToLowerInvariant()} for channel '{metadata.Id}'.",
                LegacyValue: legacyValue),
        ], ct);

        return values is null
            ? (null, resolver.BuildSoftFailMessage(envVar))
            : (projector(values[fieldName]), null);
    }
}
