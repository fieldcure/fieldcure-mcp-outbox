using System.Collections.Concurrent;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Interaction;
using ModelContextProtocol.Protocol;

namespace FieldCure.Mcp.Outbox.Credentials;

/// <summary>
/// Resolves channel secrets from in-memory cache, environment variables,
/// legacy channel metadata, and finally MCP elicitation when available.
/// </summary>
public sealed class OutboxSecretResolver
{
    readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    /// <summary>
    /// Stores a resolved secret in the in-memory cache for the current process.
    /// </summary>
    /// <param name="envVarName">The canonical environment variable name for the secret.</param>
    /// <param name="value">The secret value to cache.</param>
    public void Remember(string envVarName, string value)
    {
        if (!string.IsNullOrWhiteSpace(envVarName) && !string.IsNullOrWhiteSpace(value))
            _cache[envVarName] = value;
    }

    /// <summary>
    /// Builds a soft-fail message describing which environment variables can
    /// satisfy a missing credential request.
    /// </summary>
    /// <param name="envVarNames">Candidate environment variable names for the missing secret(s).</param>
    /// <returns>A concise user-facing message describing how to configure the credential.</returns>
    public string BuildSoftFailMessage(params string[] envVarNames)
    {
        var joined = string.Join(", ", envVarNames.Where(static n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal));
        return $"Credential not configured. Set {joined} environment variable(s), or use a client that supports MCP Elicitation.";
    }

    /// <summary>
    /// Normalizes a channel identifier and field name into the environment
    /// variable convention used by Outbox.
    /// </summary>
    /// <param name="channelId">The channel identifier, such as <c>microsoft_1</c>.</param>
    /// <param name="fieldName">The logical secret field name, such as <c>client_secret</c>.</param>
    /// <returns>The corresponding <c>OUTBOX_...</c> environment variable name.</returns>
    public static string BuildEnvVarName(string channelId, string fieldName)
    {
        var sanitizedId = new string(channelId
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_')
            .ToArray());

        var sanitizedField = new string(fieldName
            .Select(ch => char.IsLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_')
            .ToArray());

        return $"OUTBOX_{sanitizedId}_{sanitizedField}";
    }

    /// <summary>
    /// Resolves a batch of secret fields using the standard Outbox lookup order:
    /// in-memory cache, environment variables, legacy metadata, then MCP elicitation.
    /// </summary>
    /// <param name="gate">The elicitation gate, or <see langword="null"/> when elicitation is unavailable.</param>
    /// <param name="fields">The secret fields that must be resolved.</param>
    /// <param name="ct">Cancellation token for the resolution flow.</param>
    /// <returns>A dictionary of resolved values, or <see langword="null"/> when required data could not be obtained.</returns>
    internal async Task<IReadOnlyDictionary<string, string>?> ResolveFieldsAsync(
        IElicitGate? gate,
        IReadOnlyList<SecretFieldRequest> fields,
        CancellationToken ct)
    {
        var resolved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<SecretFieldRequest>();

        foreach (var field in fields)
        {
            if (TryResolve(field, out var value))
            {
                resolved[field.FieldName] = value;
                Remember(field.EnvVarName, value);
            }
            else
            {
                missing.Add(field);
            }
        }

        if (missing.Count == 0)
            return resolved;

        if (gate?.IsSupported is not true)
            return null;

        try
        {
            var result = await gate.ElicitAsync(new ElicitRequestParams
            {
                Message = missing[0].Message,
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = missing.ToDictionary(
                        f => f.FieldName,
                        f => (ElicitRequestParams.PrimitiveSchemaDefinition)new ElicitRequestParams.StringSchema
                        {
                            Title = f.Title,
                            Description = f.Description,
                            MinLength = f.Required ? 1 : null,
                        }),
                    Required = missing.Where(static f => f.Required).Select(static f => f.FieldName).ToArray(),
                },
            }, ct);

            if (!result.IsAccepted || result.Content is null)
                return null;

            foreach (var field in missing)
            {
                if (!result.Content.TryGetValue(field.FieldName, out var value))
                {
                    if (field.Required)
                        return null;
                    continue;
                }

                var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (field.Required)
                        return null;
                    continue;
                }

                resolved[field.FieldName] = text;
                Remember(field.EnvVarName, text);
            }

            return resolved;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Attempts to resolve a single secret field from non-interactive sources.
    /// </summary>
    /// <param name="field">The field descriptor to resolve.</param>
    /// <param name="value">When this method returns, contains the resolved value if successful.</param>
    /// <returns><see langword="true"/> when a non-empty value was found; otherwise <see langword="false"/>.</returns>
    bool TryResolve(SecretFieldRequest field, out string value)
    {
        value = "";
        if (_cache.TryGetValue(field.EnvVarName, out var cached) && !string.IsNullOrWhiteSpace(cached))
        {
            value = cached;
            return true;
        }

        value = Environment.GetEnvironmentVariable(field.EnvVarName) ?? "";
        if (!string.IsNullOrWhiteSpace(value))
            return true;

        value = field.LegacyValue ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }
}

/// <summary>
/// Describes one secret field that can be resolved from cache, environment
/// variables, legacy stored metadata, or MCP elicitation.
/// </summary>
/// <param name="FieldName">The logical field name used in elicitation payloads.</param>
/// <param name="EnvVarName">The environment variable name that can satisfy this field.</param>
/// <param name="Title">Short UI label shown to the user.</param>
/// <param name="Description">Longer field description shown to the user.</param>
/// <param name="Message">Prompt message used when elicitation is needed.</param>
/// <param name="Required">Whether the field must be present for the operation to proceed.</param>
/// <param name="LegacyValue">Fallback plaintext value already present in stored channel metadata.</param>
public sealed record SecretFieldRequest(
    string FieldName,
    string EnvVarName,
    string Title,
    string Description,
    string Message,
    bool Required = true,
    string? LegacyValue = null);
