# Microsoft (Outlook / Microsoft 365) Setup

Sends emails via Microsoft Graph API (`POST /me/sendMail`) with OAuth 2.0.

> **Note:** Microsoft has [deprecated basic authentication](https://learn.microsoft.com/en-us/exchange/clients-and-mobile-in-exchange-online/deprecation-of-basic-authentication-exchange-online) for SMTP. This channel uses **Microsoft Graph API** with OAuth 2.0 instead of SMTP.

For both personal accounts (@outlook.com, @hotmail.com, @live.com) and business/organization Microsoft 365 accounts.

## Prerequisites

1. A Microsoft account (personal or work/school)
2. An Azure Entra ID app registration with **Mail.Send** permission

## Setting up Azure Entra ID App

1. Go to [entra.microsoft.com](https://entra.microsoft.com) → **Identity** → **App registrations** → **New registration**
2. Enter a name (e.g. `outbox`)
3. Under **Supported account types**, select **Accounts in any organizational directory and personal Microsoft accounts** (to support both personal and work accounts)
4. Under **Redirect URI**, select **Web** and enter `http://localhost:9876/callback`
5. Click **Register**
6. Note the **Application (client) ID** on the overview page
7. Go to **API permissions** → **Add a permission** → **Microsoft Graph** → **Delegated permissions** → search `Mail.Send` → check it → **Add permissions**
8. Go to **Certificates & secrets** → **New client secret** → enter a description → **Add** → copy the **Value** (shown only once)

## Add Channel

```bash
fieldcure-mcp-outbox add microsoft
```

You will be prompted for:
- **Client ID:** The application (client) ID from step 6
- **Client Secret:** The client secret value from step 8
- A browser window will open for Microsoft sign-in and consent

![Microsoft Setup](setup-microsoft.png)
