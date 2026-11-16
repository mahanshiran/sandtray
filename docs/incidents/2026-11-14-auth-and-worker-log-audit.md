# Authentication and worker log audit

Times below use Asia/Shanghai (UTC+8).

## Repeated 401s

On November 14 from 11:00 through approximately 11:44, one remote Unity client
at 219.117.198.118 generated 263 access/me, 88 heartbeat, 67 notification,
and 3 local-lease 401 responses. No login or refresh requests from that address
appeared in this window. Other addresses received 200 responses concurrently.
The failing address is distinct from this workstation's observed API address.

The 183-byte response matches SimpleJWT's token_not_valid response. An expired
legacy access token stored in this workstation's Unity preferences reproduced
that exact status and body length. This reproduces the failure class but does
not prove the remote client's token has the same expiry or account. The current
Keychain access credential returned 200. No token values were recorded.

The observed problem is a remote client repeatedly polling with rejected
credentials without attempting refresh/login. Its installed build and exact
token rejection reason cannot be recovered from access logs. Current Unity
source already has refresh handling in the shared Get helper, while FriendsClient
has its own request helper that reports 401 without refreshing. This does not
establish which source revision the remote client runs.

## Worker timeouts

Both critical timeouts followed HUP reloads:

* November 13 23:11:33 reload; new worker 50 booted immediately; old worker 28
  began exiting at 23:12:04 and timed out at 23:13:34.
* November 14 00:28:39 reload; new worker 112 booted immediately; old worker 50
  began exiting at 00:29:09 and timed out at 00:30:39.

Both timeouts were exactly 120 seconds after reload. Tracebacks show Python
shutdown waiting in concurrent.futures.thread._python_exit -> thread.join.
Replacement workers returned successful API requests before the old workers
were killed. These entries describe old request threads failing to drain during
reload, not evidence that the replacement worker stopped serving.

Historical traces do not identify the individual unfinished queries. The
separate schedule-list incident documents database ClientWrite stalls and its
applied summary-query hotfix. During this audit PostgreSQL showed no active
ClientWrite stall, API memory was about 88 MiB, and no OOM condition was found.
Full-detail transfer throughput remains outside what these logs establish.

No production code, tokens, database records, or server settings were changed.

## Client recovery implementation

Added CanUseCloudSession to distinguish usable cloud credentials from the local
account identity. A definitive refresh rejection or a repeated authenticated
401 marks the credentials rejected, emits one account-status notification, and
opens the existing login flow. Shared GET/POST helpers stop sending rejected
credentials while allowing anonymous login and replacement credential checks.
Friends and push registration now share token refresh with account requests;
friends, access, notification/report delivery, and push polling stop while
sign-in is required. Local board identity and queued report deliveries remain.
Transient refresh failures keep the session retryable. Full C# compilation
passed with zero errors.
