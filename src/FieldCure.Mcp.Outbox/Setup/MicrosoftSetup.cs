using System.Net.Http.Headers;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;
using FieldCure.Mcp.Outbox.OAuth;

namespace FieldCure.Mcp.Outbox.Setup;

/// <summary>
/// Interactive setup flow for adding a Microsoft (Outlook / M365) channel via OAuth 2.0.
/// </summary>
public static class MicrosoftSetup
{
    /// <summary>
    /// Performs OAuth authorization and registers a new Microsoft channel.
    /// </summary>
    /// <param name="store">The channel store for persistence.</param>
    /// <param name="credentials">The credential manager for storing client credentials.</param>
    /// <param name="name">Optional display name override.</param>
    public static async Task RunAsync(ChannelStore store, OAuthTokenStore tokenStore, string? name)
    {
        ConsoleHelper.PrintHeader("Add Microsoft Channel");

        var clientId = ConsoleHelper.ReadLine("Client ID");
        if (string.IsNullOrWhiteSpace(clientId))
        {
            ConsoleHelper.PrintError("Client ID is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        var clientSecret = ConsoleHelper.ReadMasked("Client Secret");
        if (string.IsNullOrWhiteSpace(clientSecret))
        {
            ConsoleHelper.PrintError("Client Secret is required.");
            ConsoleHelper.WaitForKey();
            return;
        }

        var oauthFlow = new BrowserOAuthFlow();
        var redirectUri = oauthFlow.RedirectUri;
        var scope = Uri.EscapeDataString("Mail.Send User.Read offline_access");
        var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scope}&response_mode=query";

        var callback = await oauthFlow.RunWithConsoleAsync(authUrl, "Microsoft");
        if (!callback.IsSuccess || string.IsNullOrWhiteSpace(callback.Code))
        {
            var errorMsg = callback.ErrorDescription ?? "Authorization failed: no code received.";
            ConsoleHelper.PrintError(errorMsg);
            ConsoleHelper.WaitForKey();
            return;
        }

        Console.WriteLine("[OK]");

        // Exchange code for tokens
        using var httpClient = new HttpClient();
        var tokenParams = new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["scope"] = "Mail.Send User.Read offline_access",
            ["code"] = callback.Code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["client_secret"] = clientSecret,
        };

        var tokenResponse = await httpClient.PostAsync(
            "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            new FormUrlEncodedContent(tokenParams));

        var tokenJson = await tokenResponse.Content.ReadAsStringAsync();

        if (!tokenResponse.IsSuccessStatusCode)
        {
            ConsoleHelper.PrintError($"Token exchange failed: {tokenJson}");
            ConsoleHelper.WaitForKey();
            return;
        }

        var tokenResult = JsonSerializer.Deserialize<MicrosoftTokenResponse>(tokenJson)!;
        Console.WriteLine("Token received.");

        // Get user's email address via Graph API
        string? userEmail = null;
        try
        {
            using var meRequest = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
            var meResponse = await httpClient.SendAsync(meRequest);

            if (meResponse.IsSuccessStatusCode)
            {
                var meJson = await meResponse.Content.ReadAsStringAsync();
                var meData = JsonSerializer.Deserialize<JsonElement>(meJson);

                // Try mail first, then userPrincipalName
                if (meData.TryGetProperty("mail", out var mailProp) && mailProp.ValueKind == JsonValueKind.String)
                    userEmail = mailProp.GetString();
                if (string.IsNullOrEmpty(userEmail) && meData.TryGetProperty("userPrincipalName", out var upnProp))
                    userEmail = upnProp.GetString();
            }
        }
        catch
        {
            // Non-fatal: we can proceed without the email
        }

        if (!string.IsNullOrEmpty(userEmail))
            Console.WriteLine($"Signed in as: {userEmail}");

        // Determine channel ID
        var existingChannels = await store.LoadAsync();
        var msCount = existingChannels.Count(c => c.Type == "microsoft");
        var id = $"microsoft_{msCount + 1}";
        var displayName = name ?? "Microsoft";

        var tokenData = new MicrosoftTokenData
        {
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn),
        };

        await tokenStore.SaveAsync(id, tokenData);
        Console.WriteLine("Tokens saved to tokens.json with current-user-only file permissions.");

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "microsoft",
            Name = displayName,
            From = userEmail,
            Provider = "microsoft",
            ClientId = clientId,
        });

        ConsoleHelper.PrintSuccess($"Channel '{id}' added.");
        Console.WriteLine($"Set {FieldCure.Mcp.Outbox.Credentials.OutboxSecretResolver.BuildEnvVarName(id, "CLIENT_SECRET")} before sending, or use an MCP client that supports elicitation.");
        ConsoleHelper.WaitForKey();
    }
}
