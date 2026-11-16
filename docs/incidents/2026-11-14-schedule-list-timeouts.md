# Schedule lists exhausted API request threads

Production VIP grants were valid. The API's single two-thread Gunicorn worker
stopped answering health and entitlement requests while both threads fetched
schedule records. PostgreSQL showed the corresponding reads waiting in
ClientWrite for minutes. Stored schedule submission bodies totalled about 17 MB;
an independent 256 KB database transfer took about eight seconds.

Schedule and therapist report list queries selected full autosave, final table,
and report bodies even though cards only needed a submission-exists flag.

The fix computes that flag with a SQL CASE expression and defers test_submission,
final_submission, and therapist_report on those lists. The due-test settlement
scan also defers the bodies. Detail routes retain their existing payloads.

Validation: 43 targeted schedule/report tests completed (2 skipped), including
large-body cards, zero additional queries during card serialization, and
missing/autosave-only submission flags. Production account-scoped list calls
returned HTTP 200 in 0.7–1.6 seconds after applying the change. Public HTTPS
/ready/ returned ready again.

An isolated three-file hotfix was applied to the running sandtray-api-blue
container from its exact production source, followed by a Gunicorn HUP reload.
Original files are saved at /app/.summary-hotfix-backup inside that container;
local original/patched copies are in /tmp/sandtray-summary-hotfix. The same
changes are in api_backend/accounts/schedules.py, test_report_work.py, and
scheduled_tests.py for inclusion in the next immutable release. Recreating the
container from the previous image would lose the runtime hotfix.

This removes large payloads from polling/list queries. The underlying database
transfer throughput and full-table detail download performance remain separate
investigation items; neither was changed by this hotfix.
