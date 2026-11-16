# Scheduled test completion notifications

A submitted test or an expired test with a saved table creates one `report_pending`
AppointmentNotice for the owning therapist in the same transaction as completion.
The existing appointment/recipient/event uniqueness constraint deduplicates final
upload retries and the timeout/final-upload race. No saved table means no report
work notification. Existing completed tests are not backfilled.

The schedule worker delivers the therapist email and queues device pushes using
Reports preferences. Both use the appointment ID and existing authenticated
schedule deep link; the Unity link/tap handler opens completed test tables in the
report workspace. In-app notifications have a report-pending label and the same
route. Recipient authorization remains enforced by the schedule/detail endpoints.
A report completed before dispatch suppresses the obsolete delivery. Neither
email nor push includes report results. Large snapshot fields are excluded from
notification list and dispatch reads.

Validation: 55 schedule/report/push tests successful (2 existing skips); Unity C#
build has no errors. Delivery tests use locmem email and inspect queued pushes;
no synthetic production emails or pushes were sent. Production API, schedule
worker, and push worker received the four Python updates with `.pre-pending-report`
backups. These are runtime patches; rebuilding containers must use updated source.
