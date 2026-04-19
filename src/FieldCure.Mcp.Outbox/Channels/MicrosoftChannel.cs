using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Channels;

/// <summary>
/// Sends emails via the Microsoft Graph API with OAuth 2.0 token management.
/// </summary>
public class MicrosoftChannel : IChannel
{
    readonly string _clientId;
    readonly string _clientSecret;
    readonly OAuthTokenStore _tokenStore;
    readonly HttpClient _httpClient;

    /// <inheritdoc />
    public string Id { get; }
    /// <inheritdoc />
    public string Type => "microsoft";
    /// <inheritdoc />
    public string Name { get; }

    /// <summary>
    /// Initializes a new Microsoft Graph API channel.
    /// </summary>
    /// <param name="id">Unique channel identifier.</param>
    /// <param name="name">Display name.</param>
    /// <param name="clientId">Azure Entra ID application (client) ID.</param>
    /// <param name="clientSecret">Azure Entra ID client secret.</param>
    /// <param name="tokenStore">Shared OAuth token store.</param>
    /// <param name="httpClient">HTTP client for API calls.</param>
    public MicrosoftChannel(string id, string name, string clientId, string clientSecret,
        OAuthTokenStore tokenStore, HttpClient httpClient)
    {
        Id = id;
        Name = name;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _tokenStore = tokenStore;
        _httpClient = httpClient;
    }

    /// <inheritdoc />
    public async Task<SendResult> SendAsync(SendRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.To))
            return new SendResult { Success = false, Error = "Recipient (to) is required for Microsoft channel." };
        if (string.IsNullOrWhiteSpace(request.Subject))
            return new SendResult { Success = false, Error = "Subject is required for Microsoft channel." };

        try
        {
            var token = await GetValidAccessTokenAsync(cancellationToken);
            if (token == null)
                return new SendResult { Success = false, Error = "Microsoft authorization has expired. Please re-authorize.", ErrorCode = "reauthorization_required" };

            var payload = JsonSerializer.Serialize(new
            {
                message = new
                {
                    subject = request.Subject,
                    body = new
                    {
                        contentType = "Text",
                        content = request.Message,
                    },
                    toRecipients = new[]
                    {
                        new { emailAddress = new { address = request.To } },
                    },
                },
            });

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
                "https://graph.microsoft.com/v1.0/me/sendMail");
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            httpRequest.Content = new StringContent(payload, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.StatusCode == System.Net.HttpStatusCode.Accepted || response.IsSuccessStatusCode)
                return new SendResult { Success = true };

            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            return new SendResult { Success = false, Error = $"Graph API error ({(int)response.StatusCode}): {errorBody}" };
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
        var tokenData = await _tokenStore.GetAsync<MicrosoftTokenData>(Id, cancellationToken);
        if (tokenData == null)
            return null;

        // Check if access token is still valid (1-minute buffer)
        if (tokenData.ExpiresAt > DateTime.UtcNow.AddMinutes(1))
            return tokenData.AccessToken;

        // Try to refresh
        if (string.IsNullOrEmpty(tokenData.RefreshToken))
            return null;

        var refreshed = await RefreshTokenAsync(tokenData.RefreshToken, cancellationToken);
        if (refreshed == null)
            return null;

        // Update token data
        tokenData.AccessToken = refreshed.AccessToken;
        tokenData.ExpiresAt = DateTime.UtcNow.AddSeconds(refreshed.ExpiresIn);

        if (!string.IsNullOrEmpty(refreshed.RefreshToken))
            tokenData.RefreshToken = refreshed.RefreshToken;

        await _tokenStore.SaveAsync(Id, tokenData, cancellationToken);
        return tokenData.AccessToken;
    }

    /// <summary>
    /// Exchanges a refresh token for a new access token via the Microsoft identity platform.
    /// </summary>
    async Task<MicrosoftTokenResponse?> RefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["scope"] = "Mail.Send User.Read offline_access",
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
            ["client_secret"] = _clientSecret,
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post,
            "https://login.microsoftonline.com/common/oauth2/v2.0/token")
        {
            Content = new FormUrlEncodedContent(parameters),
        };

        var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<MicrosoftTokenResponse>(json);
    }
}

/// <summary>
/// Persisted Microsoft OAuth token data.
/// </summary>
public class MicrosoftTokenData
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
}

/// <summary>
/// Deserialized response from the Microsoft identity platform token endpoint.
/// </summary>
public class MicrosoftTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string? TokenType { get; set; }

    [JsonPropertyName("refresh_token")]
    public string? RefreshToken { get; set; }

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}
