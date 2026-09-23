# Sandtray release candidate — 2026-09-23

Status: **BLOCKED — do not deploy**

This manifest makes the formerly uncommitted client and backend workspace reproducible. It does not waive failed release gates.

## Source identity

- Client branch: `release/rc-2026-09-23`
- Client implementation commit: `e1ba153899f793a3de68c4f74bca10bd4d18c14a`
- Backend repository: `https://github.com/mahanshiran/sandtray_backend.git`
- Backend branch: `release/rc-2026-09-23`
- Backend commit: `370b989d44b74a626ccb925f9c8f2c12f628d7f3`
- Unity: `2022.3.62f3c1 (1623fc0bbb97)`
- App version: `1.8`; Android bundle version code: `7`
- Backend runtime: Python 3.12 (`python:3.12-slim-bookworm`)

The root repository intentionally does not track the nested backend checkout. A release must check out both revisions above. Run `release/verify-state.sh` before building.

## Locked inputs

| File | SHA-256 |
| --- | --- |
| `Packages/manifest.json` | `8bb385a8cb79ce6fc2a63e551f0d238603d4db10dab567464930edbb0c464b96` |
| `Packages/packages-lock.json` | `d9cd6a8c54874748fd9038e18684dd12602c4b6b919be61081edd93e5f5c500f` |
| `api_backend/requirements.txt` | `9fa0e694325389cf1b864b26a9da14d11f9e1eb3091d918e6c908c8ece81fa75` |
| `ProjectSettings/ProjectVersion.txt` | `b42279cfd794d9f1825f3b7c1f318b861fa9e2e2b3c6c146737bdbd41c01b389` |

## Verified gates

- Client and editor C# projects compile with `msbuild`.
- Relay executable checks pass: ticket validation, durable replay, and five hosting-relay checks.
- Backend Django system check passes under Python 3.12.
- Backend migration drift check reports no changes.
- Backend suite runs 359 tests successfully with 18 expected skips.
- Secret-signature scan of staged release content found no private-key or common cloud-credential signatures.
- Git LFS tracks the modified Agora macOS binary, which is a valid universal `x86_64`/`arm64` binary.

## Blocking gate

An isolated clean-worktree Unity EditMode run at the client implementation commit completed 436 tests: 414 passed and 22 failed. The earlier account-state cascade has been removed, so these are now independently visible failures rather than one contaminated run.

Failure areas include board/client labels, account-transition test setup, client workflows, localization/font assets, heightmap-region serialization, catalog readiness, report privacy/archive flows, recorder expectations, settings/profile UI expectations, social-login UI, and edit-mode object cleanup.

The candidate must not be tagged, pushed as a release, or deployed until the EditMode suite passes or each exception is explicitly reviewed and documented. Signed iOS/Android device checks and production-configuration validation remain required afterward.

## Reproduction

```bash
release/verify-state.sh
msbuild Assembly-CSharp.csproj /nologo /v:minimal
msbuild Assembly-CSharp-Editor.csproj /nologo /v:minimal

python3.12 -m venv /tmp/sandtray-backend-release
/tmp/sandtray-backend-release/bin/pip install -r api_backend/requirements.txt
/tmp/sandtray-backend-release/bin/python api_backend/manage.py check
/tmp/sandtray-backend-release/bin/python api_backend/manage.py makemigrations --check --dry-run
/tmp/sandtray-backend-release/bin/python api_backend/manage.py test
```

Run Unity tests from a clean detached worktree so the open editor and its generated `Library` do not affect the result:

```bash
/Applications/Unity/Hub/Editor/2022.3.62f3c1/Unity.app/Contents/MacOS/Unity \
  -batchmode -nographics -projectPath /path/to/clean/worktree \
  -runTests -testPlatform EditMode \
  -testResults /tmp/sandtray-editmode-results.xml \
  -logFile /tmp/sandtray-editmode.log
```
