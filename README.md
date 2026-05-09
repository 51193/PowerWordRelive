# Power Word Relive

> 你的 TRPG 跑团 AI 记录员——开着它跑团，自动帮你搞定一切。

## 它能做什么

- **自动录音**：捕捉电脑播放的声音，无需额外操作
- **分辨谁在说话**：自动识别不同玩家的声音，分开记录
- **语音转文字**：把所有人的对话变成文字
- **AI 精炼对话**：把啰嗦的口语整理成好看的剧本，去掉闲聊和骰子讨论
- **追踪故事进展**：AI 自动记录剧情推进到哪里，生成章节概要
- **管理任务清单**：AI 帮你追踪团队接到的所有任务，该做的、做完的、忘了的、失败的，一目了然
- **建立世界观词典**：AI 自动记录所有出现的人物、地点、物品，再也不怕忘了 NPC 叫什么
- **网页查看**：内建本地 Web 界面，开浏览器就能像翻聊天记录一样回顾整场游戏；也支持部署到远端服务器

## 开始使用

### 你需要什么

| 东西 | Linux | Windows | 必须？ |
|------|-------|---------|--------|
| 操作系统 | Ubuntu / Debian 等 | Windows 10/11 | 是 |
| .NET 10 运行环境 | [下载 .NET 10 Runtime](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0) | [下载 .NET 10 Runtime](https://dotnet.microsoft.com/zh-cn/download/dotnet/10.0) | 是 |
| Python 3.13+ | 系统一般自带 | [python.org](https://www.python.org/downloads/) 安装 | 是 |
| ffmpeg | `apt install ffmpeg` | [ffmpeg.org](https://ffmpeg.org/download.html) 下载并加入 PATH | 是 |
| DeepSeek API Token | 去 [platform.deepseek.com](https://platform.deepseek.com) 注册充值 | 同左 | 是 |
| HuggingFace Token | 去 [huggingface.co](https://huggingface.co/settings/tokens) 注册（免费）**+ 接受模型使用协议** | 同左 | 是 |
| ModelScope Token | 去 [modelscope.cn](https://modelscope.cn/my/overview) 注册（免费） | 同左 | 否（但强烈推荐，否则模型下载限速 ~200 KB/s） |
| 音频环回设备 | 不需要（PulseAudio 原生支持） | 任意环回设备均可（如 Stereo Mix、VoiceMeeter），搞不清楚则推荐安装 VB-Cable | 需要 |

> **Windows 用户注意**：Windows 没有内建系统音频环回设备，需要手动指定一个环回设备。默认值使用 VB-Cable 的虚拟声卡名（`VB-Audio Virtual Cable`），如果你知道自己的系统输出设备名称（如 Stereo Mix、VoiceMeeter 等），直接填入 `windows_audio_device` 配置项即可，不需要安装 VB-Cable。

### 第一步：下载

去 [Releases 页面](../../releases) 下载对应平台的压缩包：

- **Linux**：`pwr-main-xxx-linux-x64.tar.gz`
- **Windows**：`pwr-main-xxx-win-x64.tar.gz`

```bash
# Linux
tar -xzf pwr-main-*-linux-x64.tar.gz

# Windows（PowerShell）
tar -xzf pwr-main-*-win-x64.tar.gz
```

### 第二步：初始化

**Linux：**
```bash
bash setup.sh
```

**Windows（PowerShell）：**
```powershell
.\setup.ps1
```

> 如果卡住了：模型下载可能需要几分钟到十几分钟，取决于网速。出现"初始化完成"就说明好了。
>
> Windows 上如果遇到执行策略限制，先运行：`Set-ExecutionPolicy -Scope CurrentUser RemoteSigned`

### 第三步：配置

解压后的目录里有个 `config` 文件，用文本编辑器打开它。**你只需要改这几行：**

```
# 工作目录：所有录音、文字、数据库都会放到这个目录下
# 必须是绝对路径，而且目录要存在
# Linux 示例：
general.work_root: /home/你的用户名/trpg-data
# Windows 示例：
general.work_root: C:\Users\你的用户名\trpg-data

# DeepSeek API Token：去 platform.deepseek.com 注册充值后获取
llm.token: sk-xxxxxxxxxxxxxxxx

# HuggingFace Token：去 huggingface.co/settings/tokens 创建（免费的）
huggingface.token: hf_xxxxxxxxxxxxxxxx

# ModelScope Token（可选但推荐）：去 modelscope.cn/my/overview 创建（免费的）
# 不填会导致 ASR 模型下载极慢（约 200 KB/s），详见下方"配置参考→ModelScope"一节
modelscope.token: ms_xxxxxxxxxxxxxxxx
```

> **关于 HuggingFace Token**：光注册 HuggingFace 账号还不够。这个项目使用的说话人分离模型（pyannote/speaker-diarization-3.1）是一个受限模型，需要在网页上接受使用协议才能下载。完整步骤如下：
>
> 1. 注册 HuggingFace 账号：[huggingface.co/join](https://huggingface.co/join)
> 2. 打开 https://huggingface.co/pyannote/speaker-diarization-3.1 ，点"Agree and access repository"
> 3. 页面会弹一个简单的小问卷，随便填一下就行（不影响使用）
> 4. 去 https://huggingface.co/settings/tokens 创建一个 Access Token（类型选"Read"）
> 5. 把生成的 Token 填到上面 `huggingface.token` 那一行

其他所有配置项保持默认即可，不需要动。

> 也可以参考 `config.example` 查看所有可配置项的说明。

### 第四步：运行

```bash
dotnet PowerWordRelive.CLI/PowerWordRelive.CLI.dll
```

> **Windows 上**：路径分隔符为 `\`，命令相同：`dotnet PowerWordRelive.CLI\PowerWordRelive.CLI.dll`

启动后，系统就开始自动捕捉电脑声音了。你正常跑团就行。

**按 `Ctrl+C` 停止**，所有录音、文字、AI 结果都会保存在你配置的工作目录里。

## 跑完之后看什么

在你配的 `general.work_root` 目录下：

```
work_root/
├── data/pwr.db              # SQLite 数据库，所有 AI 处理结果都在这里
├── segments/                # 原始录音片段（VAD 切出来的）
├── speaker_segments/        # 按说话人分好的录音文件
└── transcriptions/          # 文字转录结果
```

### 用网页查看

启动后在浏览器打开 `http://localhost:9501`（端口由 `config` 中的 `local_mode.local_port` 决定），即可查看所有对话记录、故事进展、任务清单和世界观词典。

如果你想在别的设备上查看（比如边跑团边用手机看），需要部署到一台有公网 IP 的服务器上。部署方法见下方"远程服务器部署"一节。

## 配置参考

以下是 `config` 文件中**你可能关心的配置项**。没有列在这里的都是内部参数，保持默认就好。

### 工作目录

```
# 所有输出都放在这里，必须是绝对路径，必须已存在
# Linux:
general.work_root: /home/你/trpg-data
# Windows:
general.work_root: C:\Users\你\trpg-data
```

### 本地网页

```
# 本地 Web 界面的端口号
# 程序启动后浏览器打开 http://localhost:9501 即可查看
local_mode.local_port: 9501
```

### 远端后端连接（可选）

```
# 设为 true 以额外连接一个远端后端（与本地网页同时生效）
local_backend.remote_enabled: false
local_backend.remote_host: your.remote.server.com
local_backend.remote_port: 9500
local_backend.key_path: /etc/pwr/local_backend.key
local_backend.max_reconnect_attempts: 5
local_backend.initial_reconnect_delay_sec: 2
```

### LLM（AI 模型）

```
# 你的 DeepSeek API Token（必填）
llm.token: sk-xxxxxxxxxxxxxxxx
# API 地址（不用改）
llm.api_url: https://api.deepseek.com/v1/chat/completions
```

### 录音相关

```
# 录音采样率，一般不用改
audio_capture.sample_rate: 16000
# 静音多少毫秒算一段话结束（数值越小切得越碎）
audio_capture.silence_timeout_ms: 800
# 最长一段录音秒数，超过会被强制切开
audio_capture.max_segment_sec: 120
# Windows：音频环回设备名（Linux 下自动使用 PulseAudio，忽略此项）
# 默认使用 VB-Cable 的虚拟声卡名，安装后无需修改
# 如果你知道系统输出设备的名称（如 Stereo Mix、VoiceMeeter 等），直接填进去即可，不需要 VB-Cable
# 可用 ffmpeg -list_devices true -f dshow -i dummy 查看设备列表
audio_capture.windows_audio_device: VB-Audio Virtual Cable
```

### HuggingFace（必填）

```
# HuggingFace Token
huggingface.token: 在此填入你的HuggingFace Token
```

这个项目使用 pyannote 的 [speaker-diarization-3.1](https://huggingface.co/pyannote/speaker-diarization-3.1) 模型进行说话人分离。该模型是受限模型，除了注册账号、创建 Token 之外，还需要**手动接受使用协议**：

1. 打开 https://huggingface.co/pyannote/speaker-diarization-3.1 → 点"Agree and access repository"
2. 填一个小问卷（随便填）
3. 去 https://huggingface.co/settings/tokens 创建 Access Token（Fine-grained 类型即可）
4. 把 Token 填到 `huggingface.token`

没有 Token 或没有接受协议，说话人分离流水线会无法启动。

### ModelScope（可选，但强烈推荐）

```
# ModelScope 访问令牌（可选，但强烈推荐）
modelscope.token: （可选）在此填入你的ModelScope访问令牌
```

语音转录（ASR）使用的 FunASR 模型默认从 ModelScope 下载。不配置 Token 会被限速到约 200 KB/s，1GB 模型可能耗时数十分钟。配置后不限速。

**获取方法：**

1. 打开 https://modelscope.cn/my/overview → 登录（可用阿里云 / GitHub / 手机号注册）
2. 点击左侧"访问控制" → "创建访问令牌"
3. 随便填个名称 → 确认创建
4. 复制生成的 Token，填入上面 `modelscope.token` 一行

## 远程服务器部署

> 本地网页已经开箱即用（`http://localhost:9501`）。以下仅当你需要在其他设备上远程查看时才需要。一台有公网 IP 的服务器就行。

### 1. 在服务器上下载 RemoteBackend 包

去 [Releases 页面](../../releases) 下载对应平台的 `pwr-remote-xxx-*.tar.gz`。

```bash
# Linux
tar -xzf pwr-remote-*-linux-x64.tar.gz

# Windows
tar -xzf pwr-remote-*-win-x64.tar.gz
```

### 2. 生成密钥

在**本地电脑**上：

**Linux / macOS：**
```bash
bash generate_key.sh
```

**Windows（PowerShell）：**
```powershell
.\generate_key.ps1
```

程序会输出一串密钥，复制它。

### 3. 部署密钥

```bash
# 在服务器上（Linux）：
echo "刚才复制的密钥" | sudo tee /etc/pwr/remote_backend.key
sudo chmod 600 /etc/pwr/remote_backend.key

# 在服务器上（Windows PowerShell）：
New-Item -ItemType Directory -Force -Path C:\ProgramData\pwr
Set-Content -Path C:\ProgramData\pwr\remote_backend.key -Value "刚才复制的密钥"

# 在本地电脑上（PowerWordRelive 目录下）：
mkdir -p keys
# Linux:
echo "刚才复制的密钥" > keys/local_backend.key
# Windows PowerShell:
Set-Content -Path keys\local_backend.key -Value "刚才复制的密钥"
```

### 4. 配置并启动服务端

在服务器上解压后的目录中，编辑 `config` 文件：

```
remote_backend.port: 9500
remote_backend.key_path: /etc/pwr/remote_backend.key
```

> Windows 服务器上改为 `C:\ProgramData\pwr\remote_backend.key`。

启动：

```bash
dotnet PowerWordRelive.RemoteBackend/PowerWordRelive.RemoteBackend.dll
```

### 5. 配置本地连接服务端

在本地电脑的 `config` 中修改：

```
local_backend.remote_enabled: true
local_backend.remote_host: 你服务器的IP
local_backend.remote_port: 9500
local_backend.key_path: ./keys/local_backend.key
```

然后正常启动本地程序。启动后在浏览器打开 `http://服务器IP:9500` 即可查看。

### 6. 保持服务端长期运行（可选）

可以用 `systemd` 或 `screen` 让服务端在后台一直运行。

## 常见问题

### Q: 启动时报错"找不到 config"
A: 确认你在解压后的目录里运行命令，且该目录下有 `config` 文件。

### Q: 说话人分离不工作？
A: 99% 是因为 HuggingFace Token 没配好，或者没有接受模型使用协议。重新检查上面"HuggingFace（必填）"那段里的步骤，特别是第 1、2 步——光有 Token 不够，必须去模型页面点"Agree and access repository"才能下载模型。

### Q: AI 精炼结果质量不好？
A: 可以在 `config` 中把 `llm_request.refinement.model` 从 `deepseek-v4-flash` 改成 `deepseek-v4-pro`（更贵但更好）。

### Q: 提示"Python not found"
A: 确保系统装了 Python 3.13+。
- Linux：`apt install python3 python3-venv`
- Windows：从 [python.org](https://www.python.org/downloads/) 安装，钩选"Add Python to PATH"

### Q: 提示"ffmpeg not found"
A:
- Linux：`apt install ffmpeg`
- Windows：从 [ffmpeg.org](https://ffmpeg.org/download.html) 下载，将 `ffmpeg.exe` 所在目录加入系统 PATH

### Q: Windows 上录音无声音/找不到设备？
A: Windows 没有内建系统音频环回。两种方式解决：

**方式一：使用已有的环回设备**
先用以下命令查看设备列表：
```
ffmpeg -list_devices true -f dshow -i dummy
```
将列出的设备名填入 `config` 的 `audio_capture.windows_audio_device` 配置项。

**方式二：安装 VB-Cable**
1. 从 [vb-audio.com/Cable/](https://vb-audio.com/Cable/) 下载安装（免费）
2. 安装后保持 `config` 中 `audio_capture.windows_audio_device` 为默认值 `VB-Audio Virtual Cable` 即可

### Q: 数据库里看不到数据？
A: 先确认录音正常（看 `segments/` 目录下有没有 wav 文件）。如果有 wav 但没有文字，可能是 ASR 模型没下载好：重新运行初始化脚本（Linux：`bash setup.sh`，Windows：`.\setup.ps1`）。

## 平台支持

| 平台 | 状态 |
|------|------|
| Linux (x64) | 正式支持，充分测试 |
| Windows (x64) | 支持，**缺少充分测试**。如有问题请在 [Issues](../../issues) 提报 |
| macOS | 未适配（欢迎贡献） |

---

## 进阶：从源码构建

这部分适合想自己改代码或参与开发的人。普通用户不看这个也能正常使用。

### 环境要求

- .NET 10 SDK
- Python 3.13+
- ffmpeg

### 构建

```bash
git clone <项目地址>
cd PowerWordRelive
dotnet build
```

**初始化 Python 环境：**
- Linux：`bash out/setup.sh`
- Windows：`.\out\setup.ps1`

### 运行测试

```bash
dotnet test
```

### 打包

```bash
# Linux / macOS
bash scripts/package.sh linux-x64
# Windows (Git Bash / WSL)
bash scripts/package.sh win-x64
# 产物在 out/pkg/ 下
```

### 内部架构

```
CLI（用户入口）
  └── Host（进程管理器）
        ├── AudioCapture      # 录音 + 语音分段
        ├── SpeakerSplit      # 说话人分离
        ├── Transcribe        # 语音转文字
        ├── TranscriptionStore # 写入数据库
        ├── LLMRequester      # AI 处理（说话人识别、对话精炼、故事进展、任务、一致性表）
        ├── RemoteBackend     # 本地 Web 服务（提供网页界面）
        └── LocalBackend      # 网页查询后端（连接本地 RemoteBackend 或远端）

RemoteBackend 也支持独立部署到远端服务器，供远程查看。
```

LLMRequester 内部维护 5 种 AI 请求，按定时器循环触发：

| 请求 | 作用 | 周期 |
|------|------|------|
| `speaker_identification` | 根据对话内容推断说话人角色名 | 45s |
| `refinement` | 把口语对话精炼成剧本 | 45s |
| `story_progress` | 生成章节梗概式故事进展 | 90s |
| `task` | 管理任务清单（进行中/完成/失败/放弃） | 90s |
| `consistency` | 维护人物/地点/物品的世界观词典 | 90s |

### 项目结构

```
PowerWordRelive/
├── PowerWordRelive.CLI/               # 用户入口
├── PowerWordRelive.Host/              # 进程管理器
├── PowerWordRelive.Infrastructure/    # 共享库（含跨平台 PlatformServices）
├── PowerWordRelive.AudioCapture/      # 录音模块
├── PowerWordRelive.SpeakerSplit/      # 说话人分离
├── PowerWordRelive.Transcribe/        # 语音转文字
├── PowerWordRelive.TranscriptionStore/ # 数据库写入
├── PowerWordRelive.LLMRequester/      # AI 请求引擎
├── PowerWordRelive.LocalBackend/      # 网页查询后端
├── PowerWordRelive.RemoteBackend/     # 网页服务端
├── scripts/                           # Python 脚本 + 初始化（sh + ps1）+ CI
├── text_data/                         # AI 提示词模板 + 角色卡
└── test/                              # 测试音频和输出
```
