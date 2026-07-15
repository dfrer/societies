# Societies — Behavioral Contracts
## Player Controller & Camera Systems

**Purpose**: This document defines the explicit, testable behavioral contracts for all player-facing input and camera systems in Societies. Every contract here encodes a *convention correctness* expectation — things that feel obviously right to a human player but are invisible to automated tests unless explicitly specified. Codex must validate all of these before marking any input/camera/movement task complete.

**Engine**: Godot 4 (C#)  
**Source constants**: `TechnicalConstants.cs` (authoritative)  
**Perspective**: Third-person over-the-shoulder (implied by low-poly 3D, open world, Eco-comparable design)

> **Canonical alignment (2026-07-14):** These are targeted player-facing reference contracts. The product contract is [PRODUCT-THESIS.md](../PRODUCT-THESIS.md); current scope and implementation truth remain [`planning/active/`](../active/) and [`CURRENT_BUILD.md`](../../CURRENT_BUILD.md).

---

## CONTRACT GROUP 1: WASD Movement Direction

These contracts define the relationship between key input and world-space movement. All directions are relative to the **camera's current horizontal facing vector**, projected onto the XZ plane (Y-up world).

### BC-MOV-001 — W moves forward
```
GIVEN: Player is on flat ground, camera is facing world direction D
WHEN: W is held for 1 physics frame
THEN: player.GlobalPosition has moved in direction D (dot product of delta with D > 0)
      and the signed delta on the axis opposite to D is NOT positive
```

### BC-MOV-002 — S moves backward
```
GIVEN: Player is on flat ground, camera is facing world direction D
WHEN: S is held for 1 physics frame
THEN: player.GlobalPosition has moved in direction -D (dot product of delta with D < 0)
      and the signed delta on the axis aligned with D is NOT positive
```

### BC-MOV-003 — W and S are strict opposites
```
GIVEN: Player starts at position P0
WHEN: W is held for N frames, then S is held for N frames (same stamina/terrain conditions)
THEN: player.GlobalPosition is approximately equal to P0 (within float tolerance)
NOTE: This catches sign-flip bugs even if BC-MOV-001 and BC-MOV-002 individually pass
      due to a mirrored assignment where both are wrong in the same direction.
```

### BC-MOV-004 — A strafes left
```
GIVEN: Player is on flat ground, camera facing D, player not moving forward/back
WHEN: A is held for 1 physics frame
THEN: player.GlobalPosition has moved in the direction 90° counterclockwise from D
      (cross product of D with world-up gives right vector; left is negation of that)
```

### BC-MOV-005 — D strafes right
```
GIVEN: Player is on flat ground, camera facing D, player not moving forward/back
WHEN: D is held for 1 physics frame
THEN: player.GlobalPosition has moved in the direction 90° clockwise from D
      (positive right vector relative to camera facing)
```

### BC-MOV-006 — A and D are strict opposites (same as BC-MOV-003 pattern)
```
GIVEN: Player starts at position P0
WHEN: A is held for N frames, then D is held for N frames
THEN: player.GlobalPosition ≈ P0
```

### BC-MOV-007 — Diagonal movement is additive, not dominant
```
GIVEN: Player holds W+D simultaneously
THEN: movement vector is normalized (magnitude ≈ 1.0, not 1.414)
      and the direction is 45° between forward and right (not purely forward, not purely right)
```

---

## CONTRACT GROUP 2: Movement Speeds

Source: `TechnicalConstants.MOVEMENT_SPEED_WALK = 3.0f`, `MOVEMENT_SPEED_SPRINT = 6.0f`, `MOVEMENT_SPEED_CARRYING = 2.0f`

### BC-SPD-001 — Walk speed is exactly 3 m/s
```
GIVEN: Player is walking (no sprint, no carry penalty, flat ground)
WHEN: W is held for 1 real second
THEN: distance traveled ≈ 3.0 meters (±0.1 tolerance for physics integration)
```

### BC-SPD-002 — Sprint speed is exactly 6 m/s (2× walk)
```
GIVEN: Player has stamina > SPRINT_MIN_STAMINA_TO_START (20.0)
       and Shift is held alongside W
WHEN: measuring over 1 second
THEN: distance traveled ≈ 6.0 meters
```

### BC-SPD-003 — Sprint requires sufficient stamina to initiate
```
GIVEN: Player stamina = 19.0 (below SPRINT_MIN_STAMINA_TO_START = 20.0)
WHEN: Shift+W is pressed
THEN: player moves at walk speed (3.0 m/s), NOT sprint speed
```

### BC-SPD-004 — Heavy carry reduces speed to 2 m/s
```
GIVEN: Player inventory weight > INVENTORY_WEIGHT_MAX_KG (100.0 kg)
WHEN: W is held
THEN: distance traveled per second ≈ 2.0 meters, not 3.0
```

### BC-SPD-005 — Road bonus applies correctly
```
GIVEN: Player is on a road tile
WHEN: walking W for 1 second
THEN: distance traveled > 3.0 meters (road provides speed bonus per design doc section 12)
      and distance < 6.0 meters (road bonus does not exceed sprint)
```

---

## CONTRACT GROUP 3: Camera Behavior

### BC-CAM-001 — Mouse X rotates camera horizontally (yaw)
```
GIVEN: Player is stationary, camera facing direction D
WHEN: Mouse moves +X (right) by delta pixels
THEN: camera.GlobalRotation.Y decreases (yaw left in Godot's Y-up right-hand system)
      meaning the camera now faces a direction rotated clockwise when viewed from above
NOTE: In Godot Y-up right-hand: rotating right in world space = negative Y rotation.
      If this is counterintuitive, add a comment. Do NOT silently flip this.
```

### BC-CAM-002 — Mouse Y rotates camera vertically (pitch), not inverted
```
GIVEN: Default (non-inverted) mouse settings
WHEN: Mouse moves +Y (down on screen, toward player)
THEN: camera pitches downward (looks at ground)
      camera.Rotation.X becomes more negative (in Godot pitch-down = negative X)
WHEN: Mouse moves -Y (up on screen, away from player)  
THEN: camera pitches upward (looks at sky)
```

### BC-CAM-003 — Camera pitch is clamped
```
GIVEN: Player looks up continuously
THEN: camera pitch clamps at maximum look-up angle (suggested: 80° above horizon)
      and does NOT flip/gimbal through vertical

GIVEN: Player looks down continuously  
THEN: camera pitch clamps at maximum look-down angle (suggested: -70° below horizon)
      and does NOT clip through terrain
```

### BC-CAM-004 — WASD movement direction is always relative to camera facing
```
GIVEN: Player has rotated camera 90° to the right from world-north
WHEN: W is pressed
THEN: player moves east (the camera's new forward), NOT north (world Z)
NOTE: This is the fundamental camera-relative movement contract. Any implementation
      that uses world-space forward instead of camera-projected forward fails this.
```

### BC-CAM-005 — Camera does not move independently from the player pivot
```
GIVEN: Player is stationary (no WASD input)
WHEN: Mouse is moved in any direction
THEN: player.GlobalPosition does not change
      only camera rotation changes
```

### BC-CAM-006 — Third-person camera maintains distance from player
```
GIVEN: A target third-person offset distance D_cam
WHEN: Player moves or camera rotates
THEN: distance from camera to player pivot ≈ D_cam at all times
      (spring arm / camera arm length is preserved, not accumulated or drifted)
```

---

## CONTRACT GROUP 4: Sprint Mechanics

Source: `SPRINT_STAMINA_COST_PER_SECOND = 2.0`, `SPRINT_RECOVERY_TIME_SECONDS = 2.0`

### BC-SPR-001 — Sprint drains stamina at correct rate
```
GIVEN: Player is sprinting (Shift+W), stamina = 100.0
WHEN: 1 real second elapses
THEN: stamina ≈ 98.0 (drained by SPRINT_STAMINA_COST_PER_SECOND = 2.0)
```

### BC-SPR-002 — Sprint automatically stops when stamina hits 0
```
GIVEN: Player is sprinting, stamina approaches 0
WHEN: stamina reaches 0
THEN: player movement speed drops to walk speed (3.0 m/s) automatically
      without requiring player to release Shift
```

### BC-SPR-003 — Stamina recovery has delay after sprint
```
GIVEN: Player stops sprinting (releases Shift) at time T0
WHEN: T < T0 + SPRINT_RECOVERY_TIME_SECONDS (2.0s)
THEN: stamina does NOT increase (recovery delay active)

WHEN: T >= T0 + SPRINT_RECOVERY_TIME_SECONDS
THEN: stamina begins recovering at STAMINA_REGEN_WALK_PER_HOUR rate
```

---

## CONTRACT GROUP 5: Interaction & Action Keys

### BC-ACT-001 — E is the primary interaction key
```
GIVEN: Player is within interaction range of an interactable object
WHEN: E is pressed
THEN: interaction event fires exactly once
      and does NOT fire on E release (press, not toggle)
```

### BC-ACT-002 — Interaction has a maximum range
```
GIVEN: Player is beyond interaction range of an object
WHEN: E is pressed
THEN: no interaction event fires
      and a "too far" indicator appears if the object is still visible
```

### BC-ACT-003 — Left mouse button is primary tool/action use
```
GIVEN: Player has a tool equipped
WHEN: Left mouse button is pressed
THEN: tool action fires in the direction of the camera's forward ray
      and does NOT fire in the opposite direction
```

---

## CONTRACT GROUP 6: Network & Authority

These contracts apply specifically to the client-server architecture. Source: `NETWORK_SYNC_POSITION_EVERY_TICKS = 1` (every tick at 20 TPS).

### BC-NET-001 — Server is authoritative on position
```
GIVEN: Client predicts movement locally
WHEN: Server position update arrives that differs from client prediction
THEN: client position snaps/lerps to server position
      and does NOT ignore the correction
```

### BC-NET-002 — Input is applied locally for responsiveness
```
GIVEN: Player presses W
WHEN: measuring time until visual movement begins on the client
THEN: movement begins within 1 frame (≤ 50ms) locally
      even if server acknowledgement has not yet arrived
```

### BC-NET-003 — Position sync fires every tick
```
GIVEN: Player is moving
WHEN: measuring outgoing network packets
THEN: position update is emitted every 1 tick (50ms interval)
      matching NETWORK_SYNC_POSITION_EVERY_TICKS = 1
```

---

## CONTRACT GROUP 7: Godot-Specific Axis Conventions

These exist specifically because Godot's coordinate system is a common source of sign-flip bugs.

### BC-GOD-001 — World-space conventions are documented and consistent
```
Godot 4 uses:
  +X = right
  +Y = up  
  -Z = forward (into screen / character's forward by default)

THEREFORE:
  Walking forward (W) = moving in the -Z direction in world space
  (or in camera-space forward projected onto XZ plane)
  
Any implementation using +Z as forward is WRONG and will produce the W/S inversion bug.
```

### BC-GOD-002 — CharacterBody3D velocity assignment is camera-relative
```
GIVEN: Camera facing direction computed as -camera.GlobalTransform.Basis.Z projected onto XZ plane
WHEN: W is pressed
THEN: velocity = forward_direction * MOVEMENT_SPEED_WALK
      NOT velocity = -forward_direction * MOVEMENT_SPEED_WALK
      NOT velocity = Vector3.Forward * MOVEMENT_SPEED_WALK (world forward, not camera forward)
```

### BC-GOD-003 — Mouse sensitivity produces natural-feeling camera
```
GIVEN: Default sensitivity setting
WHEN: mouse moves 1cm on screen (approx 40 pixels at 96dpi)
THEN: camera rotates approximately 2-4 degrees
      (if it rotates <0.5° it feels dead; if it rotates >10° it feels violent)
```

---

## Testing Implementation Guide

### How to use these contracts in Codex

For each contract, Codex should implement a GDScript or C# test function that:

1. **Sets up** the exact preconditions stated
2. **Performs** the input action (use `Input.action_press()` in Godot test environment or simulate via direct velocity injection if headless)
3. **Advances** physics by the required number of frames (`await get_tree().physics_frame`)
4. **Asserts** the postcondition using `assert()` with a descriptive message

**Template**:
```csharp
[Test]
public void BC_MOV_001_W_MovesForward()
{
    // Setup
    var player = SpawnPlayerAt(Vector3.Zero);
    var cameraForward = Vector3.Forward; // -Z in Godot
    SetCameraFacing(player, cameraForward);
    
    // Act
    SimulateKeyPress(Key.W);
    AdvancePhysicsFrames(1);
    
    // Assert
    var delta = player.GlobalPosition - Vector3.Zero;
    var dotWithForward = delta.Dot(cameraForward);
    Assert.IsTrue(dotWithForward > 0,
        $"W key should move player forward (dot > 0), got dot={dotWithForward}. " +
        $"Player moved to {player.GlobalPosition}. Check velocity assignment sign.");
}
```

### Priority order for validation

Run these contracts in this order after any change to input/camera/movement code:

1. BC-GOD-001 (axis convention — read and confirm in code comments)
2. BC-MOV-003 (W+S cancellation — catches the mirroring bug directly)
3. BC-MOV-006 (A+D cancellation)
4. BC-CAM-004 (camera-relative movement — catches world-space forward bug)
5. BC-MOV-001, BC-MOV-002, BC-MOV-004, BC-MOV-005 (individual directions)
6. BC-SPD-001 through BC-SPD-004 (speeds against constants)
7. All remaining contracts

---

*This document is a living behavioral contract. When new input systems are added (vehicles, tools, swimming, climbing), add contracts here before implementation.*
