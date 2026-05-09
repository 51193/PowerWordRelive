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
# 阶段 2：预下载模型（联网必需）
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
HF_TOKEN=$(grep -s 'huggingface.token' "$SCRIPT_DIR/config" 2>/dev/null | cut -d' ' -f2- | xargs || echo "")
if [ -z "$HF_TOKEN" ]; then
    echo "[setup] 跳过 pyannote 模型下载: 未配置 huggingface.token"
    echo ""
elif [[ ! "$HF_TOKEN" =~ ^hf_ ]]; then
    echo "[setup] 错误: huggingface.token 格式无效（应以 hf_ 开头）"
    echo "[setup] 请在 config 文件中将 huggingface.token 替换为合法的 HuggingFace Token。"
    echo "[setup] Token 获取方法请阅读 README: 'https://github.com/51193/PowerWordRelive#huggingface必填'"
    echo "[setup]"
    echo "[setup] 请放心：已完成的虚拟环境创建和 VAD 模型下载不会被清除，修正配置后重新运行 setup.sh 即可继续。"
    exit 1
else
    run_download "pyannote 说话人分离" PowerWordRelive.SpeakerSplit speaker_split download_speaker_model.py \
        --cache-dir "$CACHE_DIR/huggingface" \
        --hf-token "$HF_TOKEN"
fi

mkdir -p "$CACHE_DIR/modelscope"
run_download "Paraformer 转录" PowerWordRelive.Transcribe transcribe download_transcribe_models.py \
    --cache-dir "$CACHE_DIR/modelscope"

echo "============================================"
echo "  初始化完成"
echo "============================================"
