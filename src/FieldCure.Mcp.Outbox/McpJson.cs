using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldCure.Mcp.Outbox;

/// <summary>
/// Shared JSON serialization options for MCP tool responses and configuration.
/// Uses relaxed encoding so non-ASCII characters (Korean, CJK, emoji, etc.)
/// are emitted as-is instead of \uXXXX escape sequences.
/// </summary>
internal static class McpJson
{
    /// <summary>Tool response options: snake_case, indented.</summary>
    public static readonly JsonSerializerOptions Tool = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Channel store options: camelCase, indented, skip nulls.</summary>
    public static readonly JsonSerializerOptions Store = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    /// <summary>Simple indented options for setup and channel config files.</summary>
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
