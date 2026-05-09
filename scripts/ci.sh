#!/bin/bash
set -e
echo "=== CI: Build (Release) ==="
dotnet build -c Release

echo ""
echo "=== CI: Test ==="
dotnet test

echo ""
echo "=== CI: Package ==="
bash "$(dirname "$0")/package.sh"
