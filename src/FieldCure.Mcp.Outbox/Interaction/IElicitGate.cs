using System.Text.Json;
using ModelContextProtocol.Protocol;

namespace FieldCure.Mcp.Outbox.Interaction;

/// <summary>
/// Represents the small subset of elicitation result data that Outbox needs in
/// order to collect secrets and coordinate browser-based OAuth flows.
/// </summary>
/// <param name="IsAccepted">Whether the client accepted and submitted the elicitation request.</param>
/// <param name="Content">Submitted field values keyed by schema property name.</param>
internal sealed record ElicitGateResult(bool IsAccepted, IDictionary<string, JsonElement>? Content);

/// <summary>
/// Minimal abstraction over MCP elicitation so interactive flows can be unit
/// tested without a concrete <c>McpServer</c>.
/// </summary>
internal interface IElicitGate
{
    /// <summary>
    /// Gets a value indicating whether elicitation is supported by the active client.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Sends an elicitation request to the active client.
    /// </summary>
    /// <param name="request">The elicitation request payload.</param>
    /// <param name="ct">Cancellation token for the request.</param>
    /// <returns>The simplified elicitation result returned by the client.</returns>
    Task<ElicitGateResult> ElicitAsync(ElicitRequestParams request, CancellationToken ct);
}
