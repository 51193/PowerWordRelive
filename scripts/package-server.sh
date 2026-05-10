#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
OUT_DIR="$ROOT/out"
RELEASE_DIR="$ROOT/release"
VERSION=$(git -C "$ROOT" describe --tags --always --dirty 2>/dev/null || echo "dev")

rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

echo "=== Packaging Server Release ($VERSION) ==="

tar -czf "$RELEASE_DIR/pwr-server-${VERSION}.tar.gz" \
    --exclude='*_venv' \
    --exclude='cache' \
    -C "$OUT_DIR" \
    PowerWordRelive.RemoteBackend \
    PowerWordRelive.Infrastructure \
    setup.sh \
    setup.ps1 \
    generate_key.sh \
    generate_key.ps1 \
    config.example

echo ""
echo "=== Packages ==="
ls -lh "$RELEASE_DIR"/
