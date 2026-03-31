# Custom SMTP Setup

Sends emails via a user-defined SMTP server.

## Add Channel

```bash
fieldcure-mcp-outbox add smtp
```

You will be prompted for:
- **Host:** SMTP server hostname
- **Port:** SMTP port (default: 587)
- **Use TLS:** y/n (default: y)
- **Username:** SMTP username
- **Password:** SMTP password

![SMTP Setup](setup-smtp.png)
