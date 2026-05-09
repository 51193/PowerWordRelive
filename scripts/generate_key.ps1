#!/usr/bin/env pwsh
# 生成 AES-256 密钥 (base64)，用于 RemoteBackend ↔ LocalBackend 认证
# 等价于 generate_key.sh，但使用 .NET 密码学 API（无需 openssl）

$ErrorActionPreference = "Stop"

$aes = [System.Security.Cryptography.Aes]::Create()
$aes.KeySize = 256
$aes.GenerateKey()
$key = [Convert]::ToBase64String($aes.Key)
$aes.Dispose()

Write-Host "Generated AES-256 key (base64):"
Write-Host $key
Write-Host ""
Write-Host "--- Usage ---"
Write-Host "LocalBackend:  save this key to the path specified by local_backend.key_path in config"
Write-Host "RemoteBackend: save this key to the path specified by remote_backend.key_path in config (Linux: /etc/pwr/remote_backend.key, Windows: C:\ProgramData\pwr\remote_backend.key)"
Write-Host ""
Write-Host "Example (Linux):"
Write-Host "  mkdir -p /etc/pwr"
Write-Host "  echo '$key' > /etc/pwr/remote_backend.key"
Write-Host "  chmod 600 /etc/pwr/remote_backend.key"
Write-Host ""
Write-Host "Example (Windows):"
Write-Host "  New-Item -ItemType Directory -Force -Path C:\ProgramData\pwr"
Write-Host "  Set-Content -Path C:\ProgramData\pwr\remote_backend.key -Value '$key'" -NoNewline
Write-Host ""