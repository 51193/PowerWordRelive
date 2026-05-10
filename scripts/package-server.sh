#!/bin/bash
set -e

RID="${1:-linux-x64}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
OUT_DIR="$ROOT/out"
RELEASE_DIR="$ROOT/release"
VERSION=$(git -C "$ROOT" describe --tags --always --dirty 2>/dev/null || echo "dev")

echo "=== Packaging Server Release ($VERSION, $RID) ==="

if [[ "$RID" == win-* ]]; then
    tar -czf "$RELEASE_DIR/pwr-server-${VERSION}-${RID}.tar.gz" \
        --exclude='*_venv' \
        --exclude='cache' \
        -C "$OUT_DIR" \
        PowerWordRelive.RemoteBackend \
        PowerWordRelive.Infrastructure \
        generate_key.ps1 \
        config
else
    tar -czf "$RELEASE_DIR/pwr-server-${VERSION}-${RID}.tar.gz" \
        --exclude='*_venv' \
        --exclude='cache' \
        -C "$OUT_DIR" \
        PowerWordRelive.RemoteBackend \
        PowerWordRelive.Infrastructure \
        generate_key.sh \
        config
fi

echo "[server] pwr-server-${VERSION}-${RID}.tar.gz"
