# FieldCure.Mcp.Outbox

**Multi-channel messaging MCP server** — send messages through Slack, Telegram, Email (Gmail, Naver, Microsoft Graph API), KakaoTalk, and Discord with a single `send_message` tool. One install, one interface, multiple channels.

<!-- mcp-name: io.github.fieldcure/outbox -->

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

```json
{
  "mcpServers": {
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
| `remove_channel` | Remove a channel and its data | Required |

## Channels

| Channel | Protocol | Setup |
|---------|----------|-------|
| **Slack** | Web API (`chat.postMessage`) | Bot Token |
| **Telegram** | Client API (WTelegramClient) | API ID + Hash + SMS |
| **Gmail** | SMTP (smtp.gmail.com:587) | App password |
| **Naver** | SMTP (smtp.naver.com:465) | App password |
| **Microsoft** | Graph API (`/me/sendMail`) | OAuth 2.0 |
| **Custom SMTP** | User-defined SMTP server | Username + password |
| **KakaoTalk** | Kakao REST API | OAuth 2.0 |
| **Discord** | Webhook API | Webhook URL |

## Credential Storage

Credentials are resolved in this order:

1. **In-memory cache** (from prior elicitation or env var pickup in the current session)
2. **Environment variable** `OUTBOX_{CHANNEL_ID}_{FIELD}` (e.g. `OUTBOX_SLACK_GENERAL_BOT_TOKEN`)
3. **`channels.json`** (plaintext, written by the CLI setup flow)
4. **MCP Elicitation** (when the client supports it)

OAuth dynamic tokens (Microsoft, KakaoTalk) are persisted separately in `tokens.json` with current-user-only file permissions (Windows ACL / Unix `0600`). Channel setup never passes credentials through MCP stdio.

Plaintext storage in `channels.json` is a deliberate local-trust choice — same same-user security boundary as `~/.docker/config.json` and `~/.config/gh/hosts.yml`. For shared hosts, CI, or headless deployments, set the env vars directly and leave the secret fields of `channels.json` empty. See [ADR-001](https://github.com/fieldcure/fieldcure-assiststudio/blob/main/docs/ADR-001-MCP-Credential-Management.md) Principle 2.

## Requirements

- [.NET 8.0 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) or later

## See Also

Part of the [AssistStudio ecosystem](https://github.com/fieldcure/fieldcure-assiststudio#packages).

- [GitHub](https://github.com/fieldcure/fieldcure-mcp-outbox)
- [MCP Specification](https://modelcontextprotocol.io)
