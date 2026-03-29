# FieldCure.Mcp.Outbox

**Multi-channel messaging MCP server** — send messages through Slack, Telegram, Email (SMTP), and KakaoTalk from any MCP client. Secrets stored securely in Windows Credential Manager.

## Install

```bash
dotnet tool install -g FieldCure.Mcp.Outbox
```

## Quick Start

```bash
# Add a Slack channel
fieldcure-mcp-outbox add slack

# Add a Gmail channel
fieldcure-mcp-outbox add gmail

# Start MCP server
fieldcure-mcp-outbox
```

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

## Tools (4)

| Tool | Description | Confirmation |
|------|-------------|:------------:|
| `list_channels` | List all configured messaging channels | — |
| `add_channel` | Add a new channel (opens setup console) | — |
| `send_message` | Send a message through a channel | Required |
| `remove_channel` | Remove a channel and credentials | Required |

## Channels

| Channel | Protocol | Setup |
|---------|----------|-------|
| **Slack** | Web API (`chat.postMessage`) | Bot Token |
| **Telegram** | Client API (WTelegramClient) | API ID + Hash + SMS |
| **Gmail** | SMTP (smtp.gmail.com:587) | App password |
| **Outlook** | SMTP (smtp-mail.outlook.com:587) | App password |
| **Microsoft 365** | SMTP (smtp.office365.com:587) | App password |
| **Naver** | SMTP (smtp.naver.com:587) | App password |
| **Custom SMTP** | User-defined SMTP server | Username + password |
| **KakaoTalk** | Kakao REST API | OAuth 2.0 |

## Security

- Secrets stored in **Windows Credential Manager** (DPAPI) — never exposed to LLM
- Channel setup runs in a **separate console process** — credentials never pass through MCP stdio
- `send_message` and `remove_channel` require **user confirmation** in the client

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Windows (required for Credential Manager)

## Links

- [GitHub](https://github.com/fieldcure/fieldcure-mcp-outbox)
- [MCP Specification](https://modelcontextprotocol.io)
