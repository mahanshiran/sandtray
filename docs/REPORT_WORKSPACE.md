# Session report workspace — 2026-09-13

Implemented locally. Requires a new Unity app build; no backend migration is
needed. Existing cloud analysis snapshots remain immutable and owner-filtered.

## User workflow

The sidebar Reports button opens the current saved board's report workspace.
Desktop shows AI and manual history on the left, newest first, with source,
creation time and recorded author. Phones switch between History and the editor.
New reports use optional sections for observed events, the client's explanation,
practitioner notes, agreed next steps and clearly labelled AI-assisted reflection.
New reports can use named reusable section layouts; see [report templates](REPORT_TEMPLATES.md). Only populated sections enter the saved text. Existing unstructured reports retain
their exact text and source instead of being automatically reinterpreted.

The author can edit a saved manual or AI report. Saves preserve the previous text,
structured sections and editing account ID, clear prior review confirmation, and
invalidate the previous cloud PDF link. Earlier versions are read-only. The
existing review gate is reused for PDF export. History refreshes after saving.
Close, report selection and other workspace navigation guard unsaved drafts.
The screen states that reports are stored on the current device.

## Authorship and limits

Creation requires a signed-in account. SessionManager stamps AuthorUserId and
AuthorName from the current account, overriding caller-supplied attribution.
Editing checks the persisted report's author before even accepting a no-op; all
editor entry points also check ownership and account/context changes. Missing
reports, stale text and failed atomic writes do not silently become successful
saves. AI local-save failures are surfaced; Edit can retry local persistence.

Legacy reports have no reliable author identifier. They remain readable and
exportable, but cannot be edited or automatically claimed by the current user.
New reports distinguish manual and AI sources; unknown old sources remain labelled
legacy. Live session host/editing privileges do not grant report authorship.

These are local application controls, not tamper-proof server authorization or
an authenticated clinical audit trail. A person with filesystem access can alter
local JSON. Report sync, account-isolated encrypted storage and server-controlled
revision history remain separate roadmap work. Cloud copies are immutable
snapshots: local edits require a new explicit upload/export, not an in-place
cloud-record update. No claim of worldwide clinical or regulatory certification.

## Basis

The structure follows the product checklist's separation of observation, client
voice, practitioner notes and optional AI reflection. It is an application
report format, not a mandatory clinical template. HCPC guidance emphasizes clear,
accurate, secure records and notes that formats depend on practice and profession:
https://www.hcpc-uk.org/standards/meeting-our-standards/record-keeping/our-expectations-for-your-record-keeping/
https://www.hcpc-uk.org/standards/meeting-our-standards/record-keeping/faqs-on-record-keeping/

## Validation

53 isolated Unity save/client/report checks passed. Checks cover authored creation, rejection of non-author/no-op/legacy edits,
previous-version attribution, structured manual saves and mixed-source history,
plus existing atomic persistence, stale edits and export invalidation regressions.
English and Chinese phone/desktop previews inspected. All eight app languages have
workspace strings. Native keyboard, rotation, actual failed-device writes and
end-to-end PDF sharing remain device QA; no updated binary was built in this task.
