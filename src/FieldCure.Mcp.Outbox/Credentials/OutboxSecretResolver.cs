using System.Collections.Concurrent;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Outbox.Credentials;

public sealed class OutboxSecretResolver
{
    readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    public void Remember(string envVarName, string value)
    {
        if (!string.IsNullOrWhiteSpace(envVarName) && !string.IsNullOrWhiteSpace(value))
            _cache[envVarName] = value;
    }

    public string BuildSoftFailMessage(params string[] envVarNames)
    {
        var joined = string.Join(", ", envVarNames.Where(static n => !string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.Ordinal));
        return $"Credential not configured. Set {joined} environment variable(s), or use a client that supports MCP Elicitation.";
    }

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

    public async Task<IReadOnlyDictionary<string, string>?> ResolveFieldsAsync(
        McpServer server,
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

        if (server.ClientCapabilities?.Elicitation is null)
            return null;

        try
        {
            var result = await server.ElicitAsync(new ElicitRequestParams
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

public sealed record SecretFieldRequest(
    string FieldName,
    string EnvVarName,
    string Title,
    string Description,
    string Message,
    bool Required = true,
    string? LegacyValue = null);
