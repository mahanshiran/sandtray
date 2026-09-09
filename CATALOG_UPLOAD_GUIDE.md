# Custom Object Library

## User workflow

1. Sign in and open **Objects** from the home screen.
2. Create a catalog.
3. Choose its sharing mode: Private, Clinic, Public, or Marketplace.
4. Select **Upload**, choose a GLB model and PNG/JPG/WebP thumbnail, enter its
   name and category, and upload it.
5. Open a sandbox. Personal and accessible shared objects appear alongside the
   official library.

The file chooser works in the Unity Editor, Windows/macOS/Linux standalone
builds, Android, and iOS. Desktop uses Simple File Browser; mobile uses Native
File Picker. Both packages are pinned in `Packages/manifest.json`.

## Live-session sharing

The host sends catalog metadata to participants before sending the board state.
A joined patient merges that manifest into the local object browser and downloads
models as needed. Private catalogs remain absent from the general public library
but are usable by participants in that live room.

Visibility behavior:

- **Private:** owner and live-room participants.
- **Clinic:** owner, active linked clients, and live-room participants.
- **Public / Marketplace:** all users.

## API flow

```text
upload GLB -> upload thumbnail -> create catalog object -> refresh library
```

Files are stored in the server's persistent media volume and served through
`https://api.sandtraypro.com/api/catalogs/files/{asset-id}/`.
