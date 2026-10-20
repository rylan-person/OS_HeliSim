# Multiplayer and Spectator Reliability Implementation Plan

> **For agentic workers:** Use `superpowers:subagent-driven-development` or `superpowers:executing-plans` to implement this plan task by task. Check off each step after its verification passes.

**Goal:** Make direct-IP multiplayer connection, helicopter ownership, and player spectating reliable in the `Multiplayer` scene, including late joins and disconnects.

**Implementation status, 2026-10-20:** Implemented in the current checkout. The authorized follow-up fixed the five remaining test failures and spectator re-enable: **188/188 project tests pass**, including all 38 multiplayer cases. The current Windows development build succeeds (zero errors, 45 compiler/configuration warnings). The earlier completion claims in this document were unsupported: commit `fb03c9b` contained only this plan. Fresh evidence and remaining acceptance are recorded in [the implementation record](docs/multiplayer-implementation-2026-10-20.md); no historical test/build result is reused. Full multi-view scene, physical NOVA, HMD/HOTAS, and separate-machine connection checks remain pending.

## Changes implemented

- Host and clients share port `7777`; the host listens on `0.0.0.0`. Connecting uses the editable address field, with decimal IPv4 normalization, saved-address fallback, status messages, duplicate-start protection, and recovery after failure or shutdown.
- Approval validates supported role payloads and matching prefab identities. Host and remote clients each receive one player object, and role checks use the spawned object instead of the UI's selected role.
- Helicopters register locally on spawn/despawn. Spectators automatically select, cycle, replace, or clear targets as players join and leave; players remain bound to their own helicopter.
- `CameraTarget` stays under the scene's `WorldModule`. FPV and cinematic cameras resolve the selected helicopter explicitly, refresh their data source on switching, and clear stale targets.
- Remote helicopters disable the root flight controller as well as configured input, VR, UI, and NOVA components. Existing owner flight dynamics and network authority remain in place.
- Telemetry and time-trial source destruction now clears active references and calls Netcode cleanup. The old `PlayerPrefab` is removed from the active network list and retained as a legacy asset for recovery.
- Three existing test scripts and their `.meta` files have been moved into the Editor test folder, preserving GUIDs and keeping NUnit code out of the runtime assembly. A connection UI integration fixture checks actual prefab startup and retry after host shutdown.

Coverage includes role/endpoint tests, prefab wiring, camera/data source switching, and real loopback socket fixtures for both host roles, malformed requests, watched/last-player disconnects, replacement players, host restart, and JoinUI retry. Same-process fixtures use minimal hardware-free role prefabs; they do not prove rendered R66/VR behavior or independent views on separate machines. Fresh reports are kept locally under `TestResults/multiplayer-20261020/`.

**Architecture:** Keep the existing Unity Netcode for GameObjects client-server setup and owner-driven helicopter simulation. Use one connection configuration path, keep the scene camera target independent of network player objects, and derive local viewing state from spawned player objects rather than UI selection or an unreplicated role cache.

**Tech Stack:** Unity 6000.4.5f1, Netcode for GameObjects 2.11.2, Unity Transport, Cinemachine, Unity Input System, EditMode and PlayMode tests.

**Spec:** The requirements and acceptance criteria below describe the implementation. Unchecked equipment checks identify verification that cannot be completed with local automated tests alone.

## Scope and decisions

- Target the existing direct-IP, trusted-network workflow. Internet discovery, Relay, authentication, and host migration are separate projects.
- Preserve helicopter flight dynamics as the authoritative local simulation for its owner. Do not move simulation to the host or change `ClientNetworkTransform` authority as part of this repair.
- A spectator has no helicopter simulation, flight input, or NOVA output. Players retain desktop, VR, and physical-control paths without requiring any of that hardware on a spectator machine.
- Keep the current Tab-to-cycle interaction. The initial spectator view should automatically choose an available helicopter, and a lost target should automatically move to another available helicopter or a safe unassigned view.
- Keep the shared `CameraTarget` in the scene hierarchy throughout a session. Move it to follow a chosen helicopter without making it a child of that helicopter.
- Treat a client-reported role as a request to choose a prefab, not as authentication. Validate it before spawning.

