namespace FieldCure.Mcp.Outbox.Configuration;

public record SmtpPreset(string Host, int Port, bool UseSsl);

public static class SmtpPresets
{
    static readonly Dictionary<string, SmtpPreset> Presets = new(StringComparer.OrdinalIgnoreCase)
    {
        ["gmail"] = new("smtp.gmail.com", 587, false),
        ["outlook"] = new("smtp-mail.outlook.com", 587, false),
        ["microsoft365"] = new("smtp.office365.com", 587, false),
        ["naver"] = new("smtp.naver.com", 587, false),
    };

    /// <summary>
    /// Gets a preset by provider name. Returns null if not found.
    /// All presets use port 587 with STARTTLS.
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
