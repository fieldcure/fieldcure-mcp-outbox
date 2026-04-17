using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;

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
    public static async Task RunAsync(ChannelStore store, string? name)
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

        // Use fixed port for Microsoft redirect URI (must match Azure app registration)
        const int port = 9876;
        var redirectUri = $"http://localhost:{port}/callback";
        var listenerPrefix = $"{redirectUri}/";

        // Start local HTTP listener
        using var httpListener = new HttpListener();
        httpListener.Prefixes.Add(listenerPrefix);
        httpListener.Start();

        // Open browser for authorization
        var scope = Uri.EscapeDataString("Mail.Send User.Read offline_access");
        var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scope}&response_mode=query";

        Console.WriteLine();
        Console.WriteLine("Opening browser for Microsoft login...");
        Process.Start(new ProcessStartInfo { FileName = authUrl, UseShellExecute = true });

        Console.Write("Waiting for authorization... ");

        // Wait for callback
        var context = await httpListener.GetContextAsync();
        var code = context.Request.QueryString["code"];
        var error = context.Request.QueryString["error"];

        // Send response to browser
        string responseHtmlText;
        if (!string.IsNullOrEmpty(error))
        {
            var errorDesc = context.Request.QueryString["error_description"] ?? error;
            responseHtmlText = $"<html><body><h2>Authorization failed</h2><p>{WebUtility.HtmlEncode(errorDesc)}</p></body></html>";
        }
        else
        {
            responseHtmlText = "<html><body><h2>Authorization successful!</h2><p>You can close this window.</p></body></html>";
        }

        var responseHtml = System.Text.Encoding.UTF8.GetBytes(responseHtmlText);
        context.Response.ContentType = "text/html";
        context.Response.ContentLength64 = responseHtml.Length;
        await context.Response.OutputStream.WriteAsync(responseHtml);
        context.Response.Close();

        httpListener.Stop();

        if (string.IsNullOrEmpty(code))
        {
            var errorMsg = error != null
                ? $"Authorization failed: {context.Request.QueryString["error_description"] ?? error}"
                : "Authorization failed: no code received.";
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
            ["code"] = code,
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

        // Save token file
        var tokensDir = Path.Combine(store.DataDirectory, "tokens");
        Directory.CreateDirectory(tokensDir);
        var tokenFilePath = Path.Combine(tokensDir, $"{id}.json");

        var tokenData = new MicrosoftTokenData
        {
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn),
        };

        var tokenDataJson = JsonSerializer.Serialize(tokenData, McpJson.Indented);
        await File.WriteAllTextAsync(tokenFilePath, tokenDataJson);

        Console.WriteLine("Token saved.");

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "microsoft",
            Name = displayName,
            From = userEmail,
            Provider = "microsoft",
            ClientId = clientId,
            ClientSecret = clientSecret,
        });

        ConsoleHelper.PrintSuccess($"Channel '{id}' added.");
        ConsoleHelper.WaitForKey();
    }
}
