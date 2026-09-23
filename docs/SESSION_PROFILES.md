# Live-session participant profiles

Implemented locally on 2026-09-13. Not deployed. Requires the updated backend,
relay and Unity app; no database migration is needed. Existing clients continue
to use protocol v4, but cannot display the new profile messages.

## Behavior

Circular avatars appear after room controls in the meeting bar. Photos fall back
to initials. Desktop width expands up to 1120 UI units; smaller screens scroll.
Tapping opens a read-only, scrollable profile. Ordinary users show their account
name and avatar. Therapists additionally show populated professional fields and
qualifications. Empty/whitespace/null values and empty qualifications are omitted.
Credentials remain self-reported. The editor explains session visibility in all
eight languages. Images are center-cropped into circles.

## Membership and data boundaries

Client message 40 requests the current session projection; message 41 is a
relay-only JSON response. The relay resolves account IDs from authenticated
connections and binds their routing tokens. It signs a 15-second assertion with
the existing dedicated relay key, audience `sandtray-session-profiles`, and sends
it over HTTPS to `POST /api/auth/session-profiles/`. App JWTs cannot authorize
this endpoint. The API returns only the explicit session projection; it never
returns login email, subscription details, client records, notes or legacy bio.
The existing owner-only therapist endpoints remain unchanged.

Backend thumbnails for professional photos are at most 128×128. Personal avatar
URLs are loaded by the app over HTTPS without API credentials. Responses are
private/no-store. Profiles are held in session memory, cleared on cleanup and
excluded from replay recording. They are visible to session members, who can
still take screenshots or retain information they have already seen.

The client polls every 10 seconds. Relay limits requests to one per connection
per five seconds and one in flight, with a five-second upstream timeout. Network
I/O runs outside the room lock. Before replying, the relay rechecks requester
membership and the full token/account map. Membership changes invalidate the
result. Departure/profile changes can take up to the next successful poll to
appear in the UI; loss of upstream service can leave the last in-session view
until reconnect or cleanup. There is no public search or ID-addressed app API.

## Validation and release

- 41 Django tests passed, including projection authorization, claim expiry,
  account data exclusion and normal/inactive account handling.
- Unity compilation and profile checks passed, including optional-field filtering
  and withholding therapist fields for normal users. Existing therapist checks
  and translation coverage passed.
- Relay build clean; existing authenticated TLS protocol-v4 integration passed.
- `python3 RelayServer/test_session_profiles.py` passed outsider/payload rejection,
  signed membership projection, duplicate-request suppression and membership-race
  response discard without contacting production.
- Phone (390×844) and desktop (1200×900) detail previews generated; phone long-bio
  and qualification scrolling visually inspected.

Deploy backend first, then the relay, then updated clients. Coordinate relay
restart with active sessions. Update user-facing privacy documentation to match
session profile sharing before production rollout. Real multi-device/avatar
loading and lifecycle verification remain release gates.

## Deployed for session testing — September 14, 2026

Backend session-profile endpoint and matching v4 relay are now deployed. Live synthetic host/join/profile delivery checks passed. Updated real-device clients and UI/audio/reconnect testing are still required. See [artifacts, validation and rollback](SESSION_DEPLOYMENT_2026-09-14.md).
