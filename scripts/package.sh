#!/bin/bash
set -e

RID="${1:-linux-x64}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
ROOT="$(dirname "$SCRIPT_DIR")"
RELEASE_DIR="$ROOT/release"

rm -rf "$RELEASE_DIR"
mkdir -p "$RELEASE_DIR"

bash "$SCRIPT_DIR/package-local.sh" "$RID"
bash "$SCRIPT_DIR/package-server.sh" "$RID"

echo ""
echo "=== All Packages ==="
ls -lh "$RELEASE_DIR"/
