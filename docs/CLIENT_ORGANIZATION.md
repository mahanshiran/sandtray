# Client organization

The Unity home sidebar now has a **Clients** page. This version uses the existing
on-device table/report storage; it does not sync client records or tables to the API.

## Workflow

1. Choose **Clients → New client**. A name or alias is required; client code,
   birth date, phone, email and notes are optional. Clients do not need app accounts.
2. Select a client and choose **New table**. Choose its name and size as usual.
   Its client assignment is included in the first save and subsequent saves.
3. Open **Unassigned tables** or **My Boards**, then **Move**, to assign an existing
   table. The home table-card overflow menu also has **Move to client**.
4. The client's **Tables** tab opens their saved tables; **Reports** collects saved
   reports from those tables and opens each report directly.
5. Edit the client's profile without changing their stable ID. Archive/restore
   profiles without deleting their tables or reports. Use **Show / hide archived**
   to find archived clients. Archived clients cannot receive new tables through
   this page until restored, but their existing tables and reports remain accessible.

Moving a table changes its assignment, not its model data or report contents.
Choosing **Unassigned tables** in the move picker removes an assignment. Existing
saves without `ClientId` remain unassigned. New table names are made unique, and
renaming to an existing table name is rejected to avoid overwriting another record.

## Storage and scope

- Profiles: `Application.persistentDataPath/Clients/clients.json`.
- Tables: existing `Sessions/*.json` files, with an optional `ClientId` field.
- Reports remain embedded in their table; the client view aggregates them.
- The new profile writes and table save/move/rename writes use a temporary file
  followed by atomic replacement, retaining the previous destination as `.bak`.
- This is device-local organization, not a shared clinic record system or an
  account-isolated/cloud-synchronized client database. Copying only a table JSON
  does not copy its associated client profile. Backups must include both folders.
- No Django migrations, production deployment, or API changes are needed for
  this local version. Future cloud synchronization needs a client-profile API
  that works without a registered client user and integration with board sync.

## Verification

`Assets/Editor/Tests/ClientOrganizationTests.cs` covers client identity across edits
and archival, validation, corrupt-file protection, moving/unassigning tables,
report/notes preservation, renaming/collision protection, initial assigned saves,
subsequent saves, and legacy unassigned tables.

Verification on 2026-09-09: Unity compilation passed; all 7 client-organization
tests passed. The client page, profile form and report list were rendered in
English and Chinese, and the move-picker button was exercised against fixture data.
The wider EditMode suite passed 35/37 tests. `HeightmapRegion_RoundTrip` and
`Recorder_WritesParsableFile_EndToEnd` also failed when run on their own; their
test files and networking/recording implementations were not changed in this task.

## Account linking — September 15, 2026

The client editor supports manual entry, an exact six-letter account ID search,
and selection from accepted friends. The picker is nested, so the current form
survives selection and cancellation. Selecting an account fills an empty name;
it preserves existing names/aliases and all therapist-entered fields. The public
account lookup exposes no email, phone or birth date, so those stay manual.

Each client keeps its existing immutable `Id`, separate from the optional
therapist-assigned `Reference` and the linked account. The editor displays the
client ID; the client list search accepts it and the linked public account ID.
Manual clients can be linked later through Edit profile. Save commits the link;
Cancel discards the draft. The link stores the backend, numeric user identity,
community identity code, public six-letter ID, display-name snapshot and link time.
Duplicate account links are rejected within the current client store, including
archived records. Saved links cannot be replaced in this editor. A future account
correction/unlink workflow needs explicit review and history before it is enabled.

Linking is local metadata. It does not create a friendship, notify an account,
share clinical data, or authorize report delivery. Future delivery must resolve
the saved identity and enforce recipient permissions on the server; a local link
must never be treated as recipient consent or server authorization. Existing
board/report associations continue to use the unchanged local client ID.

Validation: Unity compilation and 42 focused checks passed in the isolated test
project (client records, account picker, client/session workflows and friends).
Desktop and phone client-form renders were inspected in English and Chinese.
The broader initial run also found a separate `ClientNavigationTests` failure:
`SwitchingToNormalLeavesClients_AndDirectNavigationIsGuarded` reaches a null
navigation object in `RefreshHomeNavigationLevel`; it remains unresolved here.
No production API calls, build deployment, or physical-device tests were performed.

Client-list rows and the profile header now prefer the saved local photo and
fall back to the linked account's cached public photo for older records without
one. Account resolution uses the saved user ID and account code, never the
client's name. Missing photos and unassigned tables retain initials/placeholders.
Explicit photo removal suppresses remote fallback. Viewing a remote fallback
does not write or replace the local clinical record. Remote photos are
centre-cropped, and callbacks reject closed views and account changes.
