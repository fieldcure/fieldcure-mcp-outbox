using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FieldCure.Mcp.Outbox.Channels;

/// <summary>
/// Sends messages to Discord using a Webhook URL.
/// </summary>
public class DiscordChannel : IChannel
{
    const int ContentMaxLength = 2000;
    const int EmbedDescriptionMaxLength = 4096;
    const int MaxEmbeds = 10;
    const string Username = "AssistStudio";

    readonly string _webhookUrl;
    readonly HttpClient _httpClient;

    /// <inheritdoc />
    public string Id { get; }
    /// <inheritdoc />
    public string Type => "discord";
    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Initializes a new Discord channel.
    /// </summary>
    /// <param name="id">Unique channel identifier.</param>
    /// <param name="name">Display name.</param>
    /// <param name="webhookUrl">Discord Webhook URL.</param>
    /// <param name="httpClient">HTTP client for API calls.</param>
    public DiscordChannel(string id, string name, string webhookUrl, HttpClient httpClient)
    {
        Id = id;
        Name = name;
        _webhookUrl = webhookUrl;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default)
    {
        var message = request.Message;

        // Short message: send as plain content
        if (message.Length <= ContentMaxLength)
        {
            return await PostAsync(new { content = message, username = Username }, cancellationToken);
        }

        // Long message: use embeds
        var embeds = BuildEmbeds(message);
        return await PostAsync(new { embeds, username = Username }, cancellationToken);
    }

    /// <summary>
    /// Posts a JSON payload to the Discord webhook URL, retrying once on rate limit (429).
    /// </summary>
    async Task<SendResult> PostAsync(object payload, CancellationToken cancellationToken)
    {
        // Append ?wait=true for delivery confirmation
        var url = _webhookUrl.Contains('?')
            ? _webhookUrl + "&wait=true"
            : _webhookUrl + "?wait=true";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
        httpRequest.Content = JsonContent.Create(payload);

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        // Handle rate limiting
        if (response.StatusCode == (HttpStatusCode)429)
        {
            var retryAfter = response.Headers.RetryAfter?.Delta
                ?? TimeSpan.FromSeconds(1);
            response.Dispose();

            Console.Error.WriteLine($"[Discord] Rate limited, retrying after {retryAfter.TotalSeconds:F1}s...");
            await Task.Delay(retryAfter, cancellationToken);

            // Retry once
            using var retryRequest = new HttpRequestMessage(HttpMethod.Post, url);
            retryRequest.Content = JsonContent.Create(payload);
            response = await _httpClient.SendAsync(retryRequest, cancellationToken);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return new SendResult { Success = true };
            }

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return new SendResult { Success = false, Error = $"Discord API error {(int)response.StatusCode}: {errorBody}" };
        }
    }

    /// <summary>
    /// Splits a long message into Discord embed objects, each up to 4 096 characters.
    /// Appends a truncation notice if the message exceeds the maximum embed capacity.
    /// </summary>
    static List<object> BuildEmbeds(string message)
    {
        var embeds = new List<object>();
        var remaining = message.AsSpan();

        while (remaining.Length > 0 && embeds.Count < MaxEmbeds)
        {
            var chunkLength = Math.Min(remaining.Length, EmbedDescriptionMaxLength);
            var chunk = remaining[..chunkLength].ToString();
            remaining = remaining[chunkLength..];

            // Append truncation notice on the last embed if content remains
            if (embeds.Count == MaxEmbeds - 1 && remaining.Length > 0)
            {
                chunk = chunk[..^13] + "\n[truncated]";
            }

            embeds.Add(new { description = chunk, color = 5814783 });
        }

        return embeds;
    }
}
