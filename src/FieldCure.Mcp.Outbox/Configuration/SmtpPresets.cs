namespace FieldCure.Mcp.Outbox.Configuration;

/// <summary>
/// Represents preconfigured SMTP server settings for a known provider.
/// </summary>
/// <param name="Host">SMTP server hostname.</param>
/// <param name="Port">SMTP server port.</param>
/// <param name="UseSsl">Whether to use SSL on connect.</param>
public record SmtpPreset(string Host, int Port, bool UseSsl);

/// <summary>
/// Provides preconfigured SMTP settings for common email providers.
/// </summary>
public static class SmtpPresets
{
    static readonly Dictionary<string, SmtpPreset> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gmail"] = new("smtp.gmail.com", 587, false),
        ["naver"] = new("smtp.naver.com", 465, true),
    };

    /// <summary>
    /// Gets a preset by provider name. Returns null if not found.
    /// </summary>
    public static SmtpPreset? Get(string provider)
    {
        return Presets.GetValueOrDefault(provider);
    }

    /// <summary>
    /// Gets all available preset names.
    /// </summary>
    public static IReadOnlyCollection<string> Names => Presets.Keys;
}
