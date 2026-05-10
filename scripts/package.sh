#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
OUT_DIR="$ROOT/out"
RELEASE_DIR="$ROOT/release"
VERSION=$(git -C "$ROOT" describe --tags --always --dirty 2>/dev/null || echo "dev")

rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

bash "$SCRIPT_DIR/package-local.sh"
bash "$SCRIPT_DIR/package-server.sh"

echo ""
echo "=== All Packages ($VERSION) ==="
ls -lh "$RELEASE_DIR"/
