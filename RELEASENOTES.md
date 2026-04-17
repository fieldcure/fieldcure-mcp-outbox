# Release Notes

## v2.0.0 (2026-04-17)

### Breaking

- **Remove Windows Credential Manager dependency** — credentials are now stored in `channels.json` alongside channel metadata instead of Windows Credential Manager (advapi32.dll). Existing channels must be re-added via `add` command. This makes the package genuinely cross-platform.
- **`ChannelFactory.Create()` signature change** — `CredentialManager` parameter removed; credentials read from `ChannelMetadata` directly.

### Changed

- **Cross-platform support** — `net8.0` TFM is no longer misleading; no Windows-specific P/Invoke remains
- **Simplified credential flow** — `add` CLI stores credentials directly in `channels.json`; `remove` CLI deletes the channel entry (no separate credential cleanup needed)

### Removed

- `CredentialManager.cs` (Windows Credential Manager P/Invoke wrapper)
- `DeleteChannelCredentials()` in `SetupRunner` (credentials removed with channel entry)

### Migration

Existing channels have metadata but no credentials in `channels.json`. Run the migration script to copy credentials from Windows Credential Manager:

```powershell
# From the repo root, or download the script from GitHub
.\scripts\migrate-credentials.ps1
```

The script is idempotent — skips channels that already have credentials. Alternatively, re-add channels manually:

```bash
fieldcure-mcp-outbox remove <channel-id>
fieldcure-mcp-outbox add <type>
```

---

## v1.1.0 (2026-04-14)

### Changed

- **Centralize `JsonSerializerOptions`** — extract shared `McpJson.Options` to eliminate per-tool serializer configuration duplication

---

## v1.0.0 (2026-04-07)

- **New: Discord webhook channel** — send messages to Discord channels via webhook URL; supports message splitting with embeds for long messages, rate limit handling with automatic retry
- **New: `discordapp.com` webhook URL support** — accept both `discord.com` and `discordapp.com` webhook URL prefixes
- **Improvement: Channel list sorted alphabetically** — `list_channels` tool and CLI `list` command now return channels sorted by ID
- **Fix: Stale channel type list** — remove non-existent `outlook` / `microsoft365` from tool descriptions; add `discord` and `microsoft`
- **Fix: HttpResponseMessage leak** — properly dispose HTTP responses in Discord channel on rate limit retry
- **Cleanup: Remove unused `TcpPortFinder`** — dead code removed from KakaoTalk setup
- **Docs: Fix `SmtpPresets` doc comment** — remove incorrect claim that all presets use port 587

## v0.4.1 (2026-04-03)

- `ModelContextProtocol` 1.1.0 → 1.2.0

## v0.4.0 (2026-03-31)

- **New: Microsoft Graph API channel** — send email via `POST /me/sendMail` with OAuth 2.0; supports both personal (@outlook.com, @hotmail.com, @live.com) and work/school (Microsoft 365) accounts
- **Remove: Outlook / Microsoft 365 SMTP presets** — deprecated by Microsoft (basic auth no longer supported); replaced by the new `microsoft` channel type
- **Fix: Naver SMTP preset test** — correct expected port from 587 to 465

## v0.3.0 (2026-03-31)

- **Fix: Naver SMTP authentication** — use Naver ID only (without `@naver.com`) as SMTP username; change port from 587/STARTTLS to 465/SSL per Naver official settings
- **Fix: Naver app password hint** — show correct format `[XXXXXXXXXXXX]` (12 uppercase letters and digits) instead of Gmail-style `[xxxx xxxx xxxx xxxx]`
- **Fix: Slack bot invite instructions** — replace generic `@YourBotName` with `@YourAppName` and reference the App Name from step 2
- **Docs: Naver setup guide** — add detailed prerequisites, SMTP/POP3 activation, 2-Step Verification, and app password generation steps
- **Docs: Outlook / Microsoft 365** — mark as unverified with note to open an issue if problems occur

## v0.2.0 (2026-03-30)

- **Fix: stdio transport corruption** — move `Console.OutputEncoding = UTF8` to CLI mode only; was corrupting MCP JSON-RPC responses when set before server startup
- **Fix: ServerInfo.Version from assembly** — derive version from `AssemblyInformationalVersionAttribute` instead of hardcoding, so handshake reports the correct NuGet version
- **CI** — add GitHub Actions build & test workflow

## v0.1.0 (2026-03-29)

Initial release.

- **4 messaging channels** — Slack, Telegram, SMTP (Gmail/Outlook/M365/Naver/Custom), KakaoTalk
- **4 MCP tools** — `list_channels`, `add_channel`, `send_message`, `remove_channel`
- **Secure credential storage** — Windows Credential Manager (DPAPI); no secrets in config or conversation history
- **Interactive console setup** — launched as subprocess for secure credential entry
- **SMTP provider menu** — preset selection (Gmail/Outlook/M365/Naver) or custom SMTP; shortcut commands (`add gmail`, `add outlook`, etc.)
- **KakaoTalk OAuth** — fixed localhost:9876 callback with automatic token refresh
- **Telegram Client API** — WTelegramClient with session persistence; one-time phone verification
- **Slack Bot API** — `chat.postMessage` with bot invite reminder
- **UTF-8 console output** — ASCII-safe UI for Windows Terminal and cmd compatibility
