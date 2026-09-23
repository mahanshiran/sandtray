# Session modes and release boundaries

Code audit: 2026-09-13. This describes the current implementation, not a live-server health check or device certification.

| Mode | Entry point | Editing and saving | Release boundary |
| --- | --- | --- | --- |
| Personal / same-device | Local board setup | Local user edits; local autosave | Same-device practitioner/client use shares the device account, not separate access permissions. |
| Online | Signed-in Host / Join with room code | Host chooses editor; relay validates grants; host manages saving | Use authenticated TLS relay v4 with matching app builds. Physical handoff/reconnect tests remain required. |
| Legacy direct TCP / LAN | Internal `StartHost` / `StartClient` methods | Separate legacy role and connection handling | Not an equivalent commercial online mode: no relay ticket/TLS/revision protocol guarantees. Do not expose as a fallback for failed online authentication. |

## Important distinctions

- Creating a room does not permanently make the host the editor. The host can edit or manage a client editor.
- A joined participant's host-managed save label is not confirmation that the host has persisted the latest changes. There is no cloud-board upload confirmation in this Unity flow.
- The normal setup uses Cloud hosting; a legacy LAN branch remains in the bootstrapper, but no current UI call to `StartClient` was found. Keep this limitation explicit until LAN is removed or separately secured and tested.
- An online session still depends on its host. There is no automatic host migration. Reconnection attempts must not be described as guaranteeing room recovery after the host leaves.
- Agora audio/video is separate from board synchronization. Joining a board does not guarantee a camera/microphone permission grant or a successful media connection.
- Never send therapist notes or client records as part of a participant invitation or media session.

## Outstanding acceptance work

- Test host termination versus temporary network loss, reconnect exhaustion and deliberate leave on two devices.
- Check therapist-host/client-editor and host-as-editor with the same build/protocol versions.
- Decide separately whether a supported LAN product mode is needed. Adding it requires authenticated transport, permissions, feature-parity and device tests; existing raw TCP is not sufficient.
