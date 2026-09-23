# Therapist profiles

Backend deployed on 2026-09-13 with migration `accounts.0007_therapist_profile`.
An updated Android 1.7 build 3 test APK is available; existing installations need
the updated client to show the editor. No store release was published. See
[deployment and validation](therapist-profile-deployment.md).

## Scope and standards

Every therapist account (`user_type=psychologist`, retained for API compatibility)
has a separate one-to-one `therapist_profile` record. All user-entered fields are
optional. The ordinary personal `Profile` and account avatar remain separate.
The editor is under Settings → Therapist profile and supports a private photo,
professional details, and multiple qualifications from different jurisdictions.

The structure is informed by the published **HL7 FHIR R5 Practitioner** and
**PractitionerRole** concepts. It is an application-specific profile, not a FHIR
resource or a certified FHIR implementation. There is no single profile schema
that establishes legal authorization to practise in every country. Country and
region fields support international entries without requiring a national ID,
US NPI, UK register, particular degree, or particular professional title.

| Profile information | Basis / purpose |
| --- | --- |
| Professional display name, title, photograph, professional contacts | Practitioner identity, photo and telecom concepts |
| Repeatable qualification title, institution, issuing body, registration number, country/region and validity dates | Practitioner qualification, identifier, issuer and period concepts |
| Practice name, location, areas of practice, languages and session formats | PractitionerRole role, specialty, location, communication and service concepts |
| Bio, therapeutic approaches, age groups, accessibility and fee information | Product-specific optional practice information; these are not claims of FHIR conformance |

Sources reviewed:
- [FHIR R5 Practitioner](https://hl7.org/fhir/R5/practitioner.html)
- [FHIR R5 PractitionerRole](https://hl7.org/fhir/R5/practitionerrole.html)
- [BACP Register](https://www.bacp.co.uk/about-us/protecting-the-public/bacp-register): an example of a jurisdiction/professional-body register, not a global verification authority.

Professional titles and registrations are **self-reported**. `credential_status`
is server-generated as `self_reported` and cannot be changed by an owner. No
verified badge, right-to-practise decision, public therapist directory, insurance
billing qualification, clinical validation, or regulatory certification is implied.
DOB, home address, identity documents and client/patient data are not collected
in this profile.

## Fields and API

Authenticated owner endpoints:
- `GET /api/auth/therapist-profile/`
- `PATCH /api/auth/therapist-profile/`
- `GET /api/auth/therapist-profile/image/`

No user ID parameter is accepted for selecting someone else's profile. Normal
accounts receive 403, anonymous requests receive 401. Therapist fields are not
included in the general UserSerializer used by linked-client endpoints.

Optional text: `display_name`, `professional_title`, `bio`, `practice_name`,
`professional_email`, `professional_phone`, `website`, `country`, `region`, `city`,
`timezone`, `accessibility`, `fees_information`.

Optional arrays: `languages`, `specialties`, `approaches`, `age_groups`,
`session_formats` (`in_person`, `online`, `phone`), and `qualifications` (up to 20).
Qualification fields: `title`, `institution`, `registration_number`, `issuing_body`,
`country`, `region`, `valid_from`, `valid_until`. Dates may be omitted, empty or null;
when provided, use YYYY-MM-DD, and end must not precede start. Country fields use
two-letter code format; they are not a jurisdiction/registration verification
service. Time zones use IANA identifiers. Languages and clinical categories are
free text, not terminology-bound FHIR codes.

`therapistProfileImage` is the authenticated owner-image URL or an empty string.
It is distinct from `profile.avatar_url`. Both profile and image responses are
private/no-store. Images are stored in the profile database record, not in a public
media directory. The API accepts JPEG/PNG up to 5 MB and 16 megapixels, applies
orientation, removes metadata by re-encoding, and downsizes to at most 1024×1024.
The app's existing native/desktop picker crops a 256×256 avatar.

A PATCH can atomically save text and image intent:
- `image_action: keep` (default): retain current photo.
- `image_action: replace`, `image_data: <base64>`: validate and replace photo.
- `image_action: remove`: remove photo.

Any validation failure leaves the stored profile and image unchanged. Normal
profile saves do not change personal avatars. Network responses and picker results
are ignored after dialog/account changes. The form is disabled while saving.
Closing is disabled while saving. Closing a dirty draft asks whether to discard
changes or keep editing. Failed loads offer Retry. Tab/Shift+Tab move between
controls and selected inputs scroll into view. Labels and fixed messages support
all eight app languages; server field-validation messages may remain English.

## Lifecycle and migration

A User post-save receiver creates the record when an account becomes a therapist.
The migration backfills existing therapist accounts, copying legacy bio, clinic
name, two-letter country, time zone and certification number where available.
It preserves all original personal-profile data and does not copy the personal
avatar. A professional registration copied from legacy data remains self-reported.
GET repairs a missing therapist profile when necessary. Direct SQL/bulk role changes
bypass Django signals; callers must use normal account saving or arrange backfill.

Switching to a normal account hides and denies access to the professional editor
but preserves its record for switching back. Account deletion cascades to it.
The deployed owner endpoints remain owner-only. The local live-session feature
adds a separate relay-authorized read-only projection; see
[session profile sharing](SESSION_PROFILES.md). It excludes account email, client
records, notes and subscription data. There is no public directory.

## Validation and release gates

Backend tests cover lifecycle, role restrictions, personal-profile separation,
owner isolation, protected images, invalid fields, multiple qualifications, optional
dates, image removal, atomic validation failure and repeatable migration backfill.
Unity checks cover DTO round-trip, optional inputs, adding qualification fields,
minimum input height, avatar layout, dirty-close confirmation and all eight
translation columns. Three Unity profile checks passed. Phone-sized (390×844)
and desktop (1200×900) previews were generated in eight languages; English,
German, French, Chinese and Japanese samples were inspected. Local validation:
39 backend account/social/profile tests and a clean migration consistency check.
The production candidate passed 36 tests, and the live HTTPS workflow passed
owner isolation, image upload/removal, invalid-save preservation, optional
international credentials, role switching and migration backfill checks.

Pending: physical-device editing/photo selection and mobile-keyboard QA,
assistive-technology QA, native-speaker translation review, distribution signing
and store publication, and updated iOS/desktop builds. Country-specific
verification/retention/publication rules require separate work if those features
are added. Concurrent saves use last-write-wins; there is no revision history.
