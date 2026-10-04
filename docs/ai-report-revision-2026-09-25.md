# AI report revision — 2026-09-25

## Deployment

- Backend release: `3cd86388e0bd795ce7a409db576a1c7af8f4c0dd`, active in blue.
- Previous release: `2de1ca7f46aaaec561fa1b9cf9157e0ee25dc635`.
- Public `/ready/` returned `{"status":"ready"}` after deployment.
- Anonymous POST to `/api/analysis/reflect/` returned HTTP 401.
- No database migrations were required. No client reports were generated or sent during verification.
- Existing HSTS deployment warning remains unchanged.

## Behavior

New reports omit references and discuss possible scene themes grounded in visible relationships, with uncertainty and a neutral alternative. They do not establish the creator's personality or emotional state. Empty and single-object trays without distinct terrain receive a brief insufficient-detail response. Ordinary randomized sand is not treated as deliberate sculpting.

Unity changes strip legacy reference sections for display and PDF export without modifying saved originals. New terrain metadata distinguishes features outside the default generator's envelope; it does not prove intent or detect every edit. These client changes require an updated app build. Backend generation and PDF cleanup are live independently.

## Verification

- Candidate backend: 51 tests, 50 passed and one skipped (PostgreSQL-specific test under SQLite).
- Unity: five focused tests passed; script compilation succeeded in an isolated project copy.
- Synthetic provider checks covered English text, English vision, Chinese text, and a single hamburger. Accepted outputs and evaluation limitations are recorded in `AIknowledge/evaluations/2026-09-25-report-revision.json`.
- Validation is heuristic and does not guarantee semantic or clinical correctness. Device visual QA remains outstanding.

Rollback uses the existing `/usr/local/sbin/sandtray-rollback` deployment helper.
