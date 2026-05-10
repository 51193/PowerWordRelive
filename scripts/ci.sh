#!/bin/bash
set -e

RID="${1:-linux-x64}"

BUILD_ARGS="-c Release"
if [ -n "$GITHUB_REF" ] && [[ "$GITHUB_REF" == refs/tags/v* ]]; then
    TAG_VERSION="${GITHUB_REF#refs/tags/v}"
    BUILD_ARGS="$BUILD_ARGS -p:Version=$TAG_VERSION"
    echo "=== Building version $TAG_VERSION from tag ==="
fi

echo "=== CI ($RID): Build ==="
dotnet build $BUILD_ARGS

echo ""
echo "=== CI: Test ==="
dotnet test

echo ""
echo "=== CI ($RID): Package ==="
bash "$(dirname "$0")/package.sh" "$RID"
