#!/bin/bash
set -euo pipefail
root=$(cd "$(dirname "$0")" && pwd)
out=$(mktemp -d /tmp/sandtray-webview-build.XXXXXX)
xcodebuild -project "$root/WebView.xcodeproj" -target WebView -configuration Release \
  -arch x86_64 -arch arm64 CONFIGURATION_BUILD_DIR="$out/products" \
  OBJROOT="$out/obj" SYMROOT="$out/sym" MACOSX_DEPLOYMENT_TARGET=10.13 \
  CODE_SIGNING_ALLOWED=NO build
bundle="$root/../../Assets/Plugins/WebView.bundle"
# Unity treats .bundle as one plugin asset; do not add .meta files inside it.
python3 - "$out/products/WebView.bundle" "$bundle" <<'PY'
import shutil,sys
shutil.rmtree(sys.argv[2]);shutil.copytree(sys.argv[1],sys.argv[2])
PY
codesign --force --sign - "$bundle"
codesign --verify --verbose "$bundle"
