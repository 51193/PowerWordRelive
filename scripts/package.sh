#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
VERSION=$(git -C "$ROOT" describe --tags --always --dirty 2>/dev/null || echo "dev")
PKG_DIR="$ROOT/out/pkg"
rm -rf "$PKG_DIR" && mkdir -p "$PKG_DIR"

echo "=== Packaging RemoteBackend ($VERSION) ==="
tar -czf "$PKG_DIR/pwr-remote-${VERSION}-linux-x64.tar.gz" \
    --exclude='*_venv' \
    --exclude='cache' \
    -C "$ROOT/out" \
    PowerWordRelive.RemoteBackend

echo "=== Packaging Main System ($VERSION) ==="
tar -czf "$PKG_DIR/pwr-main-${VERSION}-linux-x64.tar.gz" \
    --exclude='*_venv' \
    --exclude='cache' \
    -C "$ROOT/out" \
    PowerWordRelive.CLI \
    PowerWordRelive.Host \
    PowerWordRelive.Infrastructure \
    PowerWordRelive.AudioCapture \
    PowerWordRelive.SpeakerSplit \
    PowerWordRelive.Transcribe \
    PowerWordRelive.TranscriptionStore \
    PowerWordRelive.LLMRequester \
    PowerWordRelive.LocalBackend \
    text_data \
    setup.sh \
    generate_key.sh \
    config

echo ""
echo "=== Packages ==="
ls -lh "$PKG_DIR"/