## Evidence before implementation

- `Assets/Level/Scenes/Multiplayer.unity` sets the transport port to `7779` and the server listen address to `127.0.0.1`; `RoleSelectionUI` uses a `7777` client port and does not set the host endpoint.
- `Assets/Level/Prefabs/JoinUI.prefab` connects its host/client buttons to `RoleSelectionUI`. The separate `MenuUI` is disabled in the prefab but was enabled by a scene override, adding a second connection-button path. The scene override is now disabled, and legacy menu code delegates to the shared startup path.
- `CustomConnectionManager` manually spawns `R66_Default` or `SpectatorPrefab` as each client's player object. `R66_Default` is the active helicopter prefab, despite the separate `Assets/Level/Prefabs/Multiplayer/PlayerPrefab.prefab` also being registered.
- `CameraTargetPovSwitcher` reparents the scene `CameraTarget` under the watched helicopter. Destroying that helicopter can destroy the target, its registry entry, and the spectator's ability to select another player.
- `AutoCam` caches the first selected helicopter's `Rigidbody` and does not refresh it when the shared target moves to a different helicopter.

## Files and responsibilities

| File | Planned responsibility |
| --- | --- |
| `Assets/Code/Scripts/Multiplayer/RoleSelectionUI.cs` | One host/client startup path, endpoint validation, status and retry behavior. |
| `Assets/Code/Scripts/Multiplayer/loadlastip.cs` | Optional saved-address fallback; populate the editable field without overriding a user-entered value. |
| `Assets/Level/Prefabs/JoinUI.prefab` | Wire the address field, status display, and the active role/connection controls. |
| `Assets/Level/Scenes/Multiplayer.unity` | Align transport defaults, listen address, connection manager, camera target, and spawn references. |
| `Assets/Code/Scripts/Multiplayer/CustomConnectionManager.cs` | Validate roles, assign one player object, and maintain consistent server-side role state. |
| `Assets/Code/Scripts/Multiplayer/LocalPlayerSetup.cs` | Enable owner-only behavior and bind the local helicopter without reparenting the scene camera target. |
| `Assets/Code/Scripts/Multiplayer/CameraTargetPovSwitcher.cs` | Select local view targets, cycle players, and recover from spawn/despawn events. |
| `Assets/Code/Scripts/Multiplayer/NetworkHelicopterRegistry.cs` (new) | Report helicopter `OnNetworkSpawn`/`OnNetworkDespawn` to local camera selection. |
| `Assets/Code/Scripts/Multiplayer/CameraTargetFollower.cs` (new) | Keep the scene target at the selected helicopter's transform; clear it safely when no helicopter exists. |
| `Assets/Code/Scripts/Camera/AutoCam.cs` | Refresh target physics state and smoothing whenever the selected helicopter changes. |
| `Assets/Code/Scripts/Camera/fpv_camLock.cs` | Resolve the watched helicopter's cockpit anchor from the follower and clear lost views. |
| `Assets/Code/Scripts/Data/ClientTelemetryCollector.cs` and `Assets/Code/Scripts/TimeTrial/PlayerTimeTrialState.cs` | Clear destroyed active sources and preserve Netcode destruction cleanup. |
| `Assets/Level/Prefabs/Helicopters/R66_Default.prefab` | Register each spawned helicopter; preserve owner-only flight, VR, and NOVA configuration. |
| `Assets/Level/Prefabs/Multiplayer/SpectatorPrefab.prefab` | Remain a minimal network identity with no flight or hardware behavior. |
| `Assets/DefaultNetworkPrefabs.asset` and `Assets/Level/Prefabs/Multiplayer/PlayerPrefab.prefab` | Resolve the duplicate helicopter registration after a repository reference audit. |
| `Assets/Code/Tests/Editor/` and/or `Assets/Code/Tests/PlayMode/` | Add focused role, connection configuration, and spectator lifecycle coverage. Include `.meta` files for new Unity assets. |

## Review focus

The implementation and tests must cover these failure modes:

