# Board autosave and recovery

Boards save locally every 30 seconds while open. The timer uses unscaled time,
so pausing simulation does not pause autosave. Unchanged scenes are not rewritten.
The app also attempts to save on loss of focus, application pause, orderly quit,
and Return to menu. Joined multiplayer guests do not overwrite a local host board.

The bottom status bar reports autosave, success, recovery, or failure. If saving
fails during Return to menu, the board stays open and an error explains what to do.
Free storage and retry leaving the board; periodic saving also retries automatically.
No API/server change is required.

## After an interrupted session

Open the board normally from Home, My Boards, or its client page. The latest
completed autosave is the board's regular save file; there is no separate import.
A forced kill or power loss can still lose changes since the last successful save
(normally up to 30 seconds). Lifecycle callbacks cannot run after a forced kill.

Writes use the existing temporary-file/atomic-replacement mechanism. The previous
save remains at `Sessions/<board>.json.bak`. If the main file is missing or invalid,
loading uses this previous copy. Invalid main files are retained with a unique
`.corrupt-…` suffix, and the backup is not overwritten during recovery. If neither
copy is readable, the app does not open an empty board and autosave over the files.
Backups with missing primary files still appear in the board lists.

Client association, creation date, therapist notes, and reports are retained when
the scene is saved. Report updates now use atomic writes too. Rename removes the
old-name recovery copy. Explicit board deletion removes its `.bak` as well, so a
deleted board does not reappear; deletion is not an undo/recovery feature.

Timer saves wait while network objects are restoring. Lifecycle/exit saves retain
unrestored object entries alongside the visible objects, and failed downloads keep
their entries for a later load. This preserves data; it does not fix unavailable
models or introduce an asset retry interface.

## Verification

`Assets/Editor/Tests/BoardAutoSaveTests.cs` covers timer/lifecycle saving, unchanged
scenes, object transforms, client/report metadata, interrupted sessions, corrupt
and missing primary files, invalid terrain, different terrain resolutions, rename,
deletion, pending downloads, failed writes/retry, and the save-failure exit dialog.
Client organization and board-label tests are included in the regression run.

These are isolated Unity Editor checks. Installed mobile apps and browser builds
still need lifecycle/storage smoke tests. This is device-local recovery, not cloud
backup, cross-device sync, or protection against losing the device/storage itself.
