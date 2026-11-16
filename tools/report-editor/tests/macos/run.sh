#!/bin/bash
set -euo pipefail
here=$(cd "$(dirname "$0")" && pwd)
root=$(cd "$here/../../../.." && pwd)
unity=${UNITY_EXECUTABLE:-/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity}
project=$(mktemp -d /tmp/sandtray-mac-clipboard-test.XXXXXX)
mkdir -p "$project/Assets/Editor" "$project/Assets/StreamingAssets/ReportEditor" "$project/Packages" "$project/ProjectSettings"
cp "$root/ProjectSettings/ProjectVersion.txt" "$project/ProjectSettings/"
cp "$root/Assets/Scripts/UI/RichReportEditor.cs" "$here/NativeRichHarness.cs" "$project/Assets/"
cp "$here/NativeRichCheck.cs" "$project/Assets/Editor/"
cp "$root/Assets/StreamingAssets/ReportEditor/index.html" "$project/Assets/StreamingAssets/ReportEditor/"
cp "$here/clipboard-image.png" "$project/"
python3 - "$root" "$project" <<'PY'
import json,sys,pathlib
root,project=map(pathlib.Path,sys.argv[1:])
deps={name:'1.0.0' for name in ['com.unity.ugui','com.unity.modules.ui','com.unity.modules.imgui','com.unity.modules.unitywebrequest','com.unity.modules.jsonserialize']}
deps['net.gree.unity-webview']='file:'+str(root/'Packages/net.gree.unity-webview')
(project/'Packages/manifest.json').write_text(json.dumps({'dependencies':deps}))
PY
swiftc "$here/clipboard.swift" -o "$project/clipboard"
"$project/clipboard" save "$project/original-clipboard.plist"
trap '"$project/clipboard" restore "$project/original-clipboard.plist"' EXIT
"$unity" -batchmode -projectPath "$project" -executeMethod NativeRichCheck.Run -logFile "$project/unity.log"
cat "$project/shortcuts-result.txt"
grep -q '^PASS:' "$project/shortcuts-result.txt"
