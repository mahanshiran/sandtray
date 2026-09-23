# Session modes and account type

Sandtray currently supports these local and live workflows:

| Workflow | Start point | Default editing behavior |
| --- | --- | --- |
| Personal or same-device work | Create a local board, optionally from a client record | The signed-in creator edits locally. No live room is created. |
| Online session from the session screen | Host an existing or new board | The host explicitly selects whether the host or invited client begins as editor. |
| Online session from an active client record | **Host session** on that client page | The practitioner hosts; the invited client begins waiting for explicit editing approval. The client ID passes through board-size setup into the new record. |

Account type is a workspace preference. Choosing Therapist exposes the local client workspace and therapist profile; choosing Personal hides those practitioner-only screens. It does not determine a participant's live editing role. Live role grants, pauses, transfers, and removals use the room's session-role messages and permission controls.

Existing boards retain their client association and the selected live session mode does not rewrite account type. Account type is also not a verification of professional credentials.

## Validation

Automated navigation checks cover visibility changes when the preference changes. Session-role routing checks cover targeted role messages independently of account type. Full two-device interaction and mobile layout validation remain separate release tasks.
