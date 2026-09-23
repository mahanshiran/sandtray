# Client profile photos and date picker

## Use

Clients → New client / Edit profile → Choose photo. Preview uses a 256×256 centre crop;
Remove restores initials. Photo and birthday edits are committed only by Save client.
The birthday button opens a calendar; tap the month/year to jump, then select a day
and press Done. Clear removes the optional birthday. Future dates are disabled.

The edit form scrolls and switches to one column in portrait/narrow layouts.
English and Chinese labels are included. Photos appear in the client list and header.

## Platforms

- iOS/Android: Native Gallery 1.9.4, pinned through the existing OpenUPM registry.
  Uses the native Photos/gallery selector and oriented image loading. iOS usage text
  is configured in ProjectSettings/NativeGallery.json; plugin build setup is enabled.
- Windows/macOS/Linux: existing Simple File Browser (PNG/JPEG); Unity Editor uses
  its native file selection dialog.
- WebGL: file input + browser image decoding/canvas reduction. Browser-supported
  formats only; unsupported images show an error. No camera capture is requested.
- Input size limit: 20 MB. Original library files are never modified.

Native Gallery API reference: https://github.com/yasirkula/UnityNativeGallery

## Storage and deployment

No backend API or migration change. Profiles remain device-local, not cloud-synced.
clients.json has an optional PhotoFile field; images live in Clients/Photos under
Application.persistentDataPath. Existing records need no migration. Saved photos
are immutable; previous photo files are retained so clients.json.bak remains usable.
Closing/cancelling a draft does not write a photo. Invalid forms do not write photos.
Missing/unreadable images fall back to initials. UI-owned textures are released.

WebGL storage is browser-local: private browsing, storage eviction, or clearing site
data can remove it, just as with the existing local client/table records.

Rebuild the Unity app after package resolution. No server restart is needed.

## Verification

Unity 2022.3 editor compilation; nine client storage/workflow tests; EN/ZH desktop
and portrait render inspection; calendar navigation, leap years, future days,
selection, Clear, Cancel; photo resize and persistence checks. Picker C# branches
compiled separately for Android, iOS, WebGL and desktop against installed APIs.

Device integration remains to test on physical iOS/Android, standalone desktop
players and a browser WebGL build: open/cancel/reopen picker, deny access, rotated
camera images, unsupported/oversized files, save/relaunch, and on-screen keyboards.
Only the Mac build module is installed here; these are not claimed as device tests.