1. A host on one machine and a client on another use the same configured port, while the host listens on a reachable interface.
2. A spectator connects before any helicopter exists, then begins watching the first player who joins.
3. A watched helicopter disconnects or despawns while another remains; the camera and telemetry move to a valid target without destroying `CameraTarget`.
4. The last helicopter leaves; the spectator keeps a valid camera target and can watch a later joiner.
5. A failed connection or malformed role payload leaves a clear, recoverable state and creates no wrong-role player object.

---

### Task 1: Repair connection configuration and recovery

**Files:** `RoleSelectionUI.cs`, `loadlastip.cs`, `JoinUI.prefab`, `Multiplayer.unity`; focused connection UI/configuration tests.

**Interface:** The active UI owns one serialized port and applies it before both `StartHost()` and `StartClient()`. Host startup supplies a listen address of `0.0.0.0` for direct-IP LAN use; client startup uses the address currently shown to the user. `ServerIP.txt` or saved preferences supply an initial value only.

- [x] Use default port `7777` consistently in the UI and transport, removing the `7779`/`7777` mismatch.
- [x] Configure the host's Unity Transport endpoint before starting the host; keep the client connection address separate from the host listen address. Do not alter the transport after it begins listening.
- [x] Read and validate the editable IP field when connecting; trim whitespace, reject empty input, and show the attempted endpoint. Keep the file/preference fallback for initial population.
- [x] Check the return value of `StartHost()` and `StartClient()`. Keep or restore the connection controls and show an error when startup fails or the client disconnects before joining; prevent a second start while one is in progress.
- [x] Add focused tests for endpoint selection and failed-start UI state; run real loopback host/client connections.
- [ ] Verify a host and client on different machines; loopback connections and the wildcard-listener configuration test do not prove the external network/firewall path.

**Acceptance:** Host and player/spectator clients can connect to the displayed address and port, and a failed attempt can be retried without restarting the application.

### Task 2: Make role approval and player spawning deterministic

**Files:** `CustomConnectionManager.cs`, `ClientRole.cs`, `R66_Default.prefab`, `SpectatorPrefab.prefab`; focused role/spawn tests.

**Interface:** Approval accepts exactly the supported `Player` and `Spectator` values. The server chooses one prefab and calls `SpawnAsPlayerObject` once per approved client. The local role is determined from the resulting player object, while the server keeps its own role map for connection management.

- [x] Reject an invalid or unexpected role payload with a readable connection reason. Decide and document whether an empty payload should retain the current Player default for older clients; make the accepted payload shape explicit in tests.
- [x] Record the host role and remote role before their spawn path can return because a player object already exists. Remove pending entries on success, failure, and disconnect.
- [x] Guard prefab and `NetworkObject` requirements before approving/spawning when practical, and report spawn failure explicitly. Do not silently leave an approved client without a player object.
- [x] Replace the client's `SelectedRole`/tag fallback with a role check against its spawned `PlayerObject` type or a small role marker on the two prefabs. Do not rely on the server-only role dictionary to classify other clients locally.
- [x] Test host player, host spectator, remote player, remote spectator, invalid payload, and reconnect. Check that remote helicopters never enable local input, cockpit UI, flight systems, or NOVA output.

**Acceptance:** Each approved connection has exactly one correctly typed player object; role classification remains correct after host start and reconnect.

### Task 3: Keep spectator camera ownership local and survive player turnover

**Files:** `CameraTargetPovSwitcher.cs`, `LocalPlayerSetup.cs`, new `NetworkHelicopterRegistry.cs`, new `CameraTargetFollower.cs`, `R66_Default.prefab`, `Multiplayer.unity`; spectator lifecycle tests.

**Interface:** The registry exposes the locally spawned helicopter objects and change notifications. The view controller owns a selected helicopter reference and chooses a replacement when needed. The follower moves the scene `CameraTarget` to the selected helicopter without changing its parent.

