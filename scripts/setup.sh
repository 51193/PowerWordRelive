#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

CACHE_DIR="$SCRIPT_DIR/cache"

# ──────────────────────────────────────────────
# 阶段 1：创建 Python 虚拟环境
# ──────────────────────────────────────────────

run_venv() {
    local name="$1"
    local project="$2"
    local venv_dir="$SCRIPT_DIR/$project/${name}_venv"
    local req_file="$SCRIPT_DIR/$project/${name}_venv.requirements.txt"

    if [ ! -f "$req_file" ]; then
        echo "[setup] 错误: 找不到依赖文件 $req_file"
        exit 1
    fi

    if [ -d "$venv_dir" ]; then
        echo "[setup] 跳过: ${name}_venv 已存在"
        return
    fi

    echo "[setup] 创建虚拟环境: ${name}_venv ..."
    python3 -m venv "$venv_dir"
    "$venv_dir/bin/pip" install --quiet -r "$req_file"
    echo "[setup] 完成: ${name}_venv"
}

echo "============================================"
echo "  PowerWordRelive 环境初始化"
echo "============================================"
echo ""

run_venv vad PowerWordRelive.AudioCapture
run_venv speaker_split PowerWordRelive.SpeakerSplit
run_venv transcribe PowerWordRelive.Transcribe

echo ""

# ──────────────────────────────────────────────
# 阶段 2：Token 预检
# ──────────────────────────────────────────────

HF_TOKEN=$(grep -s 'huggingface\.token' "$SCRIPT_DIR/config" 2>/dev/null | cut -d' ' -f2- | xargs || echo "")
MS_TOKEN=$(grep -s 'modelscope\.token' "$SCRIPT_DIR/config" 2>/dev/null | cut -d' ' -f2- | xargs || echo "")

echo "[setup] Token 状态检查:"
if [ -z "$HF_TOKEN" ]; then
    echo "[setup]   HuggingFace Token: 未配置"
else
    if [[ "$HF_TOKEN" =~ ^hf_ ]]; then
        echo "[setup]   HuggingFace Token: 已配置 ✓"
    else
        echo "[setup]   HuggingFace Token: 格式无效（应以 hf_ 开头）"
    fi
fi
if [ -z "$MS_TOKEN" ]; then
    echo "[setup]   ModelScope Token:   未配置"
else
    echo "[setup]   ModelScope Token:   已配置 ✓"
fi
echo ""

if [ -z "$HF_TOKEN" ]; then
    echo "[setup] 错误: 未配置 huggingface.token，无法继续。"
    echo "[setup] 说话人分离模型（pyannote/speaker-diarization-3.1）必须使用 HuggingFace Token 下载。"
    echo "[setup] Token 获取方法请阅读 README: 'https://github.com/51193/PowerWordRelive#huggingface必填'"
    echo "[setup]"
    echo "[setup] 请放心：已完成的虚拟环境创建和 VAD 模型下载不会被清除，修正配置后重新运行 setup.sh 即可继续。"
    exit 1
fi

if [[ ! "$HF_TOKEN" =~ ^hf_ ]]; then
    echo "[setup] 错误: huggingface.token 格式无效（应以 hf_ 开头）"
    echo "[setup] 请在 config 文件中将 huggingface.token 替换为合法的 HuggingFace Token。"
    echo "[setup] Token 获取方法请阅读 README: 'https://github.com/51193/PowerWordRelive#huggingface必填'"
    echo "[setup]"
    echo "[setup] 请放心：已完成的虚拟环境创建不会被清除，修正配置后重新运行 setup.sh 即可继续。"
    exit 1
fi

if [ -z "$MS_TOKEN" ]; then
    echo "[setup] 注意: 未配置 modelscope.token。"
    echo "[setup] 不配置此 Token，FunASR 模型（paraformer-zh，约 1GB）下载将被限速（约 200 KB/s），可能耗时数十分钟。"
    echo "[setup] 强烈推荐注册 ModelScope 账号并创建访问令牌（免费）。"
    echo "[setup] 获取方法: https://modelscope.cn/my/overview → 个人中心 → 访问令牌"
    echo "[setup] 创建后填入 config 中的 modelscope.token 即可。"
    echo ""
    read -r -p "[setup] 是否继续（下载会非常慢）？[y/N]: " yn
    if [[ ! "$yn" =~ ^[yY] ]]; then
        echo "[setup] 已取消。请在 config 中配置 modelscope.token 后重新运行 setup.sh。"
        exit 1
    fi
    echo ""
fi

# ──────────────────────────────────────────────
# 阶段 3：预下载模型（联网必需）
# ──────────────────────────────────────────────

run_download() {
    local label="$1"
    local project="$2"
    local venv="$3"
    local script="$4"
    shift 4
    local extra_args=("$@")

    local venv_dir="$SCRIPT_DIR/$project/${venv}_venv"
    local script_path="$SCRIPT_DIR/$project/$script"

    if [ ! -d "$venv_dir" ]; then
        echo "[setup] 跳过 $label 模型下载: ${venv}_venv 不存在"
        return
    fi
    if [ ! -f "$script_path" ]; then
        echo "[setup] 跳过 $label 模型下载: 找不到脚本 $script"
        return
    fi

    echo "[setup] 下载 $label 模型..."
    "$venv_dir/bin/python3" "$script_path" "${extra_args[@]}"
    echo ""
}

mkdir -p "$CACHE_DIR/torch"
run_download "Silero VAD" PowerWordRelive.AudioCapture vad download_vad_model.py \
    --cache-dir "$CACHE_DIR/torch"

mkdir -p "$CACHE_DIR/huggingface"
run_download "pyannote 说话人分离" PowerWordRelive.SpeakerSplit speaker_split download_speaker_model.py \
    --cache-dir "$CACHE_DIR/huggingface" \
    --hf-token "$HF_TOKEN"

mkdir -p "$CACHE_DIR/modelscope"
MS_ARGS=(--cache-dir "$CACHE_DIR/modelscope")
if [ -n "$MS_TOKEN" ]; then
    MS_ARGS+=(--ms-token "$MS_TOKEN")
fi
run_download "Paraformer 转录" PowerWordRelive.Transcribe transcribe download_transcribe_models.py \
    "${MS_ARGS[@]}"

echo "============================================"
echo "  初始化完成"
echo "============================================"
