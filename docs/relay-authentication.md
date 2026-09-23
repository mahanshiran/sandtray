# Relay authentication implementation contract

## Latest local hardening (not deployed)

Authenticated startup now requires `RELAY_REPLAY_STORE`. The updated systemd
drop-in provisions `/var/lib/sandtray-relay` privately and selects `replay.json`.
Install this configuration before deploying the new binary. The store contains
only consumed nonces and expiration timestamps, never tickets or user details.
Each acceptance writes/flushed a replacement snapshot before returning success.
Store write failure rejects the ticket; corrupt storage fails startup. Exclusive
ownership prevents two processes using the same store. Actual restart replay
rejection is covered by TLS integration tests. Storage is single-host, not
distributed; filesystem power-loss behavior and high-load throughput need testing.
Do not delete the store to bypass startup errors: investigate/restore it, or wait
until all previously issued tickets expire before an explicitly managed reset.

Unconfigured startup now fails; legacy mode requires the explicit test-only
`RELAY_ALLOW_INSECURE_TEST_MODE=1` switch. This supersedes the older default-v2
description below. No automatic downgrade occurs. Pre-authentication frames are
bounded before allocation, room-code generation uses a cryptographic RNG, and
invalid role values are rejected before editor state changes.

Status: backend ticket issuance and authenticated TLS relay implemented locally;
Unity code integration added; full service/device validation and production rollout pending.
Do not deploy the endpoint as a claim that multiplayer authentication is complete.

## Ticket issuance

Authenticated `POST /api/auth/relay/ticket/` accepts `purpose: host`, or
`purpose: join` with a six-character uppercase `room_code`.
Identity comes exclusively from the authenticated active Django user.
Responses are non-cacheable, rate-limited to 20 requests per user per minute,
and contain a 60-second HS256 JWT signed with a dedicated
`RELAY_TICKET_SIGNING_KEY` (at least 32 bytes; provision a cryptographically
random secret, never reuse Django's secret or commit keys).

Claims: issuer `sandtray-api`, audience `sandtray-relay`, string account ID
`sub`, `iat`, `nbf`, `exp`, unique `jti`, and purpose. Join tickets additionally
bind `room`. A ticket grants neither editing permission nor organization access.

## Required next implementation gates

- Add encrypted, certificate-validated relay transport before transmitting tickets.
  Never transmit the API access/refresh token over the current raw TCP connection.
- Introduce a coordinated protocol version requiring authentication, without an
  unauthenticated fallback on the protected listener.
- Validate the fixed algorithm, signature, issuer, audience, required claims,
  lifetime, purpose and room before allocating a room or adding a participant.
- Atomically consume `jti` once, with bounded expiration cleanup. Multiple relay
  instances require shared replay prevention or explicitly scoped tickets.
- Bind the verified account to the connection; routing tokens and display names
  remain untrusted. Define duplicate-account/reconnect behavior and reapprove edits.
- Preserve observer-first joins and server-side host/editor enforcement. A room
  code is currently an invitation, not verified clinical workspace membership.
- Fetch a fresh ticket for reconnect; fail closed on expired credentials and do
  not log tickets. Define active-session revocation separately from ticket expiry.
- Test expired/tampered/wrong-room/replayed tickets, forged privileges, disconnect
  and transfer races, certificate failures, and physical-device host/join.
- Configure production secrets, certificate renewal, rollout and rollback only
  after compatible app and relay releases are tested together.

This design does not establish clinical consent, organization authorization,
immediate account revocation on an existing connection, or regulatory compliance.

## Implemented protected listener

Configure both `RELAY_TLS_CERTIFICATE` (PFX with private key) and
`RELAY_TICKET_SIGNING_KEY`; supply `RELAY_TLS_PASSWORD` when required. Partial
configuration fails startup. This listener requires TLS 1.2/1.3 and protocol 3.
After the normal hello exchange, CreateRoom carries one BinaryWriter string
(ticket); JoinRoom carries two strings (ticket, room code). Authentication is
validated and the nonce consumed before room allocation/join. No extra trailing
payload is accepted. Authentication errors close the connection without logging
credentials. Response/game framing is otherwise unchanged.

Accounts are bound to connections. A second connection for an account already
present in that room (including the host account) is rejected until the old
connection is removed. Reconnecting participants still require editing approval.
Replay storage is bounded to 10,000 unexpired nonces and fails closed at capacity.
It is process-local: do not deploy multiple instances sharing keys without shared
replay storage. Process restart loses the replay cache; this is a remaining
hardening item, not a durable one-use guarantee.

With neither variable configured, the old protocol-2 listener remains available
for the existing deployment and emits an unauthenticated-mode warning. There is
no protocol-2 fallback inside the protected listener. Commercial rollout must
remove access to the old listener, not leave it as an alternative route.

Local checks: `dotnet run --project RelayServer.Tests/RelayServer.Tests.csproj`,
existing `RelayServer/test_protocol.py`, and `RelayServer/test_authenticated.py`
(requires PyJWT and OpenSSL). The TLS integration test trusts only its temporary
test certificate and validates `localhost`; production clients must validate the
real server hostname and certificate without a permissive validation callback.

## Unity development configuration

`NetworkBootstrapper.UseAuthenticatedRelay` defaults to true. Host/join and each
reconnect obtain a fresh ticket via the existing authenticated HTTPS backend;
normal API tokens are never sent to the relay. TLS uses platform certificate
chain and hostname validation with revocation checking, then protocol 3.
The relay address must match the certificate, and backend signing configuration
must match the protected relay. There is no automatic plaintext fallback.
The current production v2 service is incompatible with this default; do not ship
the app until the coordinated service/device validation is complete. Ticket
request failures currently require sign-in retry rather than automatic refresh.
