# Session security and local record isolation — September 15, 2026

## Implemented

- Initial relay host/join responses use a bounded control reader: lengths outside 1–4096 are rejected before allocating a body. Truncated responses fail closed. Reconnect already had this bound.
- Ticket acquisition receives the originating connection generation, rather than reading a potentially newer attempt when a worker starts. Host/join/reconnect recheck cancellation after TLS negotiation, before sending the control request. Existing certificate validation and authenticated protocol behavior are preserved.
- SessionManager captures an account/workspace guard when created. Every access through its saved directory, session capture/application and table writer checks that guard. An old manager stays blocked after the global workspace transitions successfully, including switching back to the same account with a new epoch.
- Autosave checks the pinned workspace. Delayed record-model restoration checks it while waiting for the catalog and after model loading, before scene lookup or placement.
- Runtime report-template stores capture an account guard. Stale stores cannot read, repair backups, save or delete templates. Existing per-account template paths are preserved; this does not migrate legacy files.

## Validation

Targeted EditMode checks cover malformed/truncated relay control frames, stale template reads/writes/deletion/backup repair, old-manager access after an epoch change, and existing board/client/session workflows. **96 passed, 0 failed** in the final targeted run. Results: `/tmp/sandtray-security-records-final.xml`; log: `/tmp/sandtray-security-records-final.log`. This compiled scripts for EditMode tests only; no player build was produced.

An expanded run additionally exercised `BoardClientLabelTests`: three UI checks failed (missing expected client label and EditMode `Destroy` errors in `RefreshBoardList`). Those UI failures remain unresolved; the final passing filter covers session routing, report templates, board autosave/records, client organization and client-session workflows.

## Remaining boundaries

These changes do not enable ownership/capacity rollout or claim unassigned legacy records. They do not add encrypted local storage, server-side record audit, or shared replay storage for multiple relay instances. Cancellation checks cannot retract a request already sent. Real TLS failures, account changes during connections, mobile lifecycle behavior and full ownership migration remain release checks. No app build or backend deployment is part of this change.

## Board-label follow-up

The three board-label failures above are resolved. My Boards now names its client text consistently, disables rich-text parsing so user names display literally, and makes the badge ignore raycasts. Home card overflow-caption cleanup uses the appropriate destruction method in EditMode. Adjacent table rows are checked for non-overlap rather than the obsolete card-gap layout. Rename and missing-client assertions now require one label in each list, avoiding false passes when a label is absent.

The search fixture now seeds historical mixed-author data directly instead of switching identities through a live session manager. Its stale-action check verifies the unchanged file directly and asserts that the old manager cannot read it after logout.

**7 tests passed, 0 failed**, covering BoardClientLabelTests, HomeNavigationTextTests and RecordSearchWorkspaceTests. Results: `/tmp/sandtray-board-label-fix.xml`; log: `/tmp/sandtray-board-label-fix.log`. No app build or device visual validation.

## Screenshot and replay-preview isolation

Screenshot managers now capture their original account/workspace guard in Awake. Capture, screenshot save, thumbnail save and replay-preview save reject a stale manager, including after a new workspace is active. Capture rechecks the guard after camera rendering; thumbnail save checks before directory creation. Screenshot names are treated as filenames, not paths.

Replay-preview reads, writes and deletion accept only `.sandlog` sidecars directly in the active workspace's Sessions directory. Outside-account paths, nested paths, other file types and linked files/directories are rejected. Existing preview filename conventions are retained. This does not assign legacy ownership or migrate replay files.

Replay deletion captures its account guard when confirmation opens and rechecks both guard and path before removing the recording or preview. A stale confirmation leaves both files untouched and does not reopen another account's replay screen.

**60 targeted EditMode tests passed, 0 failed**, covering ScreenshotOwnershipTests, BoardAutoSaveTests and SessionRecorderTests. Includes a stale confirmation with preserved recording/preview bytes, invalid preview paths and stale screenshot entry points. The existing recorder fixture now models the minimum retained duration instead of expecting an immediately discarded short recording to exist. Results: `/tmp/sandtray-image-isolation.xml`; log: `/tmp/sandtray-image-isolation.log`.

Actual rendering, native export and device account-switch lifecycle remain unvalidated. No app build or deployment was performed.

## Replay playback and export follow-up

Loaded replay players capture the workspace identity. Play, seek, timed event dispatch and completion stop when that guard becomes stale; seeking rechecks between events so an account change inside one callback prevents the next event. Replay UI entry validates that the selected file belongs to the active recording directory before clearing the sandbox.

Video export requires a current replay and checks ownership after frame waits, during frame capture/padding and before native sharing. Cancellation and coroutine exit dispose the AVI writer, cancel a started native encoder and remove unfinished output. Completed files are preserved. Output names include a unique identifier to avoid overwriting another export started in the same second. Export generations prevent cleanup from an old coroutine affecting a newer export.

Focused tests cover seek callbacks that change accounts, repeated cancellation releasing the file handle, preservation of completed files, and an account switch while awaiting the next export frame. Actual device video encoding/sharing and mobile lifecycle behavior remain release checks; no app build or deployment.

Final replay/export validation: **16 passed, 0 failed**. Results: `/tmp/sandtray-replay-isolation-final.xml`; log: `/tmp/sandtray-replay-isolation-final.log`.
