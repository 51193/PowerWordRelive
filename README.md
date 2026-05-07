# Power Word Relive

实时捕获系统音频，将连续语音切分为按说话人分离的音频片段，为桌面 TRPG 场景提供离线的对话结构化能力。

## 核心能力

### 1. 语音活动检测（VAD）

捕获 PulseAudio 桌面音频输出，**实时**将连续语音流按静音间隔切分为独立的语句片段，输出为 16kHz 单声道 WAV 文件。文件名以 UTC 时间戳标记，便于后续按序处理。

### 2. 说话人分离（Speaker Diarization）

将语句片段进一步拆分为按说话人区分的子片段：
- **单人语句**：识别该段所属的说话人 ID，输出为 `时间戳+SpeakerID.wav`
- **多人语句**：将每段切分后按说话人输出为 `时间戳+偏移量+SpeakerID.wav`
- **跨会话识别**：说话人声纹持久化（`.npy` 文件），同一说话人在不同会话中可被识别为同一 ID

### 3. 语音转录（Whisper Transcription）

将说话人分离后的音频片段转录为字幕文本：
- 使用 OpenAI Whisper 模型（默认 `turbo`，可配置）
- 以长驻服务方式运行，模型只加载一次
- 输出为 SRT 字幕格式，与原输入文件名对应（1:1）
- 支持 CUDA 加速，模型可配置

### 4. 按时间戳顺序输出

所有处理严格按时间戳顺序进行，输出文件按时间排序，下游进程可无缝按序消费。

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

编辑 `config` 文件（首次使用请复制 `config.example` 并填充实际值）：

```ini
# 进程定义
processes.audio_capture: PowerWordRelive.AudioCapture
processes.speaker_split: PowerWordRelive.SpeakerSplit
processes.transcribe: PowerWordRelive.Transcribe

# 可选启用的进程
process_config.audio_capture.domains: audio_capture,general
process_config.speaker_split.domains: speaker_split,general,huggingface
process_config.transcribe.domains: transcribe,general

# 工作目录（所有输入输出路径相对于此）
general.work_root: /path/to/work/root

# 音频捕获
audio_capture.output_dir: ./segments
audio_capture.silence_timeout_ms: 500

# 说话人分离
speaker_split.input_dir: ./segments
speaker_split.output_dir: ./speaker_segments
speaker_split.match_threshold: 0.55

# Whisper 转录
transcribe.input_dir: ./speaker_segments
transcribe.output_dir: ./transcriptions
transcribe.model: turbo
transcribe.device: cuda

# HuggingFace Token（用于下载模型）
huggingface.token: hf_your_token_here
```

### 运行

```bash
out/PowerWordRelive.CLI/PowerWordRelive.CLI
```

CLI 会自动启动所有已配置的进程。进程会根据配置监听对应的输入目录，实时捕获并处理音频。

使用 `Ctrl+C` 优雅退出。所有子进程会被自动清理。

## 输出结构

```
<work_root>/
├── segments/                     # VAD 切分的语句片段
│   ├── 20260506_120000_000000.wav
│   └── ...
├── speaker_segments/             # 说话人分离后的音频
│   ├── 20260506_120000_000000+speaker_0.wav
│   ├── 20260506_120000_000000+00000+speaker_1.wav
│   └── ...
├── speaker_embeddings/           # 持久化声纹
│   ├── speaker_0.npy
│   └── ...
└── transcriptions/               # Whisper 转录字幕
    ├── 20260506_120000_000000+speaker_0.srt
    └── ...
```

## 性能

说话人分离使用 pyannote speaker-diarization-3.1 模型。首次运行会从 HuggingFace 下载模型（约 32MB）到 `out/cache/huggingface/`。

在处理约 20 条以上文件后，累计处理速度通常能达到实时或快于实时（speed ≥ 1.0）。

可通过调整 `speaker_split` 域下的参数优化 CPU 性能：
- `omp_num_threads`：PyTorch 线程数（默认 8）
- `segmentation_batch_size`：分段批处理大小（默认 64）
- `embedding_batch_size`：声纹批处理大小（默认 64，增大可提升吞吐量但增加内存占用）
