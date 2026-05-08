# Power Word Relive

实时捕获系统音频，将连续语音切分为按说话人分离的音频片段，转录为文本并入库，通过 LLM 进行说话人识别和对话精炼，最终通过远端 Web 前端查询精炼结果。

## 架构

```
CLI (PowerWordRelive.CLI)
  └── Host (PowerWordRelive.Host)
        ├── AudioCapture ─────── 音频捕获 + VAD 切分
        ├── SpeakerSplit ─────── 说话人分离
        ├── Transcribe ───────── 语音转录 (ASR)
        ├── TranscriptionStore ── SRT 入库 (SQLite)
        ├── LLMRequester ─────── 说话人识别 + 对话精炼 (LLM)
        └── LocalBackend ─────── 远端查询后端 (只读 DB)

RemoteBackend (独立运行, ASP.NET Core)
  ├── WebSocket (/ws/backend) ── 接受 LocalBackend 连接 (AES 认证)
  ├── WebSocket (/ws/frontend) ─ 接受浏览器连接
  └── wwwroot/ ───────────────── 静态前端页面
```

## 核心能力

### 1. 语音活动检测（VAD）

捕获 PulseAudio 桌面音频输出，**实时**将连续语音流按静音间隔切分为独立的语句片段，输出为 16kHz 单声道 WAV 文件。文件名以 UTC 时间戳标记，便于后续按序处理。

### 2. 说话人分离（Speaker Diarization）

将语句片段进一步拆分为按说话人区分的子片段：
- **单人语句**：识别该段所属的说话人 ID，输出为 `时间戳+SpeakerID.wav`
- **多人语句**：将每段切分后按说话人输出为 `时间戳+偏移量+SpeakerID.wav`
- **跨会话识别**：说话人声纹持久化（`.npy` 文件），同一说话人在不同会话中可被识别为同一 ID

### 3. 语音转录（ASR）

将说话人分离后的音频片段转录为字幕文本：
- 使用 FunASR Paraformer 模型
- 以长驻服务方式运行，模型只加载一次
- 输出为 SRT 字幕格式，与原输入文件名对应（1:1）
- 支持 CUDA 加速

### 4. 转录入库（TranscriptionStore）

将 SRT 字幕文件解析后写入 SQLite 数据库，建立 `transcriptions` 和 `speaker_mappings` 表。

### 5. LLM 请求引擎（LLMRequester）

通过定时器驱动，对数据库内容进行 LLM 增强：
- **说话人识别**：将未确认的 speaker ID 发送给 LLM，根据角色卡和上下文推断角色名
- **对话精炼**：将转录文本结合已有精炼结果窗，由 LLM 输出增删改操作，存储在 `refinement_results` 表中
- 精炼表使用浮点主键，支持插入式排序

### 6. 远端查询前端（RemoteBackend + LocalBackend）

- **LocalBackend**：由 Host 拉起，通过 WebSocket 连接到 RemoteBackend，提供 SQLite 只读查询
- **RemoteBackend**：独立 ASP.NET Core 服务，接受一条 LocalBackend 连接，将浏览器请求转发到 LocalBackend
- 浏览器端展示精炼结果，支持微信聊天式消息面板、按角色过滤
- 连接通过 AES-256 challenge-response 认证

## 快速开始

### 环境要求

- .NET 10 SDK
- Python 3.13+
- ffmpeg（用于音频编解码）
- PulseAudio（用于桌面音频捕获）

### 安装

```bash
# 1. 构建项目
dotnet build

# 2. 初始化 Python 虚拟环境（仅在首次或依赖变更后执行）
out/setup.sh
```

### 配置

复制并编辑 `config` 文件（首次使用从 `config.example` 复制）：

