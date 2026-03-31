# Telegram Setup

Sends messages to yourself (Saved Messages) via Telegram Client API.

## Prerequisites

1. A Telegram account with a registered phone number
2. Telegram API credentials (API ID and API Hash)

## Setup Steps

1. Go to [my.telegram.org](https://my.telegram.org) and log in with your phone number
2. Click **API development tools**
3. Fill in the form:
   - **App title:** Any name (e.g. "AssistStudio")
   - **Short name:** 5-32 alphanumeric characters (e.g. "AStudio")
   - **Platform:** Desktop
   - **URL / Description:** Leave empty
4. Click **Create application**
5. Note down the **API ID** (numeric) and **API Hash** (string)

> **Note:** Test/Production configuration values on the same page are NOT needed.

## Add Channel

```bash
fieldcure-mcp-outbox add telegram
```

You will be prompted for:
- **API ID:** The numeric ID from step 5
- **API Hash:** The hash string from step 5
- **Phone:** Your phone number in international format (e.g. `8210xxxxxxxx`)
- **Verification code:** Enter the code sent to your Telegram app or via SMS

A session file is created after successful authentication. Subsequent use does not require re-authentication.

![Telegram Setup](setup-telegram.png)
