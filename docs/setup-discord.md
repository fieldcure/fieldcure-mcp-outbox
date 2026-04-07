# Discord Setup

Sends messages to a Discord channel via Webhook URL.

## Prerequisites

1. A Discord server where you have **Manage Webhooks** permission
2. A text channel to send messages to

## Setup Steps

1. Open your Discord server and go to the text channel you want to use
2. Click the **gear icon** (Edit Channel) next to the channel name
3. Go to **Integrations** → **Webhooks**
4. Click **New Webhook**
5. (Optional) Change the webhook name and avatar
6. Click **Copy Webhook URL** — the URL starts with `https://discord.com/api/webhooks/` or `https://discordapp.com/api/webhooks/`

## Add Channel

```bash
fieldcure-mcp-outbox add discord
```

You will be prompted for:
- **Channel name:** A display name for this channel (e.g. `general`, `alerts`)
- **Webhook URL:** The URL copied from step 6

## Notes

- Messages up to 2,000 characters are sent as plain text
- Longer messages are automatically split into embeds (up to 40,960 characters)
- Messages appear with the username "AssistStudio" in Discord
- Rate limiting is handled automatically with one retry
