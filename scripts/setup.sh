#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

run_venv() {
    local name="$1"
    local project="$2"
    local venv_dir="$SCRIPT_DIR/$project/${name}_venv"
    local req_file="$SCRIPT_DIR/$project/${name}_venv.requirements.txt"

    if [ ! -f "$req_file" ]; then
        echo "[error] requirements not found: $req_file"
        exit 1
    fi

    if [ -d "$venv_dir" ]; then
        echo "[skip] $venv_dir already exists"
        return
    fi

    echo "[create] $venv_dir"
    python3 -m venv "$venv_dir"
    "$venv_dir/bin/pip" install --quiet -r "$req_file"
    echo "[done] $venv_dir"
}

run_venv vad PowerWordRelive.AudioCapture
run_venv speaker_split PowerWordRelive.SpeakerSplit
run_venv_whisper() {
    local project="PowerWordRelive.Transcribe"
    local venv_dir="$SCRIPT_DIR/$project/whisper_venv"
    local req_file="$SCRIPT_DIR/$project/whisper_venv.requirements.txt"
    local torch_installer="$SCRIPT_DIR/$project/install_torch_cuda.py"

    if [ ! -f "$req_file" ]; then
        echo "[error] requirements not found: $req_file"
        exit 1
    fi

    if [ -d "$venv_dir" ]; then
        echo "[skip] $venv_dir already exists"
        return
    fi

    echo "[create] $venv_dir"
    python3 -m venv "$venv_dir"

    echo "[torch] installing CUDA torch via install_torch_cuda.py"
    "$venv_dir/bin/python3" "$torch_installer"

    echo "[install] openai-whisper"
    "$venv_dir/bin/pip" install --quiet -r "$req_file"

    echo "[done] $venv_dir"
}
run_venv_whisper