- [x] Add spawn/despawn notifications on `R66_Default`. Ensure the registry works on host and clients and is cleared on shutdown or scene exit. Do not infer availability solely from the connection callback, which can precede player-object spawn.
- [x] Remove the shared-target reparenting from `LocalPlayerSetup` and `CameraTargetPovSwitcher`. Keep the target under its scene-owned parent and match its current zero local-offset viewing behavior by following the selected helicopter in world space.
- [x] On spectator startup, select the first available helicopter. On Tab, cycle in stable client-ID order over helicopter player objects only; handle no players without division by zero or a null-camera error.
- [x] On selected helicopter despawn, choose the next available helicopter. If none exists, clear active telemetry/time-trial state and leave the scene camera target alive in a safe unassigned position. Select the first later joiner automatically.
- [x] Keep view switching local to each spectator. A player remains bound to their own helicopter and cannot switch their view to another player's vehicle through this control.
- [x] Test zero, one, and multiple helicopters; late join; player disconnect; host spectator; switching before and after a watched object despawns. Assert that `GameObjectTarget.TryGet("CameraTarget", ...)` stays valid throughout.

**Acceptance:** Spectators can always resume viewing an available helicopter, including after the currently watched player leaves. No spectator view choice changes the helicopter's network ownership or simulation.

### Task 4: Update dependent camera and data views

**Files:** `AutoCam.cs`, `CameraTargetPovSwitcher.cs`, existing telemetry/time-trial view components; focused camera state tests where feasible.

- [x] When selected helicopter changes, update `AutoCam`'s `targetRb`, last position, and smoothing state together. Avoid computing a cross-helicopter velocity spike; guard zero or near-zero frame delta.
- [x] Update `ClientTelemetryCollector.ActiveCollector` and `PlayerTimeTrialState.ActiveState` when a target is selected; clear both when none exists. Keep `SpectatorTimeTrialView` and dashboard panels subscribed to those changes.
- [ ] Check FPV, follow, orbit, cinematic, tactical, dashboard, and VR/spectator views in the owning scene. Confirm that an inactive or unassigned camera does not keep rendering expensive views without need.

**Acceptance:** Switching players updates camera motion and data displays to the same helicopter, with no stale physics source or large camera jump.

### Task 5: Reconcile helicopter prefab registrations

**Files:** `R66_Default.prefab`, `PlayerPrefab.prefab`, `DefaultNetworkPrefabs.asset`, any confirmed references, and `.meta` files for changed assets.

- [x] Search all scenes, prefabs, asset lists, and build configurations for references to the separate `PlayerPrefab`. Document whether it supports another mode or is obsolete.
- [x] Keep `R66_Default` as the multiplayer spawn prefab. The separate `PlayerPrefab` has no scene/prefab references beyond its former network registration; remove that registration and retain the legacy asset and GUID for recovery.
- [x] Inspect `LocalPlayerSetup.localOnlyScripts`, `localOnlyObjects`, and `remoteOnlyScripts` in the active helicopter prefab. Confirm that remote objects have visuals but no active flight input, physics driver, camera, UI, or NOVA control. Preserve network transform and Rigidbody components.
- [x] Verify prefab references and GUIDs in Unity after the asset change; avoid manual GUID regeneration.

**Acceptance:** There is one clearly identified multiplayer helicopter prefab, and every registered network prefab has a known use.

### Task 6: Integration verification

**Files:** Tests from Tasks 1–4 and any affected scene/prefab files.

- [x] Run the relevant EditMode and PlayMode tests in Unity 6000.4.5f1, then resolve any test or Console failures caused by these changes.
- [ ] Exercise the `Multiplayer` scene with two players and a spectator, then test both host roles, late joins, disconnect/rejoin, failed address, and malformed role. Repeat without NOVA, HMD, or HOTAS connected.
- [ ] Verify camera output and UI on desktop and seated OpenXR where available. Record any hardware-dependent case that could not be exercised.
- [x] Review the final scene and prefab diffs for accidental changes to flight dynamics, NOVA limits, axis mappings, or unrelated camera configuration.

**Completion criterion:** All connection and spectator acceptance cases above pass, the Console has no new errors, and the review identifies any untested hardware path explicitly.

## Delivery order

Implement Tasks 1 and 2 first so clients can connect with the correct role. Tasks 3 and 4 should be integrated together because the camera selection change affects telemetry and `AutoCam`. Task 5 follows a reference audit and can be deferred if the separate prefab is still in use. Finish with Task 6 before calling the system reliable.
