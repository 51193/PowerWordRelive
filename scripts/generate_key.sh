#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

LOCAL_KEY_FILE="$SCRIPT_DIR/local_key.bin"
REMOTE_KEY_FILE="$SCRIPT_DIR/remote_key.bin"

LOCAL_KEY=$(openssl rand -base64 32)
REMOTE_KEY=$(openssl rand -base64 32)

echo "$LOCAL_KEY" | tr -d '\n' > "$LOCAL_KEY_FILE"
echo "$REMOTE_KEY" | tr -d '\n' > "$REMOTE_KEY_FILE"
chmod 600 "$LOCAL_KEY_FILE" "$REMOTE_KEY_FILE"

echo "Generated AES-256 keys:"
echo "  Local:  $LOCAL_KEY_FILE"
echo "  Remote: $REMOTE_KEY_FILE"
echo ""
echo "--- Usage ---"
echo "Local key:  Host generates this automatically for local web mode."
echo "            This file is for manual testing / standalone deployment."
echo ""
echo "Remote key: Copy this file to both:"
echo "  Client:   path specified by remote_mode.remote.key_path in config"
echo "  Server:   path specified by remote_mode.server.key_path in config"
echo ""
echo "Example:"
echo "  # On client machine"
echo "  cp $REMOTE_KEY_FILE ./keys/remote_key.bin"
echo ""
echo "  # On server machine"
echo "  mkdir -p /etc/pwr"
echo "  cp remote_key.bin /etc/pwr/remote_key.bin"
echo "  chmod 600 /etc/pwr/remote_key.bin"
