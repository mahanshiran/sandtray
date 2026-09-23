# Relay v4 editing revisions

Deployed with user approval on 2026-09-12. Production now runs v4. Updated Unity and phone builds are required;
the authenticated client requests v4 and rejects v3. Django ticket
claims are unchanged. Explicit insecure test mode remains v2, not a production fallback.

## Wire contract

- 33: host role grant, Int64 revision followed by the existing role/token/name payload.
- 34: client mutation, routing-token string, Int64 grant revision, Int64 sequence,
  inner type (16–21), then its original payload.
- 35/36: targeted snapshot begin/complete, each carrying the same positive Int64 ID.
- 37: client snapshot acknowledgment, carrying that Int64 ID.

The relay validates the token against the authenticated connection, current editor,
grant revision, increasing sequence, and acknowledged snapshot. Unwrapped client
mutations and old role grants are rejected in authenticated mode. The host validates
grant and sequence again on the main thread, rejecting previously forwarded actions
that became stale while queued. These are session-scoped editing revisions, not
persistent cloud-board versions or general operation IDs.

The 2026-09-13 client follow-up limits handover snapshots to the previous/new editor,
and pause/resume snapshots to the assigned editor. Unaffected observers keep their models.
Editor snapshots resend the host board, reconciling
optimistic local changes. Clients remain observers until the board is applied and
pending object loads finish. Snapshot IDs and connection-attempt checks prevent old
completion coroutines from unlocking a newer synchronization. Missing models keep
editing locked. The panel shows the existing localized restoring-board status.

## Validation and release gates

Run the authenticated TLS integration, legacy opt-in protocol suite, relay unit suite,
and Unity workflow tests. Then test two physical devices: transfer during sculpt/drag,
pause/resume, slow model loading, reconnect, and cancelling reconnect. Verify board
equality and exactly one editor. Large-board full snapshots require performance QA.

Before deployment, retain the current v3 DLL and client build for rollback and arrange
a coordinated client rollout. Do not replace the live relay alone: v3 phone builds
will become incompatible.

## Deployment record — 2026-09-12

- Service active, restart count zero; no established relay sessions at cutover.
- DLL SHA256: `1e197d26e0b8e4a0fcd8b078609a674f878924923d0aa6c2e8e6e59087c9db59`.
- Stage: `/root/RelayServer/release-v4-20260912`; previous v3 DLL retained as `previous.dll`.
- Public certificate-validated v4 handshake passed. Live synthetic backend-ticket host/join,
  targeted snapshot, readiness gate, valid edit and duplicate rejection checks passed.
- No account records created; these probes do not replace phone/Unity end-to-end tests.
- No API image, secrets, certificate or systemd configuration changed. Phone binary not installed.
- Rollback: stop `sandtray-relay`, restore staged `previous.dll` to
  `/root/RelayServer/out/RelayServer.dll`, then start the service. This restores v3,
  so v4 clients will no longer connect. Keep TLS/authentication configuration intact.
