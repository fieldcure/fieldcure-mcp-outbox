using System.ComponentModel;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Configuration;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Outbox.Tools;

[McpServerToolType]
public static class ListChannelsTool
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [McpServerTool(Name = "list_channels")]
    [Description("Lists all configured messaging channels. Returns channel ID, type, name, and additional metadata such as the sender address for SMTP channels.")]
    public static async Task<string> ListChannels(
        ChannelStore store,
        CancellationToken cancellationToken)
    {
        var channels = await store.LoadAsync();

        var result = new
        {
            channels = channels.Select(c => new
            {
                id = c.Id,
                type = c.Type,
                name = c.Name,
                from = c.From,
            }),
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
