using System.Net.Http.Headers;
using System.Text.Json;

namespace FieldCure.Mcp.Outbox.Channels;

public class KakaoTalkChannel : IChannel
{
    readonly string _apiKey;
    readonly string? _clientSecret;
    readonly string _tokenFilePath;
    readonly HttpClient _httpClient;

    public string Id { get; }
    public string Type => "kakaotalk";
    public string Name { get; }

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

        var refreshed = await RefreshTokenAsync(tokenData.RefreshToken, cancellationToken);
        if (refreshed == null)
            return null;

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
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<KakaoTokenResponse>(json);
    }

    async Task SaveTokenDataAsync(KakaoTokenData tokenData, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_tokenFilePath);
        if (dir != null)
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(tokenData, new JsonSerializerOptions { WriteIndented = true });
        var tempPath = _tokenFilePath + ".tmp";
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, _tokenFilePath, overwrite: true);
    }
}

public class KakaoTokenData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RefreshTokenExpiresAt { get; set; }
}

public class KakaoTokenResponse
{
    public string access_token { get; set; } = string.Empty;
    public string? token_type { get; set; }
    public string? refresh_token { get; set; }
    public int expires_in { get; set; }
    public int refresh_token_expires_in { get; set; }

    // Map to PascalCase for internal use
    public string AccessToken => access_token;
    public string? RefreshToken => refresh_token;
    public int ExpiresIn => expires_in;
    public int RefreshTokenExpiresIn => refresh_token_expires_in;
}
