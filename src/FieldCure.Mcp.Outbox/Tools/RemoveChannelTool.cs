using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Configuration;
using FieldCure.Mcp.Outbox.Setup;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Outbox.Tools;

/// <summary>
/// MCP tool that removes a configured messaging channel.
/// </summary>
[McpServerToolType]
public static class RemoveChannelTool
{
    [McpServerTool(Name = "remove_channel", Destructive = true)]
    [Description("Removes a configured messaging channel and its stored data.")]
    public static async Task<string> RemoveChannel(
        ChannelStore store,
        [Description("Channel ID to remove (e.g. 'slack_dev-alerts')")]
        string channel,
        CancellationToken cancellationToken)
    {
        var metadata = await store.GetByIdAsync(channel);
        if (metadata == null)
            return JsonSerializer.Serialize(new { status = "error", error = $"Channel not found: {channel}" }, McpJson.Tool);

        SetupRunner.DeleteChannelFiles(store, metadata);
        await store.RemoveAsync(channel);

        return JsonSerializer.Serialize(new { status = "ok", removed = channel }, McpJson.Tool);
    }
}
