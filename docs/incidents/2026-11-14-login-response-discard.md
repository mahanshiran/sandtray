# Successful login response discarded by Unity

Production logs at 03:32:57 and 03:34:36 UTC on November 14 show Unity login
returning 200, followed by /auth/me/ returning 200. Repeated access/me requests
from Unity also returned 401. No token refresh calls appeared in the inspected
18-hour container log window. These observations do not establish the cause of
every 401, nor identify the account in each request.

Login deliberately sets UserId to zero before fetching /auth/me/. The shared
Get helper selected CaptureAccountGuard for every authenticated request; that
guard requires a positive captured user ID. Consequently it discarded the
successful identity response without invoking either callback, leaving login
pending. Post had the same provisional-identity guard inconsistency.

Both helpers now use the credential guard while UserId is zero, retaining
the account guard for resolved identities and the epoch check in both cases.
Static checks verified these branches. Unity compilation and an interactive
login remain unverified; the application must load the changed script.

Public production /ready/ returned 200 in 0.334 seconds during inspection.
The container still runs one worker with two threads. Earlier worker timeouts
occurred at November 13 15:13 and 16:30 UTC. See the schedule-list incident for
the separately applied large-query hotfix. No production configuration or
credentials were changed during this investigation.
