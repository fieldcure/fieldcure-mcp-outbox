using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldCure.Mcp.Outbox.Configuration;

public class ChannelStore
{
    static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string DataDirectory { get; }

    string ChannelsFilePath => Path.Combine(DataDirectory, "channels.json");

    public ChannelStore()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox");
    }

    public async Task<List<ChannelMetadata>> LoadAsync()
    {
        if (!File.Exists(ChannelsFilePath))
            return [];

        var json = await File.ReadAllTextAsync(ChannelsFilePath);
        var data = JsonSerializer.Deserialize<ChannelsFile>(json, JsonOptions);
        return data?.Channels ?? [];
    }

    public async Task SaveAsync(List<ChannelMetadata> channels)
    {
        Directory.CreateDirectory(DataDirectory);

        var data = new ChannelsFile { Channels = channels };
        var json = JsonSerializer.Serialize(data, JsonOptions);

        // Atomic write: temp file + rename
        var tempPath = ChannelsFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json);
        File.Move(tempPath, ChannelsFilePath, overwrite: true);
    }

    public async Task<ChannelMetadata?> GetByIdAsync(string id)
    {
        var channels = await LoadAsync();
        return channels.Find(c => c.Id == id);
    }

    public async Task AddAsync(ChannelMetadata channel)
    {
        var channels = await LoadAsync();
        channels.Add(channel);
        await SaveAsync(channels);
    }

    public async Task RemoveAsync(string id)
    {
        var channels = await LoadAsync();
        channels.RemoveAll(c => c.Id == id);
        await SaveAsync(channels);
    }
}

public class ChannelsFile
{
    public List<ChannelMetadata> Channels { get; set; } = [];
}

public class ChannelMetadata
{
    public required string Id { get; set; }
    public required string Type { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Telegram
    public string? Phone { get; set; }

    // SMTP
    public string? Provider { get; set; }
    public string? From { get; set; }
    public string? Host { get; set; }
    public int? Port { get; set; }
    public bool? Tls { get; set; }

    // Slack
    public string? DefaultChannel { get; set; }
}
