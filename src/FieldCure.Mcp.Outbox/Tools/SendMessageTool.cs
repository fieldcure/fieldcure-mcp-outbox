using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Outbox.Tools;

/// <summary>
/// MCP tool that sends a message through a configured channel.
/// </summary>
[McpServerToolType]
public static class SendMessageTool
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [McpServerTool(Name = "send_message", Destructive = true)]
    [Description(
        "Sends a message through a configured channel. " +
        "For SMTP channels, 'to' (recipient email) and 'subject' parameters are required. " +
        "For Slack channels, 'target_channel' can override the default channel.")]
    public static async Task<string> SendMessage(
        ChannelStore store,
        CredentialManager credentials,
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
            return JsonSerializer.Serialize(new { success = false, error = $"Channel not found: {channel}" }, JsonOptions);

        IChannel ch;
        try
        {
            ch = ChannelFactory.Create(metadata, credentials, httpClientFactory);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { success = false, error = ex.Message }, JsonOptions);
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
        }, JsonOptions);
    }
}
