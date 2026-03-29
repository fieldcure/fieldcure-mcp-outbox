using System.Net.Http.Json;
using System.Text.Json;

namespace FieldCure.Mcp.Outbox.Channels;

/// <summary>
/// Sends messages to Slack using the Bot Token and chat.postMessage API.
/// </summary>
public class SlackChannel : IChannel
{
    readonly string _botToken;
    readonly string _defaultChannel;
    readonly HttpClient _httpClient;

    /// <inheritdoc />
    public string Id { get; }
    /// <inheritdoc />
    public string Type => "slack";
    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Initializes a new Slack channel.
    /// </summary>
    /// <param name="id">Unique channel identifier.</param>
    /// <param name="name">Display name.</param>
    /// <param name="defaultChannel">Default Slack channel to post to.</param>
    /// <param name="botToken">Slack bot token (xoxb-...).</param>
    /// <param name="httpClient">HTTP client for API calls.</param>
    public SlackChannel(string id, string name, string defaultChannel, string botToken, HttpClient httpClient)
    {
        Id = id;
        Name = name;
        _defaultChannel = defaultChannel;
        _botToken = botToken;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default)
    {
        var channel = request.TargetChannel ?? _defaultChannel;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://slack.com/api/chat.postMessage");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _botToken);
        httpRequest.Content = JsonContent.Create(new
        {
            channel,
            text = request.Message,
        });

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

        if (json.TryGetProperty("ok", out var ok) && ok.GetBoolean())
        {
            return new SendResult { Success = true };
        }

        var error = json.TryGetProperty("error", out var errorProp) ? errorProp.GetString() : "Unknown error";
        return new SendResult { Success = false, Error = error };
    }
}
