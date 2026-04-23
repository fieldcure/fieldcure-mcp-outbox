# Release Notes

## v2.1.2 (2026-04-23)

### Fixed

- **KakaoTalk / Microsoft static credentials survive a server restart** — the OAuth add flows (both the legacy CLI `add kakaotalk|microsoft` and the v2.1 MCP `add_channel` path) collected the REST API key / client secret to complete the authorization-code exchange, cached them in `OutboxSecretResolver`'s in-memory map, and discarded them. On the next server start the cache was empty and `SendMessageTool` re-elicited even though the user had just finished the setup flow. Persist the same values into `ChannelMetadata.ApiKey` / `ChannelMetadata.ClientSecret` so `SendMessageTool` resolves them through the existing `LegacyValue` fallback in `channels.json`. OAuth channels now follow the same local-trust persistence convention that Slack/Discord/SMTP/Gmail/Telegram static secrets already use. Channels added before this release still elicit on send — re-add or populate the fields manually to skip the prompt.

## v2.1.1 (2026-04-20)

- Update MCP package metadata to the latest `server.json` format for NuGet and VS Code integration.

## v2.1.0 (2026-04-20)

### Added

- **OAuth channels complete inside `add_channel`** — KakaoTalk and Microsoft no longer require the `fieldcure-mcp-outbox add kakaotalk|microsoft` CLI route. The MCP tool elicits the static app credentials, opens the user's default browser on the MCP server host, listens on `http://localhost:9876/callback`, and races the listener against a second `BooleanSchema` elicitation ("Sign-in complete?") so the user can confirm or cancel from the MCP client UI. The elicit message surfaces the authorization URL so the user can open it manually if the automatic launch did not work. The CLI `add kakaotalk|microsoft` commands stay for diagnostics but are no longer the primary path.
- **`IElicitGate` abstraction** (`Interaction/IElicitGate.cs`, `McpServerElicitGate.cs`) — wraps the subset of `McpServer` that Outbox credential and OAuth flows need, so `OutboxSecretResolver`, the add-channel tool, and `BrowserOAuthFlow` are all unit-testable without constructing a real server.
- **`OutboxSecretResolverTests`** — three baseline tests lock in the cache → env var → legacy metadata → elicitation lookup order using a fake gate.
- **`BrowserOAuthFlow.IsSupportedOnCurrentHost()` WSL support** — the pre-check now treats WSL as supported when `WSL_DISTRO_NAME` is set, because the `wslu` package lets `xdg-open` delegate to the Windows host browser even when `DISPLAY` is unset.

### Changed

- **`OutboxSecretResolver.ResolveFieldsAsync`** now takes `IElicitGate?` instead of `McpServer`. The lookup order and elicitation schema are unchanged.
- **`SendMessageTool`** builds a `McpServerElicitGate` around the injected server before resolving per-channel secrets, mirroring the add-channel path.
- **`BrowserOAuthFlow.RunWithGateAsync`** replaces the previous direct-`McpServer` signature. `RunWithConsoleAsync` keeps the CLI path intact.
- **Browser confirmation prompt wording** — the second elicitation no longer claims the browser "should already be open". `Process.Start(UseShellExecute=true)` returns success as soon as the shell dispatches the URL and cannot guarantee a window appeared, so the prompt always surfaces the authorization URL and phrases the launch status as "a browser launch request was dispatched".
- **Callback completion page** — `WaitForCallbackAsync` writes the "Authorization successful" HTML back to the browser with `CancellationToken.None`, so a late grace-window timeout cannot truncate the page the user sees.
- **Telegram setup** — added an explanatory comment on the `WTelegram.Client` callback's `GetAwaiter().GetResult()` bridge: the callback is synchronous by design and runs off the stdio loop, so the blocking wait is safe.

### Deployment note

Same host requirements as v2.0 for OAuth channels: the MCP server host needs a local default browser and must be able to accept the `localhost:9876` callback. See [ADR-001 Phase 3c](https://github.com/fieldcure/fieldcure-assiststudio/blob/main/docs/ADR-001-MCP-Credential-Management.md) — headless / remote / container hosts still need `v2.2` device-code flow (planned).

---

## v2.0.0 (2026-04-17)

### Breaking

- **Remove Windows Credential Manager dependency** — credentials are now stored in `channels.json` alongside channel metadata instead of Windows Credential Manager (advapi32.dll). Existing channels must be re-added via `add` command. This makes the package genuinely cross-platform. Plaintext storage in `channels.json` is a deliberate local-trust choice documented in [ADR-001](https://github.com/fieldcure/fieldcure-assiststudio/blob/main/docs/ADR-001-MCP-Credential-Management.md) Principle 2 (local-trust exception) — it matches the same-user security boundary already used for `tokens.json`, and the same convention found in `~/.docker/config.json` and `~/.config/gh/hosts.yml`. Production / shared / headless deployments should set `OUTBOX_{id}_{field}` env vars instead and leave the secret fields of `channels.json` empty; `OutboxSecretResolver` reads `channels.json` as the last fallback only (cache → env var → `channels.json` → Elicitation).
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
