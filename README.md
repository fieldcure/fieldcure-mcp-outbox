# FieldCure MCP Outbox Server

[![NuGet](https://img.shields.io/nuget/v/FieldCure.Mcp.Outbox)](https://www.nuget.org/packages/FieldCure.Mcp.Outbox)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/fieldcure/fieldcure-mcp-outbox/blob/main/LICENSE)

A multi-channel messaging [Model Context Protocol (MCP)](https://modelcontextprotocol.io) server that sends messages through Slack, Telegram, Email (Gmail, Naver, Microsoft Graph API), KakaoTalk, and Discord. Built with C# and the official [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk).

## Features

- **Multiple messaging channels** — Slack, Telegram, Email (Gmail, Naver, Microsoft Graph API), KakaoTalk, Discord
- **4 MCP tools** — `list_channels`, `add_channel`, `send_message`, `remove_channel`
- **Cross-platform credential flow** — runtime secrets can come from env vars or MCP elicitation; OAuth tokens are stored separately for refresh lifecycle
- **CLI channel setup** — interactive console for browser/OAuth flows where applicable
- **SMTP presets** — Gmail, Naver with one-command setup
- **Microsoft Graph API** — OAuth 2.0 browser flow for Outlook / M365 email with automatic token refresh
- **KakaoTalk OAuth** — localhost callback flow with automatic token refresh
- **Telegram Client API** — send to Saved Messages via WTelegramClient
- **Stdio transport** — standard MCP subprocess model via JSON-RPC over stdin/stdout

## Why Outbox?

Existing MCP servers are channel-specific — one for Slack, another for Gmail, yet another for Telegram. Each requires separate installation, configuration, and the LLM must know which tool to call for each channel.

Outbox takes a different approach:

- **One tool, multiple channels** — `send_message` abstracts away channel differences. The LLM doesn't need to know Slack API vs SMTP vs Kakao REST.
- **Credential isolation** — OAuth refresh tokens live in `tokens.json` with current-user-only file permissions; MCP tool flows can resolve secrets at runtime via env vars or elicitation.
- **Single install** — `dotnet tool install -g` gives you 4 channels. No need to install and configure separate servers per channel.
- **KakaoTalk support** — Currently the only MCP server with KakaoTalk messaging, essential for Korean users.

## Installation

### dotnet tool (recommended)

```bash
dotnet tool install -g FieldCure.Mcp.Outbox
```

After installation, the `fieldcure-mcp-outbox` command is available globally.

### From source

```bash
git clone https://github.com/fieldcure/fieldcure-mcp-outbox.git
cd fieldcure-mcp-outbox
dotnet build
```

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Cross-platform (Windows, Linux, macOS)

## Configuration

### Claude Desktop

Add to `claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "outbox": {
      "command": "fieldcure-mcp-outbox"
    }
  }
}
```

### VS Code (Copilot)

Add to `.vscode/mcp.json`:

```json
{
  "servers": {
    "outbox": {
      "command": "fieldcure-mcp-outbox"
    }
  }
}
```

### From source (without dotnet tool)

```json
{
  "mcpServers": {
    "outbox": {
      "command": "dotnet",
      "args": [
        "run",
        "--project", "C:\\path\\to\\fieldcure-mcp-outbox\\src\\FieldCure.Mcp.Outbox"
      ]
    }
  }
}
```

## Tools

| Tool | Description | Confirmation |
|------|-------------|:------------:|
| `list_channels` | List all configured messaging channels | — |
| `add_channel` | Add a new channel (MCP elicitation for all channel types; Microsoft/KakaoTalk require a local browser on the MCP host) | — |
| `send_message` | Send a message through a configured channel | Required |
| `remove_channel` | Remove a channel and its stored credentials | Required |

## Channels

OAuth tokens for Microsoft/KakaoTalk are stored in `tokens.json` so the server can refresh them across runs (current-user-only file permissions — Windows ACL / Unix `0600`).

Static secrets (Slack bot tokens, Discord webhook URLs, SMTP passwords, etc.) resolve at send time in this order: in-memory cache → env var `OUTBOX_{CHANNEL_ID}_{FIELD}` → plaintext in `channels.json` → MCP elicitation. The CLI `add_channel` flow writes directly into `channels.json` for a zero-configuration local experience. This is an intentional local-trust choice with the same same-user boundary as `tokens.json`, documented in [ADR-001](https://github.com/fieldcure/fieldcure-assiststudio/blob/main/docs/ADR-001-MCP-Credential-Management.md) Principle 2. For shared hosts, CI, or headless deployments, set the env vars explicitly and leave `channels.json` secret fields empty.

| Channel | Protocol | Setup |
|---------|----------|-------|
| Slack | Web API (`chat.postMessage`) | [Guide](docs/setup-slack.md) |
| Telegram | Client API (WTelegramClient) | [Guide](docs/setup-telegram.md) |
| Gmail | SMTP | [Guide](docs/setup-gmail.md) |
| Naver | SMTP | [Guide](docs/setup-naver.md) |
| Microsoft | Graph API (`/me/sendMail`) | [Guide](docs/setup-microsoft.md) |
| KakaoTalk | Kakao REST API | [Guide](docs/setup-kakaotalk.md) |
| Discord | Webhook API | [Guide](docs/setup-discord.md) |
| Custom SMTP | User-defined | [Guide](docs/setup-custom-smtp.md) |

### Static Secret Environment Variables

When a channel requires a static secret at send time, Outbox looks for an environment variable named:

```text
OUTBOX_<CHANNEL_ID>_<FIELD>
```

Examples:

- `OUTBOX_MICROSOFT_1_CLIENT_SECRET`
- `OUTBOX_KAKAOTALK_1_API_KEY`
- `OUTBOX_KAKAOTALK_1_CLIENT_SECRET`
- `OUTBOX_SMTP_GMAIL_1_PASSWORD`

If the variable is unset and the MCP client supports elicitation, Outbox prompts for it interactively and caches it for the current session.

## CLI Commands

```bash
fieldcure-mcp-outbox                      # Start MCP server (stdio)
fieldcure-mcp-outbox add slack            # Add Slack channel
fieldcure-mcp-outbox add telegram         # Add Telegram channel
fieldcure-mcp-outbox add gmail            # Add Gmail SMTP channel
fieldcure-mcp-outbox add naver            # Add Naver SMTP channel
fieldcure-mcp-outbox add smtp             # Add custom SMTP channel
fieldcure-mcp-outbox add microsoft        # Add Microsoft (Outlook/M365) channel
fieldcure-mcp-outbox add kakaotalk        # Add KakaoTalk channel
fieldcure-mcp-outbox add discord          # Add Discord channel
fieldcure-mcp-outbox list                 # List configured channels
fieldcure-mcp-outbox remove <id>          # Remove a channel
```

## Data Storage

| Data | Location |
|------|----------|
| Channel metadata | `%LOCALAPPDATA%\FieldCure\Mcp.Outbox\channels.json` |
| OAuth tokens | `%LOCALAPPDATA%\FieldCure\Mcp.Outbox\tokens.json` |
| Telegram sessions | `%LOCALAPPDATA%\FieldCure\Mcp.Outbox\sessions\` |

`tokens.json` is stored in plain JSON and protected by filesystem permissions:

- Windows: ACL restricted to the current user
- Linux/macOS: mode `0600`

This protects against other local users on the same machine, but not against compromise of the same OS user account.

## Project Structure

```
src/FieldCure.Mcp.Outbox/
├── Program.cs                  # Entry point: MCP server vs CLI branching
├── Channels/
│   ├── IChannel.cs             # Channel interface + SendRequest/SendResult
│   ├── ChannelFactory.cs       # Channel instantiation by type
│   ├── SlackChannel.cs         # Slack Web API
│   ├── TelegramChannel.cs      # WTelegramClient
│   ├── SmtpChannel.cs          # MailKit SMTP
│   ├── MicrosoftChannel.cs     # Microsoft Graph API
│   ├── KakaoTalkChannel.cs     # Kakao REST API
│   └── DiscordChannel.cs       # Discord Webhook API
├── Tools/
│   ├── ListChannelsTool.cs     # list_channels
│   ├── AddChannelTool.cs       # add_channel (elicitation + browser OAuth for Kakao/Microsoft)
│   ├── RemoveChannelTool.cs    # remove_channel
│   └── SendMessageTool.cs      # send_message
├── Interaction/
│   ├── IElicitGate.cs          # Minimal MCP elicitation surface for tests
│   └── McpServerElicitGate.cs  # Production adapter around McpServer
├── OAuth/
│   └── BrowserOAuthFlow.cs     # Localhost callback + Process.Start + elicit race (MCP and CLI entry points)
├── Credentials/
│   └── OutboxSecretResolver.cs # cache → env → channels.json → elicitation
├── Setup/
│   ├── SetupRunner.cs          # CLI router (diagnostic path)
│   ├── ConsoleHelper.cs        # Masked input, prompts
│   ├── SlackSetup.cs
│   ├── TelegramSetup.cs
│   ├── SmtpSetup.cs
│   ├── MicrosoftSetup.cs
│   ├── KakaoTalkSetup.cs
│   └── DiscordSetup.cs
└── Configuration/
    ├── ChannelStore.cs         # channels.json persistence (metadata + static-secret fallback)
    ├── OAuthTokenStore.cs      # tokens.json persistence (OAuth access/refresh, user-only file perms)
    └── SmtpPresets.cs          # SMTP preset definitions
```

## Development

```bash
# Build
dotnet build

# Test
dotnet test

# Pack as dotnet tool
dotnet pack src/FieldCure.Mcp.Outbox -c Release
```

## See Also

Part of the [AssistStudio ecosystem](https://github.com/fieldcure/fieldcure-assiststudio#packages).

## License

[MIT](LICENSE)
