# Catalog Upload Backend

The upload backend is implemented in `api_backend/catalogs`.

## Endpoints

- `POST /api/catalogs/upload/` accepts one multipart `file` field.
- `GET /api/catalogs/files/{asset-id}/` streams the persistent asset.
- `GET /api/catalogs/library/` returns the signed-in user's complete usable library.
- `GET /api/catalogs/public/` returns default, public, and marketplace objects.

Supported uploads are GLB models up to 25 MB and PNG/JPG/WebP thumbnails up to
5 MB. The response includes `url`, `sha256`, `kind`, and the asset `id`.

Production stores files in `/var/lib/sandtray/media`, mounted into both Docker
deployment slots as `/app/media`. No OSS key is required for catalog uploads.

Run the backend tests with:

```bash
DJANGO_SETTINGS_MODULE=sandtray_api.test_settings python manage.py test catalogs
```