```ini
# ── 进程定义 ──
processes.audio_capture: PowerWordRelive.AudioCapture
processes.speaker_split: PowerWordRelive.SpeakerSplit
processes.transcribe: PowerWordRelive.Transcribe
processes.transcription_store: PowerWordRelive.TranscriptionStore
processes.llm_requester: PowerWordRelive.LLMRequester
processes.local_backend: PowerWordRelive.LocalBackend

# ── 进程配置域 ──
process_config.audio_capture.domains: audio_capture,general
process_config.speaker_split.domains: speaker_split,general,huggingface
process_config.transcribe.domains: transcribe,general
process_config.transcription_store.domains: transcription_store,storage,general
process_config.llm_requester.domains: llm_request,llm,general,storage,text_data
process_config.local_backend.domains: local_backend,storage,general

# ── 通用 ──
general.work_root: /absolute/path/to/work/root

# ── 各模块配置 ──
audio_capture.output_dir: ./segments
speaker_split.input_dir: ./segments
speaker_split.output_dir: ./speaker_segments
transcribe.input_dir: ./speaker_segments
transcribe.output_dir: ./transcriptions
transcription_store.input_dir: ./transcriptions
storage.sqlite_path: ./data/pwr.db

# ── LLM ──
llm.token: sk-your_token_here
llm.api_url: https://api.deepseek.com/v1/chat/completions
llm_request.timer.45: speaker_identification,refinement
llm_request.speaker_identification.model: deepseek-v4-pro
llm_request.refinement.model: deepseek-v4-flash

# ── HuggingFace Token ──
huggingface.token: hf_your_token_here

# ── 本地后端 (连接远端后端) ──
local_backend.remote_host: 127.0.0.1
local_backend.remote_port: 9500
local_backend.key_path: ./keys/local_backend.key
local_backend.max_reconnect_attempts: 5
local_backend.initial_reconnect_delay_sec: 2

# ── 远端后端 (独立运行) ──
remote_backend.port: 9500
remote_backend.key_path: /etc/pwr/remote_backend.key
```

### 运行

```bash
# 本地启动全部进程
out/PowerWordRelive.CLI/PowerWordRelive.CLI
```

使用 `Ctrl+C` 优雅退出，所有子进程会被自动清理。

## 远端前端部署

RemoteBackend 是一个独立的 ASP.NET Core 服务，部署在远端服务器上，不在 Host 的管理范围内。

### 1. 生成加密密钥

```bash
bash scripts/generate_key.sh
```

### 2. 部署密钥

将生成的密钥分别放置到两端：

```bash
# 远端服务器 — 固定路径
mkdir -p /etc/pwr
echo "<生成的密钥>" > /etc/pwr/remote_backend.key
chmod 600 /etc/pwr/remote_backend.key

# 本地机器 — 路径由 config 中的 local_backend.key_path 指定
mkdir -p ./keys
echo "<生成的密钥>" > ./keys/local_backend.key
```

### 3. 远端服务器：配置并启动 RemoteBackend

在 `out/PowerWordRelive.RemoteBackend/` 下放置 `config` 文件：

```ini
remote_backend.port: 9500
remote_backend.key_path: /etc/pwr/remote_backend.key
```

启动：

```bash
cd out/PowerWordRelive.RemoteBackend
dotnet PowerWordRelive.RemoteBackend.dll
```

### 4. 本地：配置 LocalBackend 并启动 Host

确保 `config` 中 `local_backend.*` 配置项指向正确的远端地址和密钥路径，然后：

```bash
out/PowerWordRelive.CLI/PowerWordRelive.CLI
```

### 5. 访问前端

浏览器打开 `http://<remote_host>:<remote_backend.port>`

## 输出结构

```
<work_root>/
├── segments/                     # VAD 切分的语句片段
│   └── 20260506_120000_000000.wav
├── speaker_segments/             # 说话人分离后的音频
│   ├── 20260506_120000_000000+speaker_0.wav
│   └── 20260506_120000_000000+00000+speaker_1.wav
├── speaker_embeddings/           # 持久化声纹
│   └── speaker_0.npy
├── transcriptions/               # ASR 转录字幕 (SRT)
│   └── 20260506_120000_000000+speaker_0.srt
└── data/
    └── pwr.db                    # SQLite 数据库
        ├── transcriptions        # 转录条目
        ├── speaker_mappings      # 说话人 → 角色名映射
        └── refinement_results    # LLM 精炼对话
```

## 性能

说话人分离使用 pyannote speaker-diarization-3.1 模型。首次运行会从 HuggingFace 下载模型（约 32MB）到 `out/cache/huggingface/`。

在处理约 20 条以上文件后，累计处理速度通常能达到实时或快于实时（speed ≥ 1.0）。

可通过调整 `speaker_split` 域下的参数优化 CPU 性能：
- `omp_num_threads`：PyTorch 线程数（默认 8）
- `segmentation_batch_size`：分段批处理大小（默认 64）
- `embedding_batch_size`：声纹批处理大小（默认 64，增大可提升吞吐量但增加内存占用）
