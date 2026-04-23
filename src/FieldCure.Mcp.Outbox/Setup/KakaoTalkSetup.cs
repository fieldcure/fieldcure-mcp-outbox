using System.Text.Json;
using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;
using FieldCure.Mcp.Outbox.OAuth;

namespace FieldCure.Mcp.Outbox.Setup;

/// <summary>
/// Interactive setup flow for adding a KakaoTalk channel via OAuth.
/// </summary>
public static class KakaoTalkSetup
{
    /// <summary>
    /// Performs OAuth authorization and registers a new KakaoTalk channel.
    /// </summary>
    /// <param name="store">The channel store for persistence.</param>
    /// <param name="credentials">The credential manager for storing API keys.</param>
    /// <param name="name">Optional display name override.</param>
    public static async Task RunAsync(ChannelStore store, OAuthTokenStore tokenStore, string? name)
    {
        ConsoleHelper.PrintHeader("Add KakaoTalk Channel");

        var apiKey = ConsoleHelper.ReadMasked("REST API Key");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            ConsoleHelper.PrintError("REST API Key is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        var clientSecret = ConsoleHelper.ReadMasked("Client Secret (press Enter to skip if disabled)");

        var oauthFlow = new BrowserOAuthFlow();
        var redirectUri = oauthFlow.RedirectUri;

        Console.Error.WriteLine(
            $"[debug] client_id: {(apiKey.Length >= 6 ? apiKey[..6] : apiKey)}… (len={apiKey.Length})");
        Console.Error.WriteLine(
            $"[debug] client_secret: {(string.IsNullOrWhiteSpace(clientSecret) ? "(skipped)" : $"{clientSecret.Length} chars")}");
        Console.Error.WriteLine($"[debug] redirect_uri: {redirectUri}");

        var authUrl = $"https://kauth.kakao.com/oauth/authorize?client_id={apiKey}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope=talk_message";

        var callback = await oauthFlow.RunWithConsoleAsync(authUrl, "KakaoTalk");
        if (!callback.IsSuccess || string.IsNullOrWhiteSpace(callback.Code))
        {
            ConsoleHelper.PrintError(callback.ErrorDescription ?? "Authorization failed: no code received.");
            ConsoleHelper.WaitForKey();
            return;
        }

        Console.WriteLine("[OK]");

        // Exchange code for tokens
        using var httpClient = new HttpClient();
        var tokenParams = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = apiKey,
            ["redirect_uri"] = redirectUri,
            ["code"] = callback.Code,
        };

        if (!string.IsNullOrWhiteSpace(clientSecret))
            tokenParams["client_secret"] = clientSecret;

        var tokenResponse = await httpClient.PostAsync(
            "https://kauth.kakao.com/oauth/token",
            new FormUrlEncodedContent(tokenParams));

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            Console.Error.WriteLine(
                $"[debug] token endpoint: POST https://kauth.kakao.com/oauth/token → {(int)tokenResponse.StatusCode} {tokenResponse.StatusCode}");
            ConsoleHelper.PrintError($"Token exchange failed: {tokenJson}");
            ConsoleHelper.WaitForKey();
            return;
        }

        var tokenResult = JsonSerializer.Deserialize<KakaoTokenResponse>(tokenJson)!;

        // Determine channel ID
        var existingChannels = await store.LoadAsync();
        var kakaoCount = existingChannels.Count(c => c.Type == "kakaotalk");
        var id = $"kakaotalk_{kakaoCount + 1}";
        var displayName = name ?? "KakaoTalk";

        var tokenData = new KakaoTokenData
        {
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn),
            RefreshTokenExpiresAt = tokenResult.RefreshTokenExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResult.RefreshTokenExpiresIn)
                : null,
        };

        await tokenStore.SaveAsync(id, tokenData);
        Console.WriteLine("Tokens saved to tokens.json with current-user-only file permissions.");

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "kakaotalk",
            Name = displayName,
            ApiKey = apiKey,
            ClientSecret = string.IsNullOrWhiteSpace(clientSecret) ? null : clientSecret,
        });

        ConsoleHelper.PrintSuccess($"Channel '{id}' added.");
        Console.WriteLine($"Set {FieldCure.Mcp.Outbox.Credentials.OutboxSecretResolver.BuildEnvVarName(id, "API_KEY")} before sending.");
        if (!string.IsNullOrWhiteSpace(clientSecret))
            Console.WriteLine($"If your Kakao app requires a client secret, also set {FieldCure.Mcp.Outbox.Credentials.OutboxSecretResolver.BuildEnvVarName(id, "CLIENT_SECRET")}.");
        ConsoleHelper.WaitForKey();
    }
}
