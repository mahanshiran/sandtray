# Rich report editor

Reports use **Tiptap 3.33.1 / ProseMirror**, not a custom TMP markup editor.
The editor and all scripts are bundled offline in
`Assets/StreamingAssets/ReportEditor/index.html`. There are no CDN requests,
remote fonts, editor accounts, or external image URLs. Dependency versions and
licenses are retained in `tools/report-editor/package-lock.json` and the shipped
`THIRD_PARTY_NOTICES.txt`.

The existing right pane hosts the editor through the vendored GREE unity-webview
package. Its native WebKit/WebView2/Android WebView renders actual HTML. macOS,
iOS, Android, and Windows plugins are included. Windows installations require
WebView2 Runtime. An unsupported or failed browser fails visibly; it never
silently replaces the editor with a plain text input.

## Data and workflow

`ReportSections.RichTextVersion == 1` identifies the portable HTML document.
`RichHtml` contains the recipient-visible report; `PrivateHtml` contains the
separate, collapsed private-notes document. Plain-text fields support search and
existing report workflows. HTML, embedded images, and text travel together in
local tables, cloud snapshots, and scheduled-test completion. Formatting-only
edits invalidate publication/PDF reviews and stale editor saves are rejected.

New reports start with freely editable headings. No batch migration of old
reports or saved data deletion is performed. Existing content can be opened as
plain content; past layout/template fidelity is not guaranteed or required.

Drafts are written atomically under the active account's `ReportDrafts` folder.
Recovery checks the saved report fingerprint. Save/discard removes its draft and
backup. A live snapshot acknowledgement is required before a save completes.
Changing account, closing a pane, or opening recipient review hides the native
view and clears keyboard focus; editor shortcuts never manipulate the tray.

Images: PNG/JPEG/WebP, at most 2 MB and 16 megapixels per image. Documents are
limited to 6 MB, including images. Server request and scheduled-report limits
accept these documents. DOMPurify cleans pasted HTML and the server independently
sanitizes with Bleach before rendering. PDF rendering never fetches URLs.

## PDF and recipient delivery

Analysis PDF exports pass rich HTML in `parsed_result.rich_html`. Profile-directed
email and shared-report publication store the public HTML only. ReportLab renders
headings, formatting, colors, supported font families, lists, tables and embedded
raster images to PDF, including Chinese text. Private notes are excluded from
sharing and included in a local export only when explicitly selected.

Backend changes are in the separate `api_backend` repository and require its
normal deployment before rich PDF/email delivery is live. Migration
`community.0012_rich_report_documents` adds public HTML fields. No deployment is
performed by the editor build.

## Development

```
cd tools/report-editor
npm ci
npm run build
npm test
node browser-check.mjs
```

The browser check accepts `CHROME_PATH` to use a local Chromium executable.
Backend checks: `analysis.test_rich_text`, report-recipient delivery, received
report PDFs and therapist report-work tests. Native WKWebView startup and bridge
round-trip were verified in an isolated Unity 2022.3 project. Android/iOS/Windows
player builds still require device/platform validation before release.

## Validation for this change

- C# editor build passed (existing warnings remain).
- Real Tiptap browser tests passed, including pasted images and read-only mode.
- Isolated Unity native WKWebView test passed with a 1 MB embedded image,
  Unicode, formatting, percent/plus signs and private notes.
- 57 targeted backend tests passed; migration drift check is clean.
- A broader community run also found an unrelated organization-policy assertion
  (`test_managed_therapist_external_contacts_are_off_by_default`, expected 403,
  received 200). That policy is outside this editor change and was not modified.

## macOS shortcut correction

The offscreen GREE wrapper originally passed typed characters with no Command
modifier. Its macOS bridge now forwards editing key-down/up events and native
clipboard actions, including screenshot paste. The source, universal binary and
rebuild script are in the vendored package. An isolated native Unity test covers
actual macOS pasteboard text/image data, not a synthetic browser paste event.

Run the native clipboard regression with
`tools/report-editor/tests/macos/run.sh` (Xcode and Unity 2022.3 required).
It restores the prior pasteboard afterward and checks Command+A/C/X/V,
Command+B/I/U, Command+Z/Shift+Z, and real system PNG paste. Rebuild the plugin
with `Packages/net.gree.unity-webview/Native~/Mac/build.sh`. Restart the Unity
Editor or Mac player after replacing the native plugin; loaded bundles do not
hot-reload.

## Color and image sizing correction (2026-11-14)

Text color uses an embedded swatch/hex palette instead of a native color input,
so WKWebView does not need to open a macOS color panel. It restores the selected
text range before applying Tiptap's color command. Images use the official Tiptap
Image extension's corner resize handles with the aspect ratio locked. HTML width
and height attributes survive reload; PDF rendering uses the selected width
(converted from CSS pixels to points) while fitting the page and preserving the
image ratio. Browser regressions cover selected-text color after moving focus to
a custom hex input, actual mouse dragging, dimension persistence and read-only
mode. Backend regressions cover saved dimensions and PDF image size.

## Report pane fit (2026-11-15)

The fixed A4 document now scales to the report pane width at 100% display zoom.
The 50–100% controls scale relative to that fit, and resizing the divider recalculates
the fit. This changes presentation only: logical A4 width, line breaks, stored HTML,
image dimensions and PDF output stay unchanged. Browser checks cover 430px and
1760px panes, resizing and zoom without document reflow.
