using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.Json;

namespace FieldCure.Mcp.Outbox.Configuration;

/// <summary>
/// Persists OAuth access/refresh tokens in a shared JSON file and applies
/// current-user-only filesystem protections where supported by the OS.
/// </summary>
public sealed class OAuthTokenStore
{
    public string DataDirectory { get; }
    public string TokensFilePath => Path.Combine(DataDirectory, "tokens.json");

    public OAuthTokenStore(string? dataDirectory = null)
    {
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FieldCure", "Mcp.Outbox");
    }

    public async Task<T?> GetAsync<T>(string channelId, CancellationToken cancellationToken = default)
    {
        var data = await LoadFileAsync(cancellationToken);
        if (!data.Channels.TryGetValue(channelId, out var value))
            return default;

        return value.Deserialize<T>(McpJson.Indented);
    }

    public async Task SaveAsync<T>(string channelId, T tokenData, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DataDirectory);

        var data = await LoadFileAsync(cancellationToken);
        data.Channels[channelId] = JsonSerializer.SerializeToElement(tokenData, McpJson.Indented);
        await SaveFileAsync(data, cancellationToken);
    }

    public async Task RemoveAsync(string channelId, CancellationToken cancellationToken = default)
    {
        var data = await LoadFileAsync(cancellationToken);
        if (!data.Channels.Remove(channelId))
            return;

        await SaveFileAsync(data, cancellationToken);
    }

    async Task<OAuthTokenFile> LoadFileAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(TokensFilePath))
            return new OAuthTokenFile();

        var json = await File.ReadAllTextAsync(TokensFilePath, cancellationToken);
        return JsonSerializer.Deserialize<OAuthTokenFile>(json, McpJson.Store) ?? new OAuthTokenFile();
    }

    async Task SaveFileAsync(OAuthTokenFile data, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(data, McpJson.Store);
        var tempPath = TokensFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, TokensFilePath, overwrite: true);
        ApplyUserOnlyPermissions(TokensFilePath);
    }

    static void ApplyUserOnlyPermissions(string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            var currentUser = WindowsIdentity.GetCurrent().User;
            if (currentUser is null)
                return;

            var security = new FileSecurity();
            security.SetOwner(currentUser);
            security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
            security.AddAccessRule(new FileSystemAccessRule(
                currentUser,
                FileSystemRights.FullControl,
                InheritanceFlags.None,
                PropagationFlags.None,
                AccessControlType.Allow));
            new FileInfo(filePath).SetAccessControl(security);
            return;
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
            File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }
}

public sealed class OAuthTokenFile
{
    public Dictionary<string, JsonElement> Channels { get; set; } = new(StringComparer.Ordinal);
}
