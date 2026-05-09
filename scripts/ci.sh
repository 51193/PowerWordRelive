#!/bin/bash
set -e

# 从 CI tag 读取版本号（如 v1.0.0 → 1.0.0），本地构建用 Directory.Build.props 中的默认值
BUILD_ARGS="-c Release"
if [ -n "$GITHUB_REF" ] && [[ "$GITHUB_REF" == refs/tags/v* ]]; then
    TAG_VERSION="${GITHUB_REF#refs/tags/v}"
    BUILD_ARGS="$BUILD_ARGS -p:Version=$TAG_VERSION"
    echo "=== Building version $TAG_VERSION from tag ==="
fi

echo "=== CI: Build ==="
dotnet build $BUILD_ARGS

echo ""
echo "=== CI: Test ==="
dotnet test

echo ""
echo "=== CI: Package ==="
bash "$(dirname "$0")/package.sh"
