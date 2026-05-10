#!/bin/bash
set -e

RID="${1:-linux-x64}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
OUT_DIR="$ROOT/out"
RELEASE_DIR="$ROOT/release"
VERSION=$(git -C "$ROOT" describe --tags --always --dirty 2>/dev/null || echo "dev")

echo "=== Packaging Local Release ($VERSION, $RID) ==="

if [[ "$RID" == win-* ]]; then
    tar -czf "$RELEASE_DIR/pwr-local-${VERSION}-${RID}.tar.gz" \
        --exclude='*_venv' \
        --exclude='cache' \
        --exclude='*.processing' \
        -C "$OUT_DIR" \
        PowerWordRelive.CLI \
        PowerWordRelive.Host \
        PowerWordRelive.Infrastructure \
        PowerWordRelive.AudioCapture \
        PowerWordRelive.SpeakerSplit \
        PowerWordRelive.Transcribe \
        PowerWordRelive.TranscriptionStore \
        PowerWordRelive.LLMRequester \
        PowerWordRelive.LocalBackend \
        PowerWordRelive.RemoteBackend \
        text_data \
        setup.ps1 \
        generate_key.ps1 \
        config
else
    tar -czf "$RELEASE_DIR/pwr-local-${VERSION}-${RID}.tar.gz" \
        --exclude='*_venv' \
        --exclude='cache' \
        --exclude='*.processing' \
        -C "$OUT_DIR" \
        PowerWordRelive.CLI \
        PowerWordRelive.Host \
        PowerWordRelive.Infrastructure \
        PowerWordRelive.AudioCapture \
        PowerWordRelive.SpeakerSplit \
        PowerWordRelive.Transcribe \
        PowerWordRelive.TranscriptionStore \
        PowerWordRelive.LLMRequester \
        PowerWordRelive.LocalBackend \
        PowerWordRelive.RemoteBackend \
        text_data \
        setup.sh \
        generate_key.sh \
        config
fi

echo "[local] pwr-local-${VERSION}-${RID}.tar.gz"
