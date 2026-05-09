#!/bin/bash
set -e
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
VERSION=$(git -C "$ROOT" describe --tags --always --dirty 2>/dev/null || echo "dev")
RID="${1:-linux-x64}"
PKG_DIR="$ROOT/out/pkg"
rm -rf "$PKG_DIR" && mkdir -p "$PKG_DIR"

echo "=== Packaging RemoteBackend ($VERSION, $RID) ==="
tar -czf "$PKG_DIR/pwr-remote-${VERSION}-${RID}.tar.gz" \
    --exclude='*_venv' \
    --exclude='cache' \
    -C "$ROOT/out" \
    PowerWordRelive.RemoteBackend

echo "=== Packaging Main System ($VERSION, $RID) ==="
tar -czf "$PKG_DIR/pwr-main-${VERSION}-${RID}.tar.gz" \
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
    PowerWordRelive.RemoteBackend \
    text_data \
    setup.sh \
    setup.ps1 \
    generate_key.sh \
    generate_key.ps1 \
    config

echo ""
echo "=== Packages ==="
ls -lh "$PKG_DIR"/
