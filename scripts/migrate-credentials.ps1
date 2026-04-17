<#
.SYNOPSIS
    One-time migration: moves channel credentials from Windows Credential Manager
    to channels.json for Outbox v2.0.0.
.DESCRIPTION
    Reads credentials stored by v1.x (CredentialManager / advapi32) and writes
    them into the corresponding ChannelMetadata fields in channels.json.
    Skips channels that already have credentials populated (e.g., re-added via CLI).
    Non-destructive: does not delete Credential Manager entries.
#>

$ErrorActionPreference = 'Stop'

# --- Win32 CredRead wrapper ---
Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;

public static class CredManager {
    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL {
        public uint Flags;
        public int Type;
        public string TargetName;
        public string Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public int Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    public static string Read(string targetName) {
        IntPtr credPtr;
        if (!CredRead(targetName, 1, 0, out credPtr)) return null;
        try {
            var cred = Marshal.PtrToStructure<CREDENTIAL>(credPtr);
            if (cred.CredentialBlobSize == 0 || cred.CredentialBlob == IntPtr.Zero) return null;
            var bytes = new byte[cred.CredentialBlobSize];
            Marshal.Copy(cred.CredentialBlob, bytes, 0, (int)cred.CredentialBlobSize);
            return Encoding.Unicode.GetString(bytes);
        } finally {
            CredFree(credPtr);
        }
    }
}
"@

# --- Load channels.json ---
$channelsPath = Join-Path $env:LOCALAPPDATA 'FieldCure\Mcp.Outbox\channels.json'
if (-not (Test-Path $channelsPath)) {
    Write-Error "channels.json not found at $channelsPath"
}

$data = Get-Content $channelsPath -Raw | ConvertFrom-Json
$channels = $data.channels
$migrated = 0
$skipped = 0

foreach ($ch in $channels) {
    $id = $ch.id
    $type = $ch.type

    switch ($type) {
        'slack' {
            if ($ch.token) { Write-Host "  SKIP $id (token already set)"; $skipped++; continue }
            $val = [CredManager]::Read("FieldCure.Outbox:$id")
            if ($val) { $ch | Add-Member -NotePropertyName 'token' -NotePropertyValue $val -Force; Write-Host "  OK   $id <- token"; $migrated++ }
            else { Write-Host "  MISS $id (no credential in vault)" }
        }
        'discord' {
            if ($ch.webhookUrl) { Write-Host "  SKIP $id (webhookUrl already set)"; $skipped++; continue }
            $val = [CredManager]::Read("FieldCure.Outbox:$id")
            if ($val) { $ch | Add-Member -NotePropertyName 'webhookUrl' -NotePropertyValue $val -Force; Write-Host "  OK   $id <- webhookUrl"; $migrated++ }
            else { Write-Host "  MISS $id (no credential in vault)" }
        }
        'smtp' {
            if ($ch.password) { Write-Host "  SKIP $id (password already set)"; $skipped++; continue }
            $val = [CredManager]::Read("FieldCure.Outbox:$id")
            if ($val) { $ch | Add-Member -NotePropertyName 'password' -NotePropertyValue $val -Force; Write-Host "  OK   $id <- password"; $migrated++ }
            else { Write-Host "  MISS $id (no credential in vault)" }
        }
        'telegram' {
            if ($ch.apiId -and $ch.apiHash) { Write-Host "  SKIP $id (apiId/apiHash already set)"; $skipped++; continue }
            $api = [CredManager]::Read("FieldCure.Outbox:${id}:api")
            $phone = [CredManager]::Read("FieldCure.Outbox:${id}:phone")
            if ($api) {
                $parts = $api.Split(':', 2)
                if ($parts.Length -eq 2) {
                    $ch | Add-Member -NotePropertyName 'apiId' -NotePropertyValue $parts[0] -Force
                    $ch | Add-Member -NotePropertyName 'apiHash' -NotePropertyValue $parts[1] -Force
                    Write-Host "  OK   $id <- apiId, apiHash"
                }
            }
            if ($phone) {
                $ch | Add-Member -NotePropertyName 'phone' -NotePropertyValue $phone -Force
                Write-Host "  OK   $id <- phone (full number)"
            }
            if ($api -or $phone) { $migrated++ } else { Write-Host "  MISS $id (no credential in vault)" }
        }
        'kakaotalk' {
            if ($ch.apiKey) { Write-Host "  SKIP $id (apiKey already set)"; $skipped++; continue }
            $apiKey = [CredManager]::Read("FieldCure.Outbox:${id}:api_key")
            $clientSecret = [CredManager]::Read("FieldCure.Outbox:${id}:client_secret")
            if ($apiKey) {
                $ch | Add-Member -NotePropertyName 'apiKey' -NotePropertyValue $apiKey -Force
                Write-Host "  OK   $id <- apiKey"
                if ($clientSecret) {
                    $ch | Add-Member -NotePropertyName 'clientSecret' -NotePropertyValue $clientSecret -Force
                    Write-Host "  OK   $id <- clientSecret"
                }
                $migrated++
            } else { Write-Host "  MISS $id (no credential in vault)" }
        }
        'microsoft' {
            if ($ch.clientId) { Write-Host "  SKIP $id (clientId already set)"; $skipped++; continue }
            $clientId = [CredManager]::Read("FieldCure.Outbox:${id}:client_id")
            $clientSecret = [CredManager]::Read("FieldCure.Outbox:${id}:client_secret")
            if ($clientId -and $clientSecret) {
                $ch | Add-Member -NotePropertyName 'clientId' -NotePropertyValue $clientId -Force
                $ch | Add-Member -NotePropertyName 'clientSecret' -NotePropertyValue $clientSecret -Force
                Write-Host "  OK   $id <- clientId, clientSecret"
                $migrated++
            } else { Write-Host "  MISS $id (no credential in vault)" }
        }
        default {
            Write-Host "  SKIP $id (unknown type: $type)"
        }
    }
}

# --- Save ---
if ($migrated -gt 0) {
    $json = $data | ConvertTo-Json -Depth 10
    $tempPath = "$channelsPath.tmp"
    Set-Content $tempPath $json -Encoding UTF8
    Move-Item $tempPath $channelsPath -Force
    Write-Host "`nMigrated $migrated channel(s), skipped $skipped. channels.json updated."
} else {
    Write-Host "`nNothing to migrate (skipped $skipped, no vault credentials found for the rest)."
}
