# KakaoTalk Setup

Sends messages to yourself via KakaoTalk "Send to Me" API.

## Prerequisites

1. A Kakao account
2. A Kakao Developers application with Messaging API enabled

## Setup Steps

1. Go to [developers.kakao.com](https://developers.kakao.com) and log in
2. Go to **My Applications** → **Add Application** → Enter app name → Save
3. Go to **Product Settings** → **Kakao Login** → Set status to **ON**
4. Go to **Product Settings** → **Kakao Login** → **Consent Items**
   - Find **카카오톡 메시지 전송** (`talk_message`) under **접근권한**
   - Click **설정** → Select **이용 중 동의** → Enter a purpose (e.g. "AI agent notification") → Save
5. Go to **App Settings** → **Platform Key** → Click on your REST API key
   - Copy the **REST API Key**
   - Under **카카오 로그인 리다이렉트 URI**, add: `http://localhost:9876/callback`
   - Under **클라이언트 시크릿**, copy the **Client Secret** code for 카카오 로그인
   - Ensure 활성화 is set to **ON**
   - Click **저장**

> **Critical:** The redirect URI must be exactly `http://localhost:9876/callback` — no trailing slash.

## Add Channel

```bash
fieldcure-mcp-outbox add kakaotalk
```

You will be prompted for:
- **REST API Key:** From step 5
- **Client Secret:** From step 5 (press Enter to skip if disabled, but new apps have it enabled by default)

After entering credentials:
1. Your browser will automatically open the Kakao login page
2. Log in with your Kakao account and grant permissions
3. The browser will show "Authorization successful!" — you can close it
4. The console will confirm the channel was added

## Token Management

- Access tokens are automatically refreshed when expired
- If the refresh token also expires, `send_message` will return an error
- Re-run `add kakaotalk` to re-authorize

![KakaoTalk Setup](setup-kakaotalk.png)
