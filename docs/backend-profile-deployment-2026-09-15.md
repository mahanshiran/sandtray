# Backend deployment — 2026-09-15

Deployed the current backend source, including private personal profiles and catalog object moves, to https://api.sandtraypro.com using the existing blue/green helper.

- Active slot: green.
- Release: `31b648f3343310c066dff5c4d57891b46b03a796` (source archive hash, not a Git commit).
- Previous API image: `sandtray-api:23fd0bed894abb84809fbf8b287d672d0004825c`.
- Database backup: `/var/lib/sandtray/backups/before-profile-20260915.dump` (root-only, 9,758,987 bytes).
- Applied accounts migrations 0008–0016, analysis 0003, boards 0002–0003, and community 0007.
- No production environment settings were changed. New access enforcement flags default to disabled.
- Existing push worker was left running with its existing image.

Validation: isolated backend suite completed with 210 passed and 15 PostgreSQL-specific tests skipped; migration consistency check reported no changes. Public HTTPS readiness returned success and anonymous personal-profile requests returned 401. Live profile save, read, and invalid-age validation passed inside a transaction that rolled back all temporary data.

The deployment check still reports the four existing Django security-setting warnings for HSTS, SSL redirect, secure session cookies, and secure CSRF cookies.

The previous API slot remains available through `sudo sandtray-rollback`. Rollback does not reverse the additive database migrations.

Unity source fixes address the reported Action/UnityAction mismatch, inherited member name collisions, and unused-field warnings. No Unity build or Editor compilation was run.
