# Profile image caching

Profile photos use a shared, account-scoped memory cache across the home header,
friends, account popover and therapist profile editor. Reopening a view reuses
cached image bytes. Concurrent requests for the same source share one download;
closing a view does not cancel a download needed by another view.

- Maximum 64 entries and 16 MiB of retained compressed image bytes, with least
  recently used eviction. Each view owns and releases its decoded texture.
- Freshness is capped at five minutes; Cache-Control max-age and Age can shorten
  it. Responses marked no-store or no-cache are not reused. This is an application
  cache, not a full HTTP validator cache; expired images are fetched again.
- Failed requests have a 30-second backoff. Abandoned pending requests can retry
  after 45 seconds. Pending requests and waiter lists are bounded.
- Keys distinguish backend, local account epoch, user identity and sign-in state.
  Logout clears the cache. Successful therapist profile edits invalidate photos;
  changed public image URLs naturally use a new entry.
- Old responses cannot repopulate an invalidated cache. Avatar callbacks also
  reject disabled/destroyed views and changed identities.
- No images are persisted to disk by this cache. Restarting the app starts fresh.

Validation: Unity compilation and six focused cache/avatar checks passed,
covering concurrent reuse, expiry, account isolation, invalidation, late responses,
failure backoff, abandoned requests, memory eviction and server cache directives.
