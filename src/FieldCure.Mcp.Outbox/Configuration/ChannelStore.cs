using System.Text.Json;

namespace FieldCure.Mcp.Outbox.Configuration;

/// <summary>
/// Manages persistence of channel metadata to a JSON file on disk.
/// </summary>
public class ChannelStore
{

    public string DataDirectory { get; }

    string ChannelsFilePath => Path.Combine(DataDirectory, "channels.json");

    public ChannelStore()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox");
    }

    /// <summary>
    /// Loads all channel metadata from disk.
    /// </summary>
    public async Task<List<ChannelMetadata>> LoadAsync()
    {
        if (!File.Exists(ChannelsFilePath))
            return [];

        var json = await File.ReadAllTextAsync(ChannelsFilePath);
        var data = JsonSerializer.Deserialize<ChannelsFile>(json, McpJson.Store);
        return data?.Channels ?? [];
    }

    /// <summary>
    /// Saves all channel metadata to disk atomically.
    /// </summary>
    /// <param name="channels">The full list of channel metadata to persist.</param>
    public async Task SaveAsync(List<ChannelMetadata> channels)
    {
        Directory.CreateDirectory(DataDirectory);

        var data = new ChannelsFile { Channels = channels };
        var json = JsonSerializer.Serialize(data, McpJson.Store);

        // Atomic write: temp file + rename
        var tempPath = ChannelsFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, ChannelsFilePath, overwrite: true);
    }

    /// <summary>
    /// Retrieves a single channel by its unique identifier.
    /// </summary>
    /// <param name="id">The channel identifier to look up.</param>
    public async Task<ChannelMetadata?> GetByIdAsync(string id)
    {
        var channels = await LoadAsync();
        return channels.Find(c => c.Id == id);
    }

    /// <summary>
    /// Appends a new channel to the store and persists it.
    /// </summary>
    /// <param name="channel">The channel metadata to add.</param>
    public async Task AddAsync(ChannelMetadata channel)
    {
        var channels = await LoadAsync();
        channels.Add(channel);
        await SaveAsync(channels);
    }

    /// <summary>
    /// Removes a channel by its identifier and persists the change.
    /// </summary>
    /// <param name="id">The channel identifier to remove.</param>
    public async Task RemoveAsync(string id)
    {
        var channels = await LoadAsync();
        channels.RemoveAll(c => c.Id == id);
        await SaveAsync(channels);
    }
}

/// <summary>
/// Root object for the channels.json file.
/// </summary>
public class ChannelsFile
{
    public List<ChannelMetadata> Channels { get; set; } = [];
}

/// <summary>
/// Stores configuration metadata for a messaging channel.
/// </summary>
public class ChannelMetadata
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Credentials (stored in channels.json alongside metadata) ──

    // Slack
    public string? Token { get; set; }
    public string? DefaultChannel { get; set; }

    // Telegram
    public string? ApiId { get; set; }
    public string? ApiHash { get; set; }
    public string? Phone { get; set; }

    // SMTP
    public string? Provider { get; set; }
    public string? From { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public bool? Tls { get; set; }
    public string? Password { get; set; }

    // KakaoTalk
    public string? ApiKey { get; set; }
    public string? ClientSecret { get; set; }

    // Microsoft
    public string? ClientId { get; set; }
    // ClientSecret reused from KakaoTalk field above (same name)

    // Discord
    public string? WebhookUrl { get; set; }
}
