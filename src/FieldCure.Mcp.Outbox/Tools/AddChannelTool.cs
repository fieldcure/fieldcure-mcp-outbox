using System.ComponentModel;
using System.Net.Http.Headers;
using System.Text.Json;
using FieldCure.Mcp.Outbox.Channels;
using FieldCure.Mcp.Outbox.Configuration;
using FieldCure.Mcp.Outbox.Credentials;
using FieldCure.Mcp.Outbox.OAuth;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace FieldCure.Mcp.Outbox.Tools;

[McpServerToolType]
public static class AddChannelTool
{
    [McpServerTool(Name = "add_channel")]
    [Description(
        "Adds a new messaging channel by collecting configuration through MCP elicitation. " +
        "Supported types: slack, telegram, gmail, naver, smtp, kakaotalk, microsoft, discord.")]
    public static async Task<string> AddChannel(
        McpServer server,
        ChannelStore store,
        OAuthTokenStore tokenStore,
        OutboxSecretResolver resolver,
        [Description("Channel type: slack, telegram, gmail, naver, smtp, kakaotalk, microsoft, discord")]
        string type,
        [Description("Display name for the channel (optional)")]
        string? name = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var normalizedType = type.Trim().ToLowerInvariant();
            return normalizedType switch
            {
                "slack" => await AddSlackAsync(server, store, resolver, name, cancellationToken),
                "discord" => await AddDiscordAsync(server, store, resolver, name, cancellationToken),
                "gmail" or "naver" or "smtp" => await AddSmtpAsync(server, store, resolver, normalizedType, name, cancellationToken),
                "telegram" => await AddTelegramAsync(server, store, resolver, name, cancellationToken),
                "microsoft" => await AddMicrosoftAsync(server, store, tokenStore, resolver, name, cancellationToken),
                "kakaotalk" => await AddKakaoTalkAsync(server, store, tokenStore, resolver, name, cancellationToken),
                _ => JsonSerializer.Serialize(new { status = "error", error = $"Unsupported channel type: {type}" }, McpJson.Tool),
            };
        }
        catch (OperationCanceledException)
        {
            return JsonSerializer.Serialize(new { status = "error", error = "Channel setup was cancelled." }, McpJson.Tool);
        }
    }

    static async Task<string> AddSlackAsync(McpServer server, ChannelStore store, OutboxSecretResolver resolver, string? name, CancellationToken ct)
    {
        var values = await PromptAsync(server, "Enter the Slack channel name and bot token.",
        [
            Field("channel_name", "Channel Name", "Slack channel name without leading #"),
            Field("bot_token", "Bot Token", "Slack bot token (xoxb-...)"),
        ], ct);

        if (values is null)
            return JsonSerializer.Serialize(new { status = "error", error = "Slack setup requires a client that supports MCP Elicitation." }, McpJson.Tool);

        var channelName = string.IsNullOrWhiteSpace(name) ? values["channel_name"] : name;
        if (string.IsNullOrWhiteSpace(channelName))
            return JsonSerializer.Serialize(new { status = "error", error = "Channel name is required." }, McpJson.Tool);

        var id = $"slack_{channelName}";
        resolver.Remember(OutboxSecretResolver.BuildEnvVarName(id, "BOT_TOKEN"), values["bot_token"]);

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "slack",
            Name = channelName,
            DefaultChannel = channelName,
        });

        return JsonSerializer.Serialize(new { status = "ok", channel_id = id }, McpJson.Tool);
    }

    static async Task<string> AddDiscordAsync(McpServer server, ChannelStore store, OutboxSecretResolver resolver, string? name, CancellationToken ct)
    {
        var values = await PromptAsync(server, "Enter the Discord channel name and webhook URL.",
        [
            Field("channel_name", "Channel Name", "Display name for this Discord channel"),
            Field("webhook_url", "Webhook URL", "Discord webhook URL"),
        ], ct);

        if (values is null)
            return JsonSerializer.Serialize(new { status = "error", error = "Discord setup requires a client that supports MCP Elicitation." }, McpJson.Tool);

        var channelName = string.IsNullOrWhiteSpace(name) ? values["channel_name"] : name;
        var webhookUrl = values["webhook_url"];

        if (string.IsNullOrWhiteSpace(channelName))
            return JsonSerializer.Serialize(new { status = "error", error = "Channel name is required." }, McpJson.Tool);

        if (!webhookUrl.StartsWith("https://discord.com/api/webhooks/", StringComparison.OrdinalIgnoreCase)
            && !webhookUrl.StartsWith("https://discordapp.com/api/webhooks/", StringComparison.OrdinalIgnoreCase))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = "Webhook URL must start with https://discord.com/api/webhooks/ or https://discordapp.com/api/webhooks/."
            }, McpJson.Tool);
        }

        var id = $"discord_{channelName}";
        resolver.Remember(OutboxSecretResolver.BuildEnvVarName(id, "WEBHOOK_URL"), webhookUrl);

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "discord",
            Name = channelName,
        });

        return JsonSerializer.Serialize(new { status = "ok", channel_id = id }, McpJson.Tool);
    }

    static async Task<string> AddSmtpAsync(McpServer server, ChannelStore store, OutboxSecretResolver resolver, string type, string? name, CancellationToken ct)
    {
        var existingChannels = await store.LoadAsync();
        var provider = type switch
        {
            "gmail" => "gmail",
            "naver" => "naver",
            _ => "smtp",
        };

        var id = $"smtp_{provider}_{existingChannels.Count(c => c.Type == "smtp" && c.Provider == provider) + 1}";

        if (provider == "smtp")
        {
            var values = await PromptAsync(server, "Enter SMTP channel settings.",
            [
                Field("display_name", "Display Name", "Display name for this SMTP channel"),
                Field("host", "Host", "SMTP host"),
                Field("port", "Port", "SMTP port"),
                Field("tls", "Use TLS", "Enter true or false"),
                Field("from", "From Address", "Sender email address"),
                Field("password", "Password", "SMTP password or app password"),
            ], ct);

            if (values is null)
                return JsonSerializer.Serialize(new { status = "error", error = "SMTP setup requires a client that supports MCP Elicitation." }, McpJson.Tool);

            if (!int.TryParse(values["port"], out var port))
                port = 587;

            resolver.Remember(OutboxSecretResolver.BuildEnvVarName(id, "PASSWORD"), values["password"]);

            await store.AddAsync(new ChannelMetadata
            {
                Id = id,
                Type = "smtp",
                Name = string.IsNullOrWhiteSpace(name) ? values["display_name"] : name,
                Provider = "smtp",
                From = values["from"],
                Host = values["host"],
                Port = port,
                Tls = !values["tls"].Equals("false", StringComparison.OrdinalIgnoreCase),
            });

            return JsonSerializer.Serialize(new { status = "ok", channel_id = id }, McpJson.Tool);
        }

        var preset = SmtpPresets.Get(provider)!;
        var presetValues = await PromptAsync(server, $"Enter the {provider} sender address and app password.",
        [
            Field("from", "Email Address", $"{provider} sender address"),
            Field("password", "App Password", $"{provider} app password"),
        ], ct);

        if (presetValues is null)
            return JsonSerializer.Serialize(new { status = "error", error = $"{provider} setup requires a client that supports MCP Elicitation." }, McpJson.Tool);

        resolver.Remember(OutboxSecretResolver.BuildEnvVarName(id, "PASSWORD"), presetValues["password"]);

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "smtp",
            Name = name ?? (provider == "gmail" ? "Gmail" : "Naver"),
            Provider = provider,
            From = presetValues["from"],
            Host = preset.Host,
            Port = preset.Port,
            Tls = true,
        });

        return JsonSerializer.Serialize(new { status = "ok", channel_id = id }, McpJson.Tool);
    }

    static async Task<string> AddTelegramAsync(McpServer server, ChannelStore store, OutboxSecretResolver resolver, string? name, CancellationToken ct)
    {
        var existingChannels = await store.LoadAsync();
        var id = $"telegram_{existingChannels.Count(c => c.Type == "telegram") + 1}";

        var values = await PromptAsync(server, "Enter the Telegram API credentials and phone number.",
        [
            Field("api_id", "API ID", "Telegram API ID"),
            Field("api_hash", "API Hash", "Telegram API Hash"),
            Field("phone", "Phone", "Phone number in international format, e.g. 8210xxxxxxxx"),
        ], ct);

        if (values is null)
            return JsonSerializer.Serialize(new { status = "error", error = "Telegram setup requires a client that supports MCP Elicitation." }, McpJson.Tool);

        var sessionDir = Path.Combine(store.DataDirectory, "sessions");
        Directory.CreateDirectory(sessionDir);
        var sessionPath = Path.Combine(sessionDir, $"{id}.session");

        string? verificationCode = null;
        string? twoFactorPassword = null;
        WTelegram.Helpers.Log = (_, _) => { };

        using (var client = new WTelegram.Client(what => what switch
        {
            "api_id" => values["api_id"],
            "api_hash" => values["api_hash"],
            "phone_number" => values["phone"],
            "verification_code" => verificationCode ??= PromptSingleAsync(
                server, "verification_code", "Verification Code", "Telegram SMS or app code", "Enter the Telegram verification code.", ct).GetAwaiter().GetResult(),
            "password" => twoFactorPassword ??= PromptSingleAsync(
                server, "password", "Two-Factor Password", "Telegram two-factor password", "Enter the Telegram two-factor password if prompted.", ct, required: false).GetAwaiter().GetResult(),
            "session_pathname" => sessionPath,
            _ => null,
        }))
        {
            await client.LoginUserIfNeeded();
        }

        resolver.Remember(OutboxSecretResolver.BuildEnvVarName(id, "API_HASH"), values["api_hash"]);

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "telegram",
            Name = name ?? "Telegram",
            ApiId = values["api_id"],
            Phone = values["phone"],
        });

        return JsonSerializer.Serialize(new { status = "ok", channel_id = id }, McpJson.Tool);
    }

    static async Task<string> AddKakaoTalkAsync(
        McpServer server,
        ChannelStore store,
        OAuthTokenStore tokenStore,
        OutboxSecretResolver resolver,
        string? name,
        CancellationToken ct)
    {
        if (!BrowserOAuthFlow.IsSupportedOnCurrentHost())
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = $"{BrowserOAuthFlow.GetUnsupportedReason()} KakaoTalk setup currently requires a local desktop host.",
            }, McpJson.Tool);
        }

        var values = await PromptAsync(server, "Enter the Kakao REST API key and optional client secret.",
        [
            Field("api_key", "REST API Key", "Kakao REST API key"),
            Field("client_secret", "Client Secret", "Optional Kakao client secret", required: false),
        ], ct);

        if (values is null)
            return JsonSerializer.Serialize(new { status = "error", error = "KakaoTalk setup requires a client that supports MCP Elicitation." }, McpJson.Tool);

        var oauthFlow = new BrowserOAuthFlow();
        var redirectUri = oauthFlow.RedirectUri;
        var apiKey = values["api_key"];
        var clientSecret = values.GetValueOrDefault("client_secret");
        var authUrl = $"https://kauth.kakao.com/oauth/authorize?client_id={apiKey}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope=talk_message";
        var callback = await oauthFlow.RunWithMcpAsync(server, authUrl, "KakaoTalk", ct);

        if (!callback.IsSuccess || string.IsNullOrWhiteSpace(callback.Code))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = callback.ErrorDescription ?? "KakaoTalk authorization did not complete.",
            }, McpJson.Tool);
        }

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
            new FormUrlEncodedContent(tokenParams),
            ct);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = $"KakaoTalk token exchange failed: {tokenJson}",
            }, McpJson.Tool);
        }

        var tokenResult = JsonSerializer.Deserialize<KakaoTokenResponse>(tokenJson, McpJson.Store);
        if (tokenResult is null)
            return JsonSerializer.Serialize(new { status = "error", error = "KakaoTalk token exchange returned an unreadable response." }, McpJson.Tool);

        var existingChannels = await store.LoadAsync();
        var id = $"kakaotalk_{existingChannels.Count(c => c.Type == "kakaotalk") + 1}";
        var displayName = name ?? "KakaoTalk";

        await tokenStore.SaveAsync(id, new KakaoTokenData
        {
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn),
            RefreshTokenExpiresAt = tokenResult.RefreshTokenExpiresIn > 0
                ? DateTime.UtcNow.AddSeconds(tokenResult.RefreshTokenExpiresIn)
                : null,
        });

        resolver.Remember(OutboxSecretResolver.BuildEnvVarName(id, "API_KEY"), apiKey);
        if (!string.IsNullOrWhiteSpace(clientSecret))
            resolver.Remember(OutboxSecretResolver.BuildEnvVarName(id, "CLIENT_SECRET"), clientSecret);

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "kakaotalk",
            Name = displayName,
        });

        return JsonSerializer.Serialize(new
        {
            status = "ok",
            channel_id = id,
            oauth = "browser",
        }, McpJson.Tool);
    }

    static async Task<string> AddMicrosoftAsync(
        McpServer server,
        ChannelStore store,
        OAuthTokenStore tokenStore,
        OutboxSecretResolver resolver,
        string? name,
        CancellationToken ct)
    {
        if (!BrowserOAuthFlow.IsSupportedOnCurrentHost())
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = $"{BrowserOAuthFlow.GetUnsupportedReason()} Microsoft setup currently requires a local desktop host.",
            }, McpJson.Tool);
        }

        var values = await PromptAsync(server, "Enter the Microsoft client ID and client secret.",
        [
            Field("client_id", "Client ID", "Microsoft application (client) ID"),
            Field("client_secret", "Client Secret", "Microsoft client secret"),
        ], ct);

        if (values is null)
            return JsonSerializer.Serialize(new { status = "error", error = "Microsoft setup requires a client that supports MCP Elicitation." }, McpJson.Tool);

        var oauthFlow = new BrowserOAuthFlow();
        var redirectUri = oauthFlow.RedirectUri;
        var clientId = values["client_id"];
        var clientSecret = values["client_secret"];
        var scope = Uri.EscapeDataString("Mail.Send User.Read offline_access");
        var authUrl = $"https://login.microsoftonline.com/common/oauth2/v2.0/authorize?client_id={clientId}&response_type=code&redirect_uri={Uri.EscapeDataString(redirectUri)}&scope={scope}&response_mode=query";
        var callback = await oauthFlow.RunWithMcpAsync(server, authUrl, "Microsoft", ct);

        if (!callback.IsSuccess || string.IsNullOrWhiteSpace(callback.Code))
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = callback.ErrorDescription ?? "Microsoft authorization did not complete.",
            }, McpJson.Tool);
        }

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
            new FormUrlEncodedContent(tokenParams),
            ct);
        var tokenJson = await tokenResponse.Content.ReadAsStringAsync(ct);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            return JsonSerializer.Serialize(new
            {
                status = "error",
                error = $"Microsoft token exchange failed: {tokenJson}",
            }, McpJson.Tool);
        }

        var tokenResult = JsonSerializer.Deserialize<MicrosoftTokenResponse>(tokenJson, McpJson.Store);
        if (tokenResult is null)
            return JsonSerializer.Serialize(new { status = "error", error = "Microsoft token exchange returned an unreadable response." }, McpJson.Tool);

        string? userEmail = null;
        try
        {
            using var meRequest = new HttpRequestMessage(HttpMethod.Get, "https://graph.microsoft.com/v1.0/me");
            meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenResult.AccessToken);
            var meResponse = await httpClient.SendAsync(meRequest, ct);
            if (meResponse.IsSuccessStatusCode)
            {
                var meJson = await meResponse.Content.ReadAsStringAsync(ct);
                var meData = JsonSerializer.Deserialize<JsonElement>(meJson);
                if (meData.TryGetProperty("mail", out var mailProp) && mailProp.ValueKind == JsonValueKind.String)
                    userEmail = mailProp.GetString();
                if (string.IsNullOrWhiteSpace(userEmail) && meData.TryGetProperty("userPrincipalName", out var upnProp) && upnProp.ValueKind == JsonValueKind.String)
                    userEmail = upnProp.GetString();
            }
        }
        catch
        {
        }

        var existingChannels = await store.LoadAsync();
        var id = $"microsoft_{existingChannels.Count(c => c.Type == "microsoft") + 1}";
        var displayName = name ?? "Microsoft";

        await tokenStore.SaveAsync(id, new MicrosoftTokenData
        {
            AccessToken = tokenResult.AccessToken,
            RefreshToken = tokenResult.RefreshToken ?? string.Empty,
            ExpiresAt = DateTime.UtcNow.AddSeconds(tokenResult.ExpiresIn),
        });

        resolver.Remember(OutboxSecretResolver.BuildEnvVarName(id, "CLIENT_SECRET"), clientSecret);

        await store.AddAsync(new ChannelMetadata
        {
            Id = id,
            Type = "microsoft",
            Name = displayName,
            From = userEmail,
            Provider = "microsoft",
            ClientId = clientId,
        });

        return JsonSerializer.Serialize(new
        {
            status = "ok",
            channel_id = id,
            oauth = "browser",
        }, McpJson.Tool);
    }

    static SecretFieldRequest Field(string name, string title, string description, bool required = true) =>
        new(name, "__unused__", title, description, "", required);

    static async Task<Dictionary<string, string>?> PromptAsync(
        McpServer server,
        string message,
        IReadOnlyList<SecretFieldRequest> fields,
        CancellationToken ct)
    {
        if (server.ClientCapabilities?.Elicitation is null)
            return null;

        try
        {
            var result = await server.ElicitAsync(new ElicitRequestParams
            {
                Message = message,
                RequestedSchema = new ElicitRequestParams.RequestSchema
                {
                    Properties = fields.ToDictionary(
                        f => f.FieldName,
                        f => (ElicitRequestParams.PrimitiveSchemaDefinition)new ElicitRequestParams.StringSchema
                        {
                            Title = f.Title,
                            Description = f.Description,
                            MinLength = f.Required ? 1 : null,
                        }),
                    Required = fields.Where(static f => f.Required).Select(static f => f.FieldName).ToArray(),
                },
            }, ct);

            if (!result.IsAccepted || result.Content is null)
                return null;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
            {
                if (!result.Content.TryGetValue(field.FieldName, out var value))
                {
                    if (field.Required)
                        return null;
                    continue;
                }

                var text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
                if (string.IsNullOrWhiteSpace(text))
                {
                    if (field.Required)
                        return null;
                    continue;
                }

                values[field.FieldName] = text;
            }

            return values;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    static async Task<string?> PromptSingleAsync(
        McpServer server,
        string fieldName,
        string title,
        string description,
        string message,
        CancellationToken ct,
        bool required = true)
    {
        var values = await PromptAsync(server, message, [Field(fieldName, title, description, required)], ct);
        return values?.GetValueOrDefault(fieldName);
    }
}
