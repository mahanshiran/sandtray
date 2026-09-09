# Catalog Upload Setup

No manual Unity scene wiring or OSS credentials are required.

The main menu builds the catalog-management and upload panels at runtime. The
backend deployment mounts `/var/lib/sandtray/media` automatically, and Django
migration `catalogs.0004_catalogasset` creates the upload records table.

For a new server, run the normal Sandtray bootstrap and deployment workflow.
For an existing server, pushing the backend to `main` runs tests, applies the
migration, and deploys the updated API automatically.

Recommended smoke test:

1. Sign in to the app.
2. Open **Objects** and create a private catalog.
3. Upload a small GLB and thumbnail.
4. Open the object browser and place it.
5. Host a room and join as Patient from a second device.
6. Confirm that the custom object appears and can be placed on both devices.
