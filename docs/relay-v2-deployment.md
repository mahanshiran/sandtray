# Relay v2 deployment — 2026-09-12

Deployed with user approval to `43.99.51.164:7777`, service `sandtray-relay`.
Django/API services were not modified. Older app builds without protocol v2 cannot connect.

## Release

- Active DLL: `/root/RelayServer/out/RelayServer.dll`
- SHA-256: `2b44717cea44694f4b9b6e77ade87391f4e6b6641b9d9861d3d79a78da57797c`
- Staged release: `/root/RelayServer/release-v2-tj94lBKA`
- Previous release backup: `/root/RelayServer/backup-before-v2-tj94lBKA`
- Service remains enabled at boot with automatic restart.
- No established relay connections were observed before deployment.

The release also serializes join confirmation before exposing a client to broadcasts,
reserves generated room codes under the room-map lock, and clears host references on shutdown.

## Verification

Local `test_protocol.py` passed, including 20 joins during concurrent broadcasts.
`python3 RelayServer/smoke_remote.py 43.99.51.164` passed against the deployed service:
v2 handshake, room creation/join, observer mutation rejection, approved editor traffic,
status delivery, targeted snapshot routing, participant removal and heartbeat.
The probe creates a new temporary room and closes its sockets; it does not inspect user rooms.

This is relay protocol verification, not full Unity/device or commercial-security validation.
Verified account identity, secure transport review, workspace authorization and device testing remain open.

## Rollback

Run on the relay server only if rollback is necessary. This interrupts current sessions and
restores the old protocol, so updated v2 apps will no longer connect.

```sh
systemctl stop sandtray-relay
cp /root/RelayServer/backup-before-v2-tj94lBKA/RelayServer.dll /root/RelayServer/out/RelayServer.dll
cp /root/RelayServer/backup-before-v2-tj94lBKA/RelayServer.deps.json /root/RelayServer/out/RelayServer.deps.json
cp /root/RelayServer/backup-before-v2-tj94lBKA/RelayServer.runtimeconfig.json /root/RelayServer/out/RelayServer.runtimeconfig.json
systemctl start sandtray-relay
systemctl is-active sandtray-relay
```
