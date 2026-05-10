#!/usr/bin/env pwsh
$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

$LocalKeyFile = Join-Path $ScriptDir "local_key.bin"
$RemoteKeyFile = Join-Path $ScriptDir "remote_key.bin"

function Generate-Key {
    $aes = [System.Security.Cryptography.Aes]::Create()
    $aes.KeySize = 256
    $aes.GenerateKey()
    $key = [Convert]::ToBase64String($aes.Key)
    $aes.Dispose()
    return $key
}

$LocalKey = Generate-Key
$RemoteKey = Generate-Key

Set-Content -Path $LocalKeyFile -Value $LocalKey -NoNewline
Set-Content -Path $RemoteKeyFile -Value $RemoteKey -NoNewline

Write-Host "Generated AES-256 keys:"
Write-Host "  Local:  $LocalKeyFile"
Write-Host "  Remote: $RemoteKeyFile"
Write-Host ""
Write-Host "--- Usage ---"
Write-Host "Local key:  Host generates this automatically for local web mode."
Write-Host "            This file is for manual testing / standalone deployment."
Write-Host ""
Write-Host "Remote key: Copy this file to both:"
Write-Host "  Client:   path specified by remote_mode.remote.key_path in config"
Write-Host "  Server:   path specified by remote_mode.server.key_path in config"
Write-Host ""
Write-Host "Example (Windows):"
Write-Host "  # On client"
Write-Host "  Copy-Item $RemoteKeyFile .\keys\remote_key.bin"
Write-Host ""
Write-Host "  # On server"
Write-Host "  New-Item -ItemType Directory -Force -Path C:\ProgramData\pwr"
Write-Host "  Copy-Item remote_key.bin C:\ProgramData\pwr\remote_key.bin"
