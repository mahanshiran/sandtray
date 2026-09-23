# Account type in Settings

Home → Settings → Account type lets a signed-in user switch among Personal
(`normal`, the existing API value), Therapist (`psychologist`) and Organization
(`organization`). The legacy internal value `normal` is never shown to users.

The client sends only `user_type` to `PATCH /api/auth/me/`. It applies and caches
the returned type only after a successful response for the same account. It has
a 30-second timeout, retries once after refreshing an expired access token, and
disables all options during saving. Failed saves leave the previous selection.
Guests are prompted to sign in; admin accounts cannot switch through this control.

This is an account preference, not credential verification or a subscription
upgrade. Switching does not delete boards, client records or reports.

No API behavior change or database migration is required. Backend changes for
this feature consist only of regression tests in `AccountTypeSettingsTests`.
The Unity application must be rebuilt to ship the new Settings controls.

API verification:

```sh
python manage.py test accounts.tests.AccountTypeSettingsTests --settings=sandtray_api.test_settings --noinput
```
