# Record search and archiving

Implemented locally, 2026-09-14. Requires new client builds; no backend migration.

## Entry points and filters

My Boards → Search records opens the search workspace. Client history → Filters opens it scoped to that client; report lists also offer Search records.

Search switches between device-local boards, the signed-in author's reports, and device-local clients (therapist accounts). It searches names, client references/contact details/notes, board notes, and report text. Board matches include active reports by the current author, never other authors' report text. Report rows show date/time, source and a short text preview. Results sort newest first with 30 items per page.

Filters combine client, inclusive local date range, manual/AI source for reports, and Active/Archived/All status. Dates use YYYY-MM-DD; invalid or reversed ranges stay in the filter editor. Report dates are creation dates; board/client dates are their recorded modified dates. Unknown dates are excluded when a date bound is present. Cancel leaves the applied filters unchanged.

## Archive and restore

The search workspace uses consistent Archive/Restore actions for all three record types. New Archived flags on boards/reports default to false for existing files. Normal scene saves preserve archive state. Active board lists and report histories exclude archived entries; Search → Filters → Archived or All makes them available for restoration. Client archiving uses the existing client record flag.

Archive writes preserve board/report content, associations, revision history and cloud-export identifiers. Archiving a board does not individually archive its reports. Report archive mutations reload the persisted report and require its recorded author. Archiving is local organization, not deletion or withdrawal of previously shared copies.

## Access boundary

Existing client and board files remain device-local without account ownership. This change neither migrates nor assigns those records. Full account-isolated storage remains separate work. The UI states the local-data boundary; expanded report-content search includes only the current account's authored reports. Legacy reports without authors and other authors' reports are excluded. Account/token/role changes invalidate the open search view and its actions.

## Verification

46 isolated Unity checks passed, including combined filters, author exclusions, inclusive date boundaries, invalid ranges, archive persistence across normal saves, restoration, report author enforcement, search UI, filter cancellation, stale account callbacks and existing save regressions. English/Chinese desktop and phone previews were rendered and visually inspected. All new labels have eight-language translations.

Native keyboard, touch scrolling, rotation and accessibility require device validation. No updated application binary was built or published.
