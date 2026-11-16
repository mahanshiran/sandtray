# Scheduled live-session recovery

Leaving a scheduled online board previously sent `complete` and removed the
appointment from Ongoing. Leaving or disconnecting now sends `pause` with the
published relay room code. The appointment remains `in_progress` until the
therapist explicitly selects End session. This permits recovery beyond one hour
and avoids imposing a new time limit on active sessions.

Pause clears only the matching room code; late callbacks from an older room
cannot clear a replacement. A crash that cannot send pause also leaves the
appointment recoverable. Restart uses the existing board chooser, publishes a
new room code and resets the client's ready notification generation. The client
can wait in session details, which refreshes when the new room is published.
Restarting skips the one-time free-feature claim; relay hosting authorization
and usage metering still apply. A repeated publication of the same room does
not resend notices. Tests are unchanged by this online-session lifecycle.

Validation: schedule lifecycle regressions cover normal leave, crash without a
pause request, restart after two hours, stale disconnects, client permissions,
notification idempotency and explicit completion. The existing scheduled-test
suite also passes. C# editor assembly builds with zero errors. Native two-player
reconnection still requires an interactive Unity run.
