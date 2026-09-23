#!/bin/bash
# Run after CopyPlugIns, before Xcode signs Sandtray.app. Only modify the build product.
set -euo pipefail
if [[ "${CODE_SIGNING_ALLOWED:-YES}" == "NO" ]]; then exit 0; fi
: "${EXPANDED_CODE_SIGN_IDENTITY:?Missing Xcode signing identity}"
: "${TARGET_BUILD_DIR:?Missing target build directory}"
: "${PLUGINS_FOLDER_PATH:?Missing plug-in destination}"
plugin="${TARGET_BUILD_DIR}/${PLUGINS_FOLDER_PATH}/AgoraRtcWrapperUnity.bundle"
if [[ ! -d "$plugin/Contents/Frameworks" ]]; then
    echo "error: Agora nested frameworks missing: $plugin" >&2
    exit 1
fi
# The SDK export flattens versioned-framework symlinks into duplicate files.
# Restore the standard structure in the build product only. Keep displaced
# copies outside the app so a failed build remains recoverable.
backup=$(/usr/bin/mktemp -d "${TEMP_DIR:-/tmp}/sandtray-agora-layout.XXXXXX")
while IFS= read -r -d '' framework; do
    name="$(basename "$framework" .framework)"
    if [[ ! -f "$framework/Versions/A/$name" || ! -f "$framework/Versions/A/Resources/Info.plist" ]]; then
        echo "error: Unexpected Agora framework structure: $framework" >&2
        exit 1
    fi
    mkdir -p "$backup/$name"
    for entry in "$name" Resources Headers Modules Versions/Current; do
        if [[ "$entry" == Headers || "$entry" == Modules ]]; then
            [[ -e "$framework/Versions/A/$entry" ]] || continue
        fi
        if [[ ! -L "$framework/$entry" ]]; then
            if [[ -e "$framework/$entry" ]]; then
                mv "$framework/$entry" "$backup/$name/$(basename "$entry")"
            fi
            if [[ "$entry" == "Versions/Current" ]]; then
                ln -s A "$framework/$entry"
            else
                ln -s "Versions/Current/$entry" "$framework/$entry"
            fi
        fi
    done
done < <(/usr/bin/find "$plugin/Contents/Frameworks" -maxdepth 1 -type d -name '*.framework' -print0)
# Sign nested frameworks before their containing wrapper bundle.
while IFS= read -r -d '' container; do
    /usr/bin/codesign --force --sign "$EXPANDED_CODE_SIGN_IDENTITY" --options runtime "$container"
done < <(/usr/bin/find "$plugin" -depth -type d \( -name '*.framework' -o -name '*.bundle' -o -name '*.app' \) -print0)
/usr/bin/codesign --verify --deep --strict --verbose=2 "$plugin"
echo "Agora nested signatures verified."
