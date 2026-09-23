# Optional therapist certificates

Therapists can upload 0–10 public certificates from Edit therapist profile. Certificates are independent of VIP tiers and never required to save the profile. Each upload includes a name and optional issuer, issue date and expiry date. Add, view, replace file and remove are available. Certificate changes save immediately, independently of the profile Save button.

Other users can open documents from therapist profiles reached through friends or live-session avatars. Documents are self-reported, not verified. The editor explicitly tells therapists that uploads are public; direct document links can be opened without login. Images open in the browser; PDFs download/open through the device's PDF viewer.

Limits: PDF/JPG/PNG, 10 MB per upload; images up to 40 megapixels and normalized to JPEG (maximum 4096 pixels on either edge, metadata stripped). PDFs must parse, be unencrypted and have 1–200 pages. Raw PDF originals are preserved. No authenticity verification or automatic verification badge is provided.

## API and persistence

- GET/POST `/api/auth/therapist-certificates/`: authenticated therapist owner listing/upload.
- GET `/api/auth/therapists/{user_id}/certificates/`: authenticated users may list a therapist's documents.
- PATCH/DELETE `/api/auth/therapist-certificates/{uuid}/`: owner-only replacement/metadata change/removal.
- GET `/api/auth/therapist-certificates/{uuid}/file/`: public non-indexed document with no-store caching, nosniff and sandbox headers. PDF responses use attachment disposition.

Migration 0022 stores documents and metadata in the existing PostgreSQL database. Its backup and account-deletion cascade cover certificates. Upload count is checked while holding the owner row lock, so concurrent uploads cannot exceed ten. Normal-role profiles do not expose documents; changing back to therapist restores access to retained uploads. Removing a certificate invalidates its server URL, though copies previously downloaded by users cannot be recalled.

Validation: 172 backend tests run, 8 existing skips; Unity editor compilation successful. Mobile/desktop upload and device viewer behavior still require a device smoke test. Browser/WebGL uploading is currently unavailable (viewing remains supported).
