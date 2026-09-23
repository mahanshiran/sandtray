# Schedules

## App flow

Open **Schedules** in the home sidebar. Both clients and therapists see future
requests and confirmed sessions. Therapists can choose **New request**, select a
linked client, and enter a date and local start time. The linked client's profile
also has **Schedule session** beside **Host online**.

A manual client must first be linked to an account. The client receives the
request and can accept or decline from its details. Acceptance confirms the
session and queues an email and eligible-device push for both participants.
Either participant can cancel before the start; cancellation notifies both and
stops pending reminders. To change the time, cancel and send a new request.

**Appointments have a start time only. Sessions remain unlimited after starting.**
Fifteen minutes before a confirmed appointment, its therapist can choose either
**Start a new board** or a saved board already assigned to that appointment's
client. Unrelated client boards are not offered. The ordinary authenticated
hosting, autosave, replay and loading flow is reused; scheduling does not create
a second kind of room.

The room is published to the appointment only after the therapist's board and
relay room are ready. The client detail view waits and checks automatically,
then changes to **Join session** without exposing or requiring a manually typed
room code. The existing client loading panel remains blocking until the complete
snapshot and its required objects are ready. The therapist can resume an
interrupted in-progress appointment with another room and explicitly end it;
leaving the hosted board normally also marks the appointment complete.
Accepted sessions with the exact same start time for a participant are rejected.
Without planned end times, other overlaps cannot be determined automatically.

## Reminders and delivery

- Confirmed sessions receive reminders 24 hours, one hour, ten minutes and at
  the scheduled start, for both participants, using email and enabled iOS push
  devices. Starting the room also notifies the invited client.
- A reminder whose time passed before acceptance is not created. This avoids
  sending several catch-up reminders for a short-notice session.
- The worker checks due notices every two seconds when idle. Reminders expire
  ten minutes after their due time, or at the session start, whichever is earlier.
  An extended outage skips stale reminders instead of sending a burst afterward.
- Each event/recipient and each event/device has a database uniqueness guard.
  Acceptance and cancellation retries are idempotent. Email is claimed before
  SMTP; ambiguous failures become `email_status=uncertain`, and process failures
  can leave `sending`. These are not automatically retried because delivery may
  already have occurred. Inspect provider delivery records before recovery.
- Push uses the existing EMAS retry/collapse/job-key mechanism. Delivery still
  depends on OS permission, the device's **Schedules** preference, connectivity
  and provider acceptance. Lock-screen text contains no participant names.
- Email links open a public landing page with **Open in Sandtray**. The landing
  page contains no appointment details. Push taps open the same private detail
  view directly. Authentication and participant checks are enforced by the API.
- Custom URL: `sandtray://schedule/<uuid>`. The app handles cold and warm starts
  and waits for login. iOS URL registration is added during build; Android uses
  a manifest intent filter. This follows [Unity's deep-link handling](https://docs.unity.cn/2022.1/Documentation/ScriptReference/Application-deepLinkActivated.html).
- Push currently uses the project's existing **iOS** integration. Email and the
  in-app list work for other platforms; Android native push is not implemented.
- Dates are stored in UTC. The selected date's device UTC offset is captured for
  email display. In-app times use the viewing device's local zone. Ambiguous or
  nonexistent daylight-saving times must be changed before submission.

## Backend

Authenticated endpoints under `/api/auth/schedules/`:

| Method and path | Behavior |
| --- | --- |
| GET `/` | Future pending/accepted sessions; 50 per page with `?offset=50` |
| POST `/` | Therapist proposes `{id, client_code, starts_at, utc_offset_minutes}` |
| GET `/<uuid>/` | Participant-only details, including terminal states |
| POST `/<uuid>/` | `{action: "accept", "decline", "cancel", "start", "complete"}`; start also requires a six-character `room_code` |

`id` is a client-generated UUID retained on request retries. `client_code` is the
linked account's UUID identity code. `starts_at` is an ISO timestamp with an
explicit offset. Requests must start at least five minutes in the future and
within one year. At most 100 future pending requests per therapist; write/read
endpoint throttle is 60 requests per user per hour.

Participant account locks serialize same-start acceptance. Worker locks avoid
locking joined user rows in reverse order. The public `/schedules/open/<uuid>/`
route returns a static landing page; knowing a UUID grants no access.

Only the therapist may start or complete the live session. Starting is rejected
until 15 minutes before the appointment. The room code is returned only by the
authenticated participant-only schedule endpoint and is cleared on completion.

## Operations

Release: `43c90054e59d7027ac4b1d1f71da852b57a710aa` (2026-09-15).
Migrations: `accounts.0018`, `community.0008`; additive schema changes only.

- `sandtray-schedules.service`: `deliver_schedules`, sends due email and enqueues push.
- `sandtray-push.service`: `deliver_push`, delivers device notifications.
- Both run the active API image. The deployment script restarts enabled workers
  after successful cutover so they support the current notice schema.
- SMTP credentials remain in `/etc/sandtray/api.env`; push credentials remain in
  `/etc/sandtray/push.env`. They are not copied into images.

Validation: 185 account/community tests passed with 11 existing skips. The 17
scheduling tests also passed against isolated PostgreSQL, including concurrent
acceptance and concurrent worker claims. Unity compiled; two schedule tests and
phone/desktop dialog and home-navigation render checks passed.

A new app build is required. Real-device push taps, OS link launching, and actual
inbox delivery still need end-to-end verification using intended test accounts.
Automated tests used in-memory email and mocked push; they sent no real notices.
