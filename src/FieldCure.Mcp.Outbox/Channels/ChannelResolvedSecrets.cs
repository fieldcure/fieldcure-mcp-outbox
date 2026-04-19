namespace FieldCure.Mcp.Outbox.Channels;

public sealed class ChannelResolvedSecrets
{
    public string? BotToken { get; init; }
    public string? ApiHash { get; init; }
    public string? Password { get; init; }
    public string? ApiKey { get; init; }
    public string? ClientSecret { get; init; }
    public string? WebhookUrl { get; init; }
}
