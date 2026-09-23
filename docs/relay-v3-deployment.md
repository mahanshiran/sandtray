# Authenticated relay deployment — 2026-09-12

## Durable replay hardening deployment — 2026-09-12

Deployed with user approval. Active DLL SHA256:
`482721706891f5c03d1529040bc682e6b9046539883dd669994cdad8ad4fde4a`.
Staging and previous DLL/drop-in: `/root/RelayServer/release-durable-20260912`.
Includes restart-persistent replay storage, fail-closed startup, bounded pre-auth
frames, cryptographic room codes and invalid-role rejection. Updated systemd
drop-in creates private `/var/lib/sandtray-relay` with restrictive umask.
Extended live TLS host/join/role/snapshot/removal/replay probe passed; no sessions
were observed before cutover. API code unchanged.

Rollback requires restoring both `previous.dll` and `previous.conf` from this
directory, daemon-reload and a controlled relay restart. Preserve replay storage.
Do not delete it to work around errors. Real-device reconnect testing remains open.

## TLS routing hotfix — 2026-09-12

User reported immediate disconnects. Reproduced against the original v3 DLL:
role assignment sent plaintext through `TcpClient.GetStream()`, corrupting TLS.
Targeted snapshots and removal had the same bypass. All three now use the stored
connection stream. Extended local and live tests cover registration, role
assignment, all three snapshot types and removal, in addition to heartbeat/replay.
Old DLL fails the role test with an SSL record-layer error; hotfix passes.

Hotfix active DLL SHA256:
`a82fe10e6b28c19eddd8dabf0711a84c23515202f474f33fe906bbaf3ed76c0d`.
Staging/previous DLL: `/root/RelayServer/release-v3-tls-routing-fix`.
No established sessions observed before restart. Service active and extended live
synthetic-principal probe passed. Backend and client unchanged for this hotfix.
The original v3 smoke test missed targeted messages; it was insufficient to
validate a real join. Real-device confirmation remains required.

Deployed with user approval to `api.sandtraypro.com:7777` (TLS, protocol 3).
Legacy v2 clients cannot connect. Unity's relay hostname was corrected from the
raw IP to the certificate hostname in this workspace; no app binary was released.

## Deployed artifacts

- Relay DLL SHA256: `92314f059815e1f840e5618606ef5f3d3ef6d318cb11d9b0ba2f99ece4582844`.
- Staging: `/root/RelayServer/release-v3-20260912`.
- Relay/API-env backup: `/root/RelayServer/backup-before-v3-20260912`.
- API image: `sandtray-api:3797fa34ee79bb8b0293b11e6d44360d206f8135`, blue slot.
  This is an operational artifact identifier, not a Git commit. It layers only
  `accounts/relay.py` and `accounts/urls.py` over the prior production image.
- Prior API image/container retained in green for rollback; no migrations applied.
- Dedicated signing secret provisioned server-side in API and relay environment
  files, mode 0600. Never copy these files into source control.
- Systemd relay drop-in reads `/etc/sandtray/relay.env`.
- PFX certificate at `/etc/sandtray/secrets/relay.pfx`, mode 0600.
- Certbot deploy hook refreshes the PFX and restarts the relay on renewal. This
  restart interrupts sessions; graceful renewal remains a future improvement.

## Infrastructure repair

Nginx had failed before deployment because legacy `sandtray.service` occupied
port 8000. Existing container readiness was healthy. Stopped and disabled the
legacy service, started Nginx, then used the existing blue/green deploy script.
Do not re-enable the legacy service alongside Nginx.

## Verification

- Public HTTPS `/ready/`: 200.
- Anonymous POST `/api/auth/relay/ticket/`: 401.
- Live backend view issued synthetic-principal tickets; certificate-validated
  TLS host/join and heartbeat succeeded; reused ticket rejected.
- No real user records were accessed or created by that probe. It does not test
  a real login, Unity client, or a physical device.
- Nginx and relay active; relay restart count zero after deployment.
- No established relay sessions were observed immediately before cutover.

## Remaining release risks

Real Unity/device validation, durable replay storage (currently process-local),
credential expiry/revocation UX, workspace membership authorization, and mobile
TLS compatibility remain open. Django deployment checks also reported existing
HSTS, HTTPS redirect and secure-cookie warnings. This deployment is not a claim
of clinical/commercial readiness.

## Rollback (operator action, interrupts sessions)

Stop `sandtray-relay`; move its `authentication.conf` systemd drop-in out of the
drop-in directory; restore the three RelayServer DLL/deps/runtimeconfig files
from the backup above; run `systemctl daemon-reload` and start the relay.
This restores unauthenticated v2 and breaks protocol-3 clients, so only use it
as an explicitly accepted rollback. Retain backups and secrets for recovery.

If API rollback is also required, run `/usr/local/sbin/sandtray-rollback` to the
retained green slot. Leave the legacy `sandtray.service` disabled. Do not restore
the API environment wholesale unless its later changes have been reviewed.
