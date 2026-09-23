# Recipient-copy review and private report notes

Implemented locally, 2026-09-14. Requires new clients. No reports were sent to real recipients during implementation or checks.

Structured reports now have a separate publication path. The practitioner-notes field is never included in that recipient copy. Public observations, client explanation and next steps can continue to sync automatically to a board's previously configured recipients. AI text is omitted unless explicitly selected and reviewed. Reports whose source is AI, and unstructured/legacy reports, require individual review before publication.

Share & notify verifies recipients, then opens an exact recipient-copy preview. AI inclusion defaults off. Approve & share persists the approved text separately from the internal report with a content fingerprint and review timestamp. The persisted current report must still match the reviewed fingerprint, and only its recorded author can approve. Text edits clear this approval independently of PDF review state. Changes during review, account changes and cancelled previews cannot approve or publish.

The delivery queue builds each outgoing API payload from the safe publication text, never directly from ResultText. Queued older entries are checked again before sending. Reports awaiting review or containing only private text are not sent; the notification status explains the reason. Upload acknowledgment compares the latest publication text to the submitted text so private internal content does not cause repeated uploads.

For unstructured reports the app cannot identify private passages automatically. Their full proposed text must be reviewed explicitly; the preview explains this. Deliberately placing private content in public sections is outside field-based separation. Previously shared server copies are not retroactively recalled. Existing recipient permissions and server API validation remain in force; no backend schema changes were needed for this client behavior.

This change targets friend report delivery. Internal report editing and the existing separately reviewed PDF export continue to use the full local report; export privacy defaults remain separate work. Shared-copy review is a user's content confirmation, not clinical approval or verified professional identity.

44 automated Unity checks passed, including exact reviewed content, private/AI exclusions, stale/author rejection, independent review invalidation, mock outbound API payloads, queue acknowledgment, preview inclusion controls, cancellation and account changes, plus existing save regressions. English/Chinese desktop and phone previews were rendered and inspected; labels cover all eight app languages. A UI assertion was corrected for Unity's empty-string representation of unset serialized strings. No external email, chat, report or push was sent.
