# Release Notes

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
