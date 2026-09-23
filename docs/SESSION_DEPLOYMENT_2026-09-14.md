# Session testing deployment — September 14, 2026

Authorized by the user to deploy the backend for session testing. Deployment completed successfully.

## Scope and artifacts

- API: `https://api.sandtraypro.com`, active blue slot.
- API image: `sandtray-api:23fd0bed894abb84809fbf8b287d672d0004825c` (content-derived release ID, not a Git commit).
- Base: production image `b1613586be11` / `sandtray-api:report-notifications-candidate`. Session update adds `accounts/session_profiles.py` and its URL to the deployed URL configuration, preserving existing endpoints. Session/authentication test modules were included in the image.
- No database migrations were pending or applied. No production user records were created or modified by the smoke probe.
- Relay: current workspace Release build, authenticated TLS protocol v4, `api.sandtraypro.com:7777`.
- Relay DLL SHA256: `9aaf964d7360244208534ebd2a998c90370f45231b2062ef855b3b7df1b2f41c`.
- Relay stage/rollback: `/root/RelayServer/release-session-20260914`; prior complete output saved under `previous/` before replacement. No active relay connections were observed immediately before restart.
- Subscription, hosting-metering and cloud rollout were not enabled. New access-system models/routes and unrelated pending backend changes were not part of this targeted session deployment. Existing push worker remains on its previous image.

## Verification

- Candidate API: 7 session-profile and relay-authentication tests passed against an isolated SQLite test database; no test migrations against production.
- Candidate Django migration plan: no pending operations.
- Relay authenticated TLS integration passed, including durable replay rejection after process restart.
- Relay session-profile membership/race tests passed. Updated their harness to include the current HostingMeter source dependency.
- Live synthetic-principal probe passed certificate-validated v4 handshake, host/join, role assignment, relay-to-API session-profile response, targeted snapshots, heartbeat, participant removal and ticket-replay rejection. Probe identities were checked absent from the database; no real account impersonation or writes.
- Public readiness returned ready. Anonymous profile access rejected with 403; anonymous relay-ticket request rejected with 401.
- Relay active/running with restart count zero after deployment.

Existing Django deployment warnings remain for HSTS, application SSL redirect and secure session/CSRF cookie settings. This session deployment did not change those settings or certify full release readiness.

## User/device test still required

Use the current Unity workspace and a compatible updated v4 device build. Validate real login, host/join, participant names/photos, editing handoffs, audio/video, background/reconnect and save recovery. Older v3/v2 binaries cannot join this relay. No AAB/iOS binary was built or installed by this deployment.

## Rollback

The previous API container is retained in green; use the existing `sudo sandtray-rollback` operation if needed. No schema rollback is required for this release. For relay rollback, stop `sandtray-relay`, restore the three RelayServer DLL/deps/runtimeconfig files from `previous/` to `/root/RelayServer/out/`, and start the service. Preserve TLS configuration, signing keys and replay storage. Coordinate any restart with active sessions. Prior relay is also v4 but lacks the newly deployed profile support.
