using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Configuration;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Outbox.Tools;

/// <summary>
/// MCP tool that adds a new messaging channel via an interactive setup console.
/// </summary>
[McpServerToolType]
public static class AddChannelTool
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [McpServerTool(Name = "add_channel")]
    [Description(
        "Adds a new messaging channel by opening a setup console window for secure credential entry. " +
        "Supported types: slack, telegram, gmail, naver, smtp, kakaotalk, microsoft, discord.")]
    public static async Task<string> AddChannel(
        ChannelStore store,
        [Description("Channel type: slack, telegram, gmail, naver, smtp, kakaotalk, microsoft, discord")]
        string type,
        [Description("Display name for the channel (optional)")]
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        // Snapshot channels before adding
        var before = await store.LoadAsync();
        var beforeIds = before.Select(c => c.Id).ToHashSet();

        // Get the executable path
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath))
            return JsonSerializer.Serialize(new { status = "error", error = "Cannot determine executable path." }, JsonOptions);

        // Build arguments
        var args = $"add {type}";
        if (!string.IsNullOrEmpty(name))
            args += $" --name \"{name}\"";

        // Start the setup process in a new console window
        var processInfo = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = args,
            UseShellExecute = true,
            CreateNoWindow = false,
        };

        var process = Process.Start(processInfo);
        if (process == null)
            return JsonSerializer.Serialize(new { status = "error", error = "Failed to start setup process." }, JsonOptions);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
            return JsonSerializer.Serialize(new { status = "error", error = "Setup process exited with an error." }, JsonOptions);

        // Find the newly added channel
        var after = await store.LoadAsync();
        var newChannel = after.FirstOrDefault(c => !beforeIds.Contains(c.Id));

        if (newChannel == null)
            return JsonSerializer.Serialize(new { status = "error", error = "No new channel was added." }, JsonOptions);

        return JsonSerializer.Serialize(new { status = "ok", channel_id = newChannel.Id }, JsonOptions);
    }
}
