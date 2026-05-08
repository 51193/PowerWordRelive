#!/usr/bin/env bash
set -euo pipefail

KEY=$(openssl rand -base64 32)

echo "Generated AES-256 key (base64):"
echo "$KEY"
echo ""
echo "--- Usage ---"
echo "LocalBackend:  save this key to the path specified by local_backend.key_path in config"
echo "RemoteBackend: save this key to the path specified by remote_backend.key_path in config (default: /etc/pwr/remote_backend.key)"
echo ""
echo "Example:"
echo "  mkdir -p /etc/pwr"
echo "  echo '$KEY' > /etc/pwr/remote_backend.key"
echo "  chmod 600 /etc/pwr/remote_backend.key"
