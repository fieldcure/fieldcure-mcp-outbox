using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Configuration;
using FieldCure.Mcp.Outbox.Setup;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Outbox.Tools;

/// <summary>
/// MCP tool that removes a configured messaging channel and its credentials.
/// </summary>
[McpServerToolType]
public static class RemoveChannelTool
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [McpServerTool(Name = "remove_channel", Destructive = true)]
    [Description("Removes a configured messaging channel and its stored credentials.")]
    public static async Task<string> RemoveChannel(
        ChannelStore store,
        CredentialManager credentials,
        [Description("Channel ID to remove (e.g. 'slack_dev-alerts')")]
        string channel,
        CancellationToken cancellationToken)
    {
        var metadata = await store.GetByIdAsync(channel);
        if (metadata == null)
            return JsonSerializer.Serialize(new { status = "error", error = $"Channel not found: {channel}" }, JsonOptions);

        // Delete credentials and files
        SetupRunner.DeleteChannelCredentials(credentials, metadata);
        SetupRunner.DeleteChannelFiles(store, metadata);

        await store.RemoveAsync(channel);

        return JsonSerializer.Serialize(new { status = "ok", removed = channel }, JsonOptions);
    }
}
