using System.Diagnostics;
using System.Net;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;

namespace FieldCure.Mcp.Outbox.Setup;

/// <summary>
/// Interactive setup flow for adding a KakaoTalk channel via OAuth.
/// </summary>
public static class KakaoTalkSetup
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>
    /// Performs OAuth authorization and registers a new KakaoTalk channel.
    /// </summary>
    /// <param name="store">The channel store for persistence.</param>
    /// <param name="credentials">The credential manager for storing API keys.</param>
    /// <param name="name">Optional display name override.</param>
    public static async Task RunAsync(ChannelStore store, CredentialManager credentials, string? name)
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

        // Use fixed port for Kakao redirect URI (must match Kakao developer console)
        // OAuth redirect URI: no trailing slash (matches Kakao console registration)
        // HttpListener prefix: requires trailing slash
        const int port = 9876;
        var redirectUri = $"http://localhost:{port}/callback";
        var listenerPrefix = $"{redirectUri}/";

        // Start local HTTP listener
        using var httpListener = new HttpListener();
        httpListener.Prefixes.Add(listenerPrefix);
        httpListener.Start();

        // Open browser for authorization
        var authUrl = $"https://kauth.kakao.com/oauth/authorize?client_id={apiKey}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope=talk_message";

        Console.WriteLine();
        Console.WriteLine("Opening browser for Kakao login...");
        Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

        Console.Write("Waiting for authorization... ");

        // Wait for callback
        var context = await httpListener.GetContextAsync();
        var code = context.Request.QueryString["code"];

        // Send response to browser
        var responseHtml = System.Text.Encoding.UTF8.GetBytes(
            "<html><body><h2>Authorization successful!</h2><p>You can close this window.</p></body></html>");
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = responseHtml.Length;
        await context.Response.OutputStream.WriteAsync(responseHtml);
        context.Response.Close();

        httpListener.Stop();

        if (string.IsNullOrEmpty(code))
        {
            ConsoleHelper.PrintError("Authorization failed: no code received.");
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
            ["code"] = code,
        };

        if (!string.IsNullOrWhiteSpace(clientSecret))
            tokenParams["client_secret"] = clientSecret;

        var tokenResponse = await httpClient.PostAsync(
            "https://kauth.kakao.com/oauth/token",
            new FormUrlEncodedContent(tokenParams));

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
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

        // Save token file
        var tokensDir = Path.Combine(store.DataDirectory, "tokens");
        Directory.CreateDirectory(tokensDir);
        var tokenFilePath = Path.Combine(tokensDir, $"{id}.json");

        var tokenData = new KakaoTokenData
        {
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn),
            RefreshTokenExpiresAt = tokenResult.RefreshTokenExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResult.RefreshTokenExpiresIn)
                : null,
        };

        var tokenDataJson = JsonSerializer.Serialize(tokenData, JsonOptions);
        await File.WriteAllTextAsync(tokenFilePath, tokenDataJson);

        Console.WriteLine("Token received and saved.");

        // Store credentials
        credentials.Store($"FieldCure.Outbox:{id}:api_key", apiKey);
        if (!string.IsNullOrWhiteSpace(clientSecret))
            credentials.Store($"FieldCure.Outbox:{id}:client_secret", clientSecret);

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "kakaotalk",
            Name = displayName,
        });

        ConsoleHelper.PrintSuccess($"Channel '{id}' added.");
        ConsoleHelper.WaitForKey();
    }
}
