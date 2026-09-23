# Shared report publication history

Implemented server snapshots for each successful initial publication and changed publication, recording the authenticated author, server timestamp, revision, table label, source and exact recipient text. Identical retries are idempotent. Snapshot failure rolls back the publication. PostgreSQL author-row locking serializes concurrent creation and updates; stale changed revisions return 409.

The author's report Versions view offers Shared history, with paginated summaries and read-only exact-text detail. Closing the view or changing account invalidates callbacks. History is fetched on demand and is not cached locally. Recipients can read the current shared report through their notice but cannot retrieve withdrawn historical text.

API additions:
- `GET /api/community/reports/local/<local_id>/`: author's current publication.
- `GET /api/community/reports/<uuid>/revisions/?before=<revision>`: 50 summaries per page.
- `GET /api/community/reports/<uuid>/revisions/<revision>/`: exact snapshot.

Deploy migration `community.0007_shared_report_revision_history` before releasing the updated app/API. Existing reports receive only their current version, explicitly marked as an imported baseline; older revisions cannot be reconstructed. No production migration or deployment was performed. The endpoints expose no history mutation, but this is an application audit trail, not tamper-proof storage against database administrators. Account deletion cascades the associated report history. This does not implement cloud board versions, automatic conflict resolution, or client-association auditing.

Validation: Django schema check found no pending migration changes. All 48 community tests passed on an isolated PostgreSQL database, including author-only access, recipient/stranger denial, read-only endpoints, cursor validation, baseline migration, cascade deletion, atomic rollback and concurrent retry/conflict cases. Unity compiled and passed 46 checks, including two new history UI checks and existing sharing/storage regressions. Native-device layout/network checks and coordinated release remain open.
