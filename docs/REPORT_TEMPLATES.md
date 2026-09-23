# Reusable report templates

Implemented locally on 2026-09-13. Requires updated client builds; no backend migration.

## Workflow

Open Reports → New report. The Template picker offers Standard sections and the signed-in account's saved templates. Manage templates opens a separate overlay without discarding report text.

Create a named template by including or excluding the five existing report section types: observed events, client explanation, practitioner notes, next steps, and optional AI reflection. New templates initially include the first four. Save and Cancel remain in a fixed footer above the safe-area edge. Templates can be edited, duplicated under a new name, and deleted after confirmation. Unsaved template changes require confirmation before leaving the editor.

Selecting another template prompts before discarding unsaved report text. New reports always start with empty content. Templates contain only a name and section keys; there is no action that copies report answers, client identifiers, or private notes into a template.

Saved reports retain their template name and section selection as independent snapshots. Editing or deleting a template does not rewrite an existing report or change the layout of an open draft. Existing unstructured reports and reports without template metadata retain their prior editor behavior. Populated report fields remain visible even if layout metadata omits them.

## Storage and limits

Templates live under `Application.persistentDataPath/ReportTemplates/<account-id>/report-templates.json`. Files record their owning account. The UI checks account and dialog identity before actions; stale callbacks cannot write templates for a replacement account. This is local app isolation, not encryption or protection against direct filesystem access.

Writes use the existing atomic local-record writer and backup rotation. Valid backups recover corrupt primary files while retaining damaged bytes; unreadable copies never become an empty overwrite. Revisions reject stale edits/deletes. Names are limited to 80 characters, unique per account ignoring case, with at least one section and at most 100 templates per account.

Cloud synchronization, arbitrary custom section headings, reusable session templates, and prewritten report content are outside this implementation.

## Validation

47 isolated Unity checks cover template persistence/account separation, corrupt-copy recovery, failed-write retry, stale edits/deletes, independent duplicates and report-layout snapshots, plus existing report/save behavior. UI checks cover create/select/save, unsaved-draft preservation, edit/duplicate/delete/cancel, and blocked writes after account changes. New labels have all eight app-language translations.

English and Chinese desktop and phone previews were rendered and visually checked for the picker, manager, and template editor. Native keyboard, rotation and screen-reader interaction require device validation. No app build or deployment was performed.
