namespace FieldCure.Mcp.Outbox.Channels;

/// <summary>
/// Represents a messaging channel capable of sending messages.
/// </summary>
public interface IChannel
{
    /// <summary>
    /// Gets the unique identifier for this channel.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the channel type (slack, telegram, smtp, kakaotalk).
    /// </summary>
    string Type { get; }

    /// <summary>
    /// Gets the display name for this channel.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Sends a message through this channel.
    /// </summary>
    /// <param name="request">The message request containing recipient, subject, and body.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the send operation.</returns>
    Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a request to send a message.
/// </summary>
public record SendRequest
{
    /// <summary>
    /// Gets the message body.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Gets the recipient address (required for SMTP).
    /// </summary>
    public string? To { get; init; }

    /// <summary>
    /// Gets the message subject (required for SMTP).
    /// </summary>
    public string? Subject { get; init; }

    /// <summary>
    /// Gets the target channel override (e.g. Slack channel name).
    /// </summary>
    public string? TargetChannel { get; init; }
}

/// <summary>
/// Represents the result of a send operation.
/// </summary>
public record SendResult
{
    /// <summary>
    /// Gets whether the send was successful.
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// Gets the error message if the send failed.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>
    /// Gets additional error detail (e.g. "reauthorization_required").
    /// </summary>
    public string? ErrorCode { get; init; }
}
