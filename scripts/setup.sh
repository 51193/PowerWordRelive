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
run_venv funasr PowerWordRelive.Transcribe
