#!/usr/bin/env pwsh
# PowerWordRelive Windows 环境初始化脚本
# 等价于 setup.sh，但使用 PowerShell 实现

$ErrorActionPreference = "Stop"

$ScriptDir = Split-Path -Parent $PSCommandPath
$CacheDir = Join-Path $ScriptDir "cache"

# ──────────────────────────────────────────────
# 阶段 1：创建 Python 虚拟环境
# ──────────────────────────────────────────────

function New-Venv {
    param([string]$Name, [string]$Project)

    $VenvDir = Join-Path $ScriptDir "$Project/${Name}_venv"
    $ReqFile = Join-Path $ScriptDir "$Project/${Name}_venv.requirements.txt"

    if (-not (Test-Path $ReqFile)) {
        Write-Host "[setup] 错误: 找不到依赖文件 $ReqFile" -ForegroundColor Red
        exit 1
    }

    if (Test-Path $VenvDir) {
        Write-Host "[setup] 跳过: ${Name}_venv 已存在"
        return
    }

    Write-Host "[setup] 创建虚拟环境: ${Name}_venv ..."
    python -m venv $VenvDir
    & "$VenvDir\Scripts\pip" install --quiet -r $ReqFile
    Write-Host "[setup] 完成: ${Name}_venv"
}

Write-Host "============================================"
Write-Host "  PowerWordRelive 环境初始化"
Write-Host "============================================"
Write-Host ""

New-Venv -Name "vad" -Project "PowerWordRelive.AudioCapture"
New-Venv -Name "speaker_split" -Project "PowerWordRelive.SpeakerSplit"
New-Venv -Name "transcribe" -Project "PowerWordRelive.Transcribe"

Write-Host ""

# ──────────────────────────────────────────────
# 阶段 2：Token 预检
# ──────────────────────────────────────────────

$ConfigFile = Join-Path $ScriptDir "config"
$HfToken = $null
$MsToken = $null
if (Test-Path $ConfigFile) {
    $match = Select-String -Path $ConfigFile -Pattern '^huggingface\.token:\s*(.+)$' | Select-Object -First 1
    if ($match) { $HfToken = $match.Matches.Groups[1].Value.Trim() }
    $matchMs = Select-String -Path $ConfigFile -Pattern '^modelscope\.token:\s*(.+)$' | Select-Object -First 1
    if ($matchMs) { $MsToken = $matchMs.Matches.Groups[1].Value.Trim() }
}

Write-Host "[setup] Token 状态检查:"
if (-not $HfToken) {
    Write-Host "[setup]   HuggingFace Token: 未配置"
} elseif ($HfToken -match '^hf_') {
    Write-Host "[setup]   HuggingFace Token: 已配置" -ForegroundColor Green
} else {
    Write-Host "[setup]   HuggingFace Token: 格式无效（应以 hf_ 开头）" -ForegroundColor Yellow
}
if (-not $MsToken) {
    Write-Host "[setup]   ModelScope Token:   未配置"
} else {
    Write-Host "[setup]   ModelScope Token:   已配置" -ForegroundColor Green
}
Write-Host ""

if (-not $HfToken) {
    Write-Host "[setup] 错误: 未配置 huggingface.token，无法继续。" -ForegroundColor Red
    Write-Host "[setup] 说话人分离模型（pyannote/speaker-diarization-3.1）必须使用 HuggingFace Token 下载。"
    Write-Host "[setup] Token 获取方法请阅读 README: 'https://github.com/51193/PowerWordRelive#huggingface必填'"
    Write-Host "[setup]"
    Write-Host "[setup] 请放心：已完成的虚拟环境创建不会被清除，修正配置后重新运行 setup.ps1 即可继续。"
    exit 1
}

if ($HfToken -notmatch '^hf_') {
    Write-Host "[setup] 错误: huggingface.token 格式无效（应以 hf_ 开头）" -ForegroundColor Red
    Write-Host "[setup] 请在 config 文件中将 huggingface.token 替换为合法的 HuggingFace Token。"
    Write-Host "[setup] Token 获取方法请阅读 README: 'https://github.com/51193/PowerWordRelive#huggingface必填'"
    Write-Host "[setup]"
    Write-Host "[setup] 请放心：已完成的虚拟环境创建不会被清除，修正配置后重新运行 setup.ps1 即可继续。"
    exit 1
}

if (-not $MsToken) {
    Write-Host "[setup] 注意: 未配置 modelscope.token。"
    Write-Host "[setup] 不配置此 Token，FunASR 模型（paraformer-zh，约 1GB）下载将被限速（约 200 KB/s），可能耗时数十分钟。"
    Write-Host "[setup] 强烈推荐注册 ModelScope 账号并创建访问令牌（免费）。"
    Write-Host "[setup] 获取方法: https://modelscope.cn/my/overview -> 个人中心 -> 访问令牌"
    Write-Host "[setup] 创建后填入 config 中的 modelscope.token 即可。"
    Write-Host ""
    $yn = Read-Host "[setup] 是否继续（下载会非常慢）？[y/N]"
    if ($yn -notmatch '^[yY]') {
        Write-Host "[setup] 已取消。请在 config 中配置 modelscope.token 后重新运行 setup.ps1。"
        exit 1
    }
    Write-Host ""
}

# ──────────────────────────────────────────────
# 阶段 3：预下载模型（联网必需）
# ──────────────────────────────────────────────

function Invoke-Download {
    param(
        [string]$Label,
        [string]$Project,
        [string]$Venv,
        [string]$Script,
        [string[]]$ExtraArgs
    )

    $VenvDir = Join-Path $ScriptDir "$Project/${Venv}_venv"
    $ScriptPath = Join-Path $ScriptDir "$Project/$Script"

    if (-not (Test-Path $VenvDir)) {
        Write-Host "[setup] 跳过 $Label 模型下载: ${Venv}_venv 不存在"
        return
    }
    if (-not (Test-Path $ScriptPath)) {
        Write-Host "[setup] 跳过 $Label 模型下载: 找不到脚本 $Script"
        return
    }

    Write-Host "[setup] 下载 $Label 模型..."
    & "$VenvDir\Scripts\python" $ScriptPath @ExtraArgs
    Write-Host ""
}

New-Item -ItemType Directory -Force -Path (Join-Path $CacheDir "torch") | Out-Null
Invoke-Download -Label "Silero VAD" -Project "PowerWordRelive.AudioCapture" `
    -Venv "vad" -Script "download_vad_model.py" `
    -ExtraArgs @("--cache-dir", (Join-Path $CacheDir "torch"))

New-Item -ItemType Directory -Force -Path (Join-Path $CacheDir "huggingface") | Out-Null
Invoke-Download -Label "pyannote 说话人分离" -Project "PowerWordRelive.SpeakerSplit" `
    -Venv "speaker_split" -Script "download_speaker_model.py" `
    -ExtraArgs @("--cache-dir", (Join-Path $CacheDir "huggingface"), "--hf-token", $HfToken)

New-Item -ItemType Directory -Force -Path (Join-Path $CacheDir "modelscope") | Out-Null
$MsExtraArgs = @("--cache-dir", (Join-Path $CacheDir "modelscope"))
if ($MsToken) {
    $MsExtraArgs += @("--ms-token", $MsToken)
}
Invoke-Download -Label "Paraformer 转录" -Project "PowerWordRelive.Transcribe" `
    -Venv "transcribe" -Script "download_transcribe_models.py" `
    -ExtraArgs $MsExtraArgs

Write-Host "============================================"
Write-Host "  初始化完成"
Write-Host "============================================"
