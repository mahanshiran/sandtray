# Received report presentation and PDF — 2026-09-23

The client reader displays a light document surface with board title, recipient name, therapist name, local issue date, revision, report ID and section headings. AI-origin reports remain labeled. The reader can export a PDF through NativeShare on the existing supported platforms. It renders only the report's recipient-visible text, with no access to private practitioner notes or other board data.

Backend adds parsed `sections` and `client_name` to shared-report responses, retaining `text` for compatibility. Parsing recognizes the existing manual report headings in all app languages and common AI headings; unrecognized text is preserved. No schema changes or report migration.

`GET /api/community/reports/received/<uuid>/pdf/` uses the same recipient authorization as the report reader, returns private/no-store PDF bytes, and is write-throttled to bound rendering load. There is no analysis generation or therapist PDF allowance requirement to download an already received report. PDFs include metadata, escaped report text, section styling, page numbers, and a confidentiality footer. Long paragraphs paginate; CJK text uses the same ReportLab CID font support as existing report exports. Board images are not part of the existing shared-report payload.

Validation: Unity runtime compilation passed. Local and candidate backend runs completed 42 tests (37 passed, 5 PostgreSQL-specific skips), covering direct delivery, recipient authorization, PDF bytes, long-report pagination, and section parsing. A sample bilingual PDF was rendered with PDFium and visually inspected. No real client report was sent during verification.

Release: b5d4359f699e2fe24b2f01c7147e2edc7853e5fe, based on 0423e3d74881e5d309d1b206cd4272f47f4addaf. Changes are community report views, URLs, PDF renderer, and PDF tests. The Unity client needs Play mode restart or a new build.

Deployment completed in blue; public readiness returned ready and unauthenticated PDF access returned 401. Existing notification workers restarted successfully through the deployment helper.
