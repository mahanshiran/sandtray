#!/usr/bin/env bash
set -euo pipefail

root_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
backend_dir="$root_dir/api_backend"
expected_backend="370b989d44b74a626ccb925f9c8f2c12f628d7f3"
expected_backend_remote="https://github.com/mahanshiran/sandtray_backend.git"

fail() {
  printf 'release state check failed: %s\n' "$1" >&2
  exit 1
}

[[ -d "$backend_dir/.git" ]] || fail "api_backend is not an independent Git checkout"
[[ -z "$(git -C "$root_dir" status --porcelain --untracked-files=all)" ]] || fail "client worktree is dirty"
[[ -z "$(git -C "$backend_dir" status --porcelain --untracked-files=all)" ]] || fail "backend worktree is dirty"
[[ "$(git -C "$backend_dir" rev-parse HEAD)" == "$expected_backend" ]] || fail "backend revision does not match the manifest"
[[ "$(git -C "$backend_dir" config --get remote.origin.url)" == "$expected_backend_remote" ]] || fail "backend origin does not match the manifest"

check_hash() {
  local expected="$1"
  local path="$2"
  local actual
  actual="$(shasum -a 256 "$root_dir/$path" | awk '{print $1}')"
  [[ "$actual" == "$expected" ]] || fail "$path hash does not match the manifest"
}

check_hash "8bb385a8cb79ce6fc2a63e551f0d238603d4db10dab567464930edbb0c464b96" "Packages/manifest.json"
check_hash "d9cd6a8c54874748fd9038e18684dd12602c4b6b919be61081edd93e5f5c500f" "Packages/packages-lock.json"
check_hash "9fa0e694325389cf1b864b26a9da14d11f9e1eb3091d918e6c908c8ece81fa75" "api_backend/requirements.txt"
check_hash "b42279cfd794d9f1825f3b7c1f318b861fa9e2e2b3c6c146737bdbd41c01b389" "ProjectSettings/ProjectVersion.txt"

printf 'client:  %s\n' "$(git -C "$root_dir" rev-parse HEAD)"
printf 'backend: %s\n' "$expected_backend"
printf 'release state is clean and dependency inputs match the manifest\n'
