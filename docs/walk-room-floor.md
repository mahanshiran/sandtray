# Walk mode outside the tray

Walkers who jump over the physical tray rim from tall sand can now fall onto the therapy-room floor and continue walking and jumping. The rim still blocks ordinary movement below its top. Room walls, the table and placed models retain their CharacterController collisions, and movement is bounded by the room floor rather than the tray footprint.

Sand height is sampled only within its actual rectangular or circular footprint. Outside the tray, gravity uses the room floor height; walking underneath the table does not snap the walker through its base onto the sand. Position corrections synchronize the collision body before its next movement sweep so the room boundary cannot be undone by stale physics state.

Keyboard and touch controls, movement speed, jump strength, and entering/exiting walk mode are unchanged.

Verified with eight passing Unity EditMode tests in an isolated Unity 2022.3.62f3c1 project copy. Tests use actual SandMesh, SandboxFrame, floor/model colliders and CharacterController movement, covering both tray shapes, jumping over the rim and landing, floor movement and jumping, ordinary rim blocking, circular corners outside the sand, walking beneath the table, room boundaries and model collision outside the tray. No physical mobile-device test was performed.

Implementation: `Assets/Scripts/Camera/WalkModeController.cs`.
Tests: `Assets/Editor/Tests/WalkRoomFloorTests.cs`.
