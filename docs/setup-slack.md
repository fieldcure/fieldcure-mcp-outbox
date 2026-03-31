# Slack Setup

Sends messages to a Slack channel via Bot API.

## Prerequisites

1. A Slack workspace (free plan is fine, single-person workspace works)
2. A Slack App with Bot Token

## Setup Steps

1. Go to [api.slack.com/apps](https://api.slack.com/apps) and click **Create New App** → **From scratch**
2. Enter an App Name (e.g. "AssistStudio") and select your workspace
3. In the left menu, go to **OAuth & Permissions**
4. Under **Bot Token Scopes**, click **Add an OAuth Scope** and add `chat:write`
5. Scroll up and click **Install to Workspace** → **Allow**
6. Copy the **Bot User OAuth Token** (`xoxb-...`)
7. **Important:** In your Slack workspace, invite the bot to the channel where you want to send messages. Type `/invite @YourAppName` in the channel (e.g., `/invite @AssistStudio` if you named your app "AssistStudio" in step 2).

## Add Channel

```bash
fieldcure-mcp-outbox add slack
```

You will be prompted for:
- **Channel name:** The default Slack channel name (e.g. `general`, `dev-alerts`)
- **Bot Token:** The `xoxb-...` token from step 6

> **Note:** The bot must be invited to the channel before it can send messages.
> Type `/invite @YourAppName` in the channel (use the App Name from step 2).

![Slack Setup](setup-slack.png)
