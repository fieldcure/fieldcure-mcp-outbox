# Release Notes

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
