# Local board checkpoints and restore

Implemented 2026-09-14. No app build or deployment performed.

Open a board's overflow menu → Version history, or Search records → board result → Version history. Checkpoint saved board creates a version of the last saved layout. Up to 20 valid checkpoints are retained per board. This is explicit checkpoint creation, not automatic capture of every editing action or unsaved stroke.

Restore shows a confirmation describing the changes. The board must be closed. Objects, terrain, paint and board dimensions are restored together; current reports, notes, client associations, assignment history and archive state remain current. A durable safety checkpoint of the current layout is required before replacement. Users reopen the board to load the restored layout.

Versions are stored below the local Sessions directory in `.versions/<stable-history-id>/`. The identifier survives normal saves and board renames. Explicit deletion of a readable board removes its version directory. Checkpoints contain layout data only, not report or private-note copies. Like existing local records, this storage is neither account-isolated nor encrypted. Unreadable board files whose history identifiers cannot be recovered may leave orphaned version files; they are not listed as live boards.

Validation checks the snapshot's board identifier, finite object transforms, terrain and paint encoding, positive finite dimensions, and SHA-256 fingerprints. A board changed since confirmation or a modified checkpoint cannot be restored through a stale action. Atomic primary/backup writes preserve the current save on failure. Filename inputs for history directories and checkpoints must be exact GUIDs.

44 automated Unity checks passed across version and save behavior. Coverage includes safety checkpoints, metadata preservation, archive state, active-board rejection, stale confirmations, corrupt/tampered snapshots, path rejection, history retention, renames/deletion, failed writes, and confirmation/cancellation/account-change UI flows. English/Chinese desktop and phone previews were rendered and inspected. Labels cover all eight app languages.

Remaining: updated builds and physical-device interaction checks, cloud version synchronization, and recovery for orphaned version files. Board thumbnails are regenerated through the existing screenshot flow after reopening; restoring a checkpoint does not generate a new rendered thumbnail immediately.
