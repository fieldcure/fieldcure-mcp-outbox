# Gmail Setup

Sends emails via Gmail SMTP.

## Prerequisites

1. A Gmail account with **2-Step Verification** enabled
2. An **App Password** (NOT your regular Google password)

## Getting an App Password

1. Go to [myaccount.google.com/security](https://myaccount.google.com/security)
2. Ensure 2-Step Verification is ON
3. Go to [myaccount.google.com/apppasswords](https://myaccount.google.com/apppasswords)
4. Enter an app name (e.g. "Outbox") → **Create**
5. Copy the 16-character password (e.g. `abcd efgh ijkl mnop`)
6. Enter it without spaces when prompted

## Add Channel

```bash
fieldcure-mcp-outbox add gmail
```

You will be prompted for:
- **Gmail address:** Your Gmail email address
- **App password:** The 16-character app password
