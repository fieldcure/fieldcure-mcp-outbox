using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FieldCure.Mcp.Outbox.Channels;

/// <summary>
/// Sends messages to KakaoTalk using the Kakao REST API with OAuth token management.
/// </summary>
public class KakaoTalkChannel : IChannel
{
    readonly string _apiKey;
    readonly string? _clientSecret;
    readonly string _tokenFilePath;
    readonly HttpClient _httpClient;

    /// <inheritdoc />
    public string Id { get; }
    /// <inheritdoc />
    public string Type => "kakaotalk";
    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Initializes a new KakaoTalk channel.
    /// </summary>
    /// <param name="id">Unique channel identifier.</param>
    /// <param name="name">Display name.</param>
    /// <param name="apiKey">Kakao REST API key.</param>
    /// <param name="clientSecret">Kakao client secret (optional).</param>
    /// <param name="tokenFilePath">File path for persisted OAuth tokens.</param>
    /// <param name="httpClient">HTTP client for API calls.</param>
    public KakaoTalkChannel(string id, string name, string apiKey, string? clientSecret,
        string tokenFilePath, HttpClient httpClient)
    {
        Id = id;
        Name = name;
        _apiKey = apiKey;
        _clientSecret = clientSecret;
        _tokenFilePath = tokenFilePath;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var token = await GetValidAccessTokenAsync(cancellationToken);
            if (token == null)
                return new SendResult { Success = false, Error = "KakaoTalk authorization has expired. Please re-authorize.", ErrorCode = "reauthorization_required" };

            var templateObject = JsonSerializer.Serialize(new
            {
                object_type = "text",
                text = request.Message,
                link = new
                {
                    web_url = "https://github.com/fieldcure/fieldcure-mcp-outbox",
                    mobile_web_url = "https://github.com/fieldcure/fieldcure-mcp-outbox",
                },
            });

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://kapi.kakao.com/v2/api/talk/memo/default/send");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpRequest.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["template_object"] = templateObject,
            });

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<JsonElement>(responseJson);
                if (result.TryGetProperty("result_code", out var code) && code.GetInt32() == 0)
                    return new SendResult { Success = true };
            }

            return new SendResult { Success = false, Error = $"KakaoTalk API error: {responseJson}" };
        }
        catch (Exception ex)
        {
            return new SendResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Returns a valid access token, refreshing it if expired.
    /// </summary>
    async Task<string?> GetValidAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_tokenFilePath))
            return null;

        var json = await File.ReadAllTextAsync(_tokenFilePath, cancellationToken);
        var tokenData = JsonSerializer.Deserialize<KakaoTokenData>(json);
        if (tokenData == null)
            return null;

        // Check if access token is still valid
        if (tokenData.ExpiresAt > DateTime.UtcNow.AddMinutes(1))
            return tokenData.AccessToken;

        // Try to refresh
        if (string.IsNullOrEmpty(tokenData.RefreshToken))
            return null;

        Console.Error.WriteLine($"[KakaoTalk] Access token expired (was {tokenData.ExpiresAt:u}), attempting refresh...");
        var refreshed = await RefreshTokenAsync(tokenData.RefreshToken, cancellationToken);
        if (refreshed == null)
        {
            Console.Error.WriteLine("[KakaoTalk] Token refresh returned null — reauthorization may be required");
            return null;
        }
        Console.Error.WriteLine($"[KakaoTalk] Token refreshed, new expiry in {refreshed.ExpiresIn}s");

        // Update token file
        tokenData.AccessToken = refreshed.AccessToken;
        tokenData.ExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);

        // Refresh token is only returned when remaining validity < 1 month
        if (!string.IsNullOrEmpty(refreshed.RefreshToken))
        {
            tokenData.RefreshToken = refreshed.RefreshToken;
            if (refreshed.RefreshTokenExpiresIn > 0)
                tokenData.RefreshTokenExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.RefreshTokenExpiresIn);
        }

        await SaveTokenDataAsync(tokenData, cancellationToken);
        return tokenData.AccessToken;
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token via the Kakao OAuth endpoint.
    /// </summary>
    async Task<KakaoTokenResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = _apiKey,
            ["refresh_token"] = refreshToken,
        };

        if (!string.IsNullOrEmpty(_clientSecret))
            parameters["client_secret"] = _clientSecret;

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://kauth.kakao.com/oauth/token")
        {
            Content = new FormUrlEncodedContent(parameters),
        };

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            Console.Error.WriteLine($"[KakaoTalk] Token refresh failed ({response.StatusCode}): {json}");
            return null;
        }

        return JsonSerializer.Deserialize<KakaoTokenResponse>(json);
    }

    /// <summary>
    /// Persists token data to disk atomically.
    /// </summary>
    async Task SaveTokenDataAsync(KakaoTokenData tokenData, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_tokenFilePath);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(tokenData, McpJson.Indented);
        var tempPath = _tokenFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _tokenFilePath, overwrite: true);
    }
}

/// <summary>
/// Persisted KakaoTalk OAuth token data.
/// </summary>
public class KakaoTokenData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
}

/// <summary>
/// Deserialized response from the Kakao OAuth token endpoint.
/// </summary>
public class KakaoTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; }
}
