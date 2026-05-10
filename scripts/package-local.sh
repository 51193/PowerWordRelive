#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
OUT_DIR="$ROOT/out"
RELEASE_DIR="$ROOT/release"
VERSION=$(git -C "$ROOT" describe --tags --always --dirty 2>/dev/null || echo "dev")

rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

echo "=== Packaging Local Release ($VERSION) ==="

tar -czf "$RELEASE_DIR/pwr-local-${VERSION}.tar.gz" \
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
    setup.ps1 \
    generate_key.sh \
    generate_key.ps1 \
    config.example

echo ""
echo "=== Packages ==="
ls -lh "$RELEASE_DIR"/
