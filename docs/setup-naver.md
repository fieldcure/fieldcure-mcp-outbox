# Naver Setup

Sends emails via Naver SMTP (`smtp.naver.com:465` with SSL).

## Prerequisites

1. A Naver account with POP3/SMTP or IMAP/SMTP enabled
2. **2-Step Verification** enabled on your Naver account
3. An **App Password** (NOT your regular Naver password)

## Enabling SMTP

1. Go to [Naver Mail](https://mail.naver.com) → **환경설정** → **POP3/IMAP 설정**
2. Under **POP3/SMTP 사용** (or **IMAP/SMTP 설정**), select **사용함**
3. Click **저장**

> **Note:** If POP3/SMTP is unused for 90 days, Naver automatically disables it.

## Enabling 2-Step Verification & Getting an App Password

1. Go to [Naver ID Security Settings](https://nid.naver.com/user2/help/myInfoV2?m=viewSecurity&lang=ko_KR) (네이버ID → **보안설정**)
2. Click **설정** next to **2단계 인증**
3. Re-enter your password, select your mobile device, and approve the push notification on the Naver app
4. After 2-Step Verification is enabled, click **보안설정 확인** → **보안설정더보기** to go to the management page
5. Under **애플리케이션 비밀번호 관리**, select **직접 입력** → enter a name (e.g. `outbox`) → click **생성하기**
6. Copy the 12-character app password (uppercase letters and digits, e.g. `AB3CDE7FGHKL`, shown only once)

> **Note:** 2-Step Verification requires the Naver app (v8.6.0+) installed on your smartphone.

## Add Channel

```bash
fieldcure-mcp-outbox add naver
```

You will be prompted for:
- **Naver ID:** Your Naver email address (e.g. `yourname@naver.com`)
- **App password:** The 12-character uppercase app password from step 6 (NOT your login password)
