# Hybrid Multiplayer Connections Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. Implementation and commits require authorization in this repository's session.

**Goal:** Add Online hosting and joining through Unity Relay while retaining direct IP connections for LAN installations and cloud-independent Solo flight.

**Architecture:** Keep one Netcode for GameObjects simulation and role/spawn system. Select either a direct connection or a Multiplayer Services session using Relay; both feed a shared, attempt-owned network lifecycle. The existing menu coordinator continues to own scene transitions, recording finalization and return to menu.

**Tech Stack:** Unity 6000.4.5f1; Netcode for GameObjects 2.11.2; Multiplayer Services 2.2.2; Unity Transport 2.7.2; Authentication 3.6.1; existing uGUI/TMP menus and Editor tests.

**Spec:** The [design contract](#design-contract) in this document. Source inspection date: 2026-10-20. This is a proposed implementation plan, with no runtime changes or new verification results implied.

## Global Constraints

- Preserve the pinned Editor and packages; the required multiplayer package is already installed.
- Preserve Player/Spectator approval, ownership, late joining, helicopter registration and local-only cameras/input. Relay changes connectivity, not simulation authority.
- Solo and LAN must work without internet, UGS initialization or authentication.
- Preserve the existing role selection in the main menu; do not show the legacy role prompt after a menu launch.
- Preserve NOVA shutdown safeguards, recording finalization, seated VR and desktop controls. No physical motion is authorized by this work.
- Keep the existing dirty menu/Mapbox work and unrelated files intact. Re-inspect them before execution; a clean checkout of HEAD alone does not contain all current menu repairs.
- Use supported Editor tooling for serialized assets and generated `.meta` files. Keep NUnit in the predefined Editor assembly.
- Follow [the workflow](../../AGENT_WORKFLOW.md) and [testing rules](../../TESTING.md). Planning does not authorize Unity operations, cloud configuration or implementation.

## Review Focus

1. Cancel while authentication or session creation is still pending: late completion must not start a flight or overwrite a newer attempt (Tasks 3–4).
2. Relay → LAN → Relay in one application process: old transport settings, callbacks and cleanup must not affect the next connection (Tasks 2–4).
3. Spectator hosting/joining: the selected role must reach approval before either startup path; spectator input and cameras must stay local (Tasks 2, 4, 6).
4. Host exit, lost internet or service failure: stop the current flight cleanly, preserve the useful failure reason and restore usable menu controls (Tasks 3–4, 6).
5. Offline installation with saved Online preferences: switching to LAN or Solo must remain available without a cloud request or sign-in screen (Tasks 1, 5–6).

## Design contract

### Connection choices

| User choice | Host action | Join action | Internet requirement |
|---|---|---|---|
| Solo | Existing local host, loopback only | Unavailable | None for connection startup |
| Local network / Direct IP | Listen on configured local port | Enter host IPv4 address and port | None for connection startup |
| Online / Relay | Create a private session and display its join code | Enter a session join code | Required for every participant |

The offline guarantee concerns multiplayer connectivity. Existing streamed map data and other independently networked features may still need internet or their own cached assets.

Keep `MenuLaunchMode` values `Solo = 0`, `Host = 1`, `Join = 2`. Add a separate `MenuConnectionMethod` with `Direct = 0`, `Relay = 1`; missing serialized fields must resolve to Direct. Fresh installations and migrated v1 settings default to Direct. Later successful launches may remember the user's selected method.

There is no automatic fallback between methods. An Online failure offers Retry, Local network and Solo choices. Switching methods starts a new attempt only after the old attempt can no longer touch local networking. Direct internet connections remain technically possible where routing/firewalls allow them, but the menu presents this mode primarily for LAN use.

Online creation uses anonymous authentication; no account entry UI is required. Proposed first-release capacity is **8 total participants, including the host and spectators**, supplied as application configuration rather than a claimed performance limit. Validate that capacity in a representative flight before expanding it. Region selection uses the SDK's automatic QoS choice initially; expose manual region selection later only if deployment measurements justify it.

Create private sessions (`IsPrivate = true`) for code-based joining. A code grants access to the session; keep it in memory and the session UI, excluding it from saved settings, routine logs and telemetry. Codes are invitations, not user authentication. No public browser, matchmaking, friends system, password flow or automatic reconnection is included in this first version.

### Common network startup

The current path is `MainMenuScreen` → `MenuSessionCoordinator` → `RoleSelectionUI` → `NetworkManager.StartHost/StartClient`. `RoleSelectionUI` currently combines IPv4 configuration, the role payload, startup callbacks and presentation. Online startup must bypass IPv4 validation while reusing the role and lifecycle rules.

Extract that non-visual responsibility into a small `SimulatorConnection` component on the scene's existing network root. It captures the exact `NetworkManager`, transport and approval manager for an attempt; never resolves a later singleton from a stale callback. Keep `RoleSelectionUI` as a compatibility presenter for direct entry into `Multiplayer.unity`, delegating its existing public actions to this component.

Before either transport starts:

1. Validate the selected role and existing prefab/approval configuration.
2. Set `CustomConnectionManager.hostRole` and `NetworkConfig.ConnectionData` to the existing single role byte.
3. Subscribe the attempt's connection/failure callbacks and lock repeated starts.
4. Configure only the chosen transport, then start Netcode exactly once.

For Direct, use the pinned transport's `SetConnectionData(...)` API after the previous driver has stopped. Inspection confirms this API resets its protocol to direct; do not call its private `SetProtocol` or assume changing an address field resets the protocol. Preserve the existing loopback-only Solo listen address and LAN listen behavior.

For Online, use Multiplayer Services sessions, not a second independent low-level Relay allocation flow. In 2.2.2, `WithRelayNetwork()` normally starts Netcode through the SDK's built-in handler. Register an attempt-scoped `INetworkHandler` instead so cancellation can prevent late startup and the existing simulator lifecycle remains the sole caller of `StartHost/StartClient`.

The handler accepts only the expected Relay host/client configuration, applies `NetworkConfiguration.RelayServerData` with `SetRelayServerData(...)`, and delegates startup/stop to `SimulatorConnection`. `StartAsync` completes on actual NGO host readiness or local client connection, and faults on approval/transport/start failure. Reject duplicate starts and any restart after cancellation, failure or closure. `StopAsync` is idempotent and affects only its captured attempt/manager.

Use `.WithNetworkOptions(new NetworkOptions { RelayProtocol = RelayProtocol.DTLS })` on create and join for the initial Windows desktop target, alongside the create call's `.WithRelayNetwork()`. These types and enum values exist in the pinned SDK's `Unity.Services.Multiplayer` namespace. Do not copy obsolete `RelayNetworkOptions.Protocol` examples or add a package upgrade to make an example compile.

### Session ownership and cleanup

`MenuSessionCoordinator` remains the application-level owner. Keep cloud service operations in `RelaySessionConnection`; keep NGO startup in `SimulatorConnection`; keep menu rendering in presenters.

Use one monotonically identified connection attempt with an owned cancellation token and terminal outcome. Proposed visible stages are Loading scene, Connecting to online services, Creating/Joining session, Connecting to host, Connected and Closing. Stage changes and Unity-object access stay on Unity's main thread; no `.Wait()`, `.Result` or blocking network calls.

Load the flight scene and prepare its role/approval components before invoking create/join. The initial release does not create a waiting room before scene loading. Initialize UGS and anonymously sign in only when Online is selected; coalesce concurrent initialization and reuse valid authentication. Keep development/test authentication profiles isolated between player processes.

The pinned create/join APIs do not expose a per-call cancellation token. Cancel or timeout therefore invalidates the attempt immediately, cancels its local NGO wait and disables its handler's future starts. Continue observing the outstanding SDK task. If it later returns a session, close that session exactly once. Do not dispose cancellation state while a continuation still needs it, leave unobserved task exceptions, or allow old cleanup to shut down a new manager. Permit LAN/Solo once local shutdown is confirmed; serialize further Online attempts until the previous cloud create/join has settled and its cleanup is owned.

Use separate configurable initial budgets: **30 seconds for online preparation/create/join**, followed by the existing **30 seconds for NGO connection**. Timeouts produce a recoverable message, with no automatic retry loop. Keep the existing 15-second shutdown progress threshold and requirement to confirm local shutdown before destroying the network root. Bound cloud cleanup observation at **10 seconds** for foreground UI; retain an owned background continuation for unresolved cleanup rather than claiming it succeeded.

On Return to Menu or a terminal failure:

1. Mark the attempt closed and disable further starts/callback-driven transitions.
2. Preserve existing motion-output disable and recording finalization behavior.
3. For Online hosts, call `IHostSession.DeleteAsync()`; for Online clients, call `ISession.LeaveAsync()`. These invoke the registered handler's stop. For Direct, stop through `SimulatorConnection` directly. Do not independently run both normal Online shutdown paths.
4. If service closure fails before local shutdown, perform an idempotent local stop of that captured manager and keep observing/reporting cloud cleanup separately.
5. After confirmed local shutdown, remove attempt subscriptions and session-only UI data, destroy session roots, then restore MainMenu using the existing coordinator path.

Explicit host deletion matters: in the pinned SDK, a host's ordinary `LeaveAsync()` can allow the remaining service session to continue. This application is not implementing NGO host migration. Do not configure `WithHostMigration`; react to host changes, session deletion/removal and NGO disconnect as terminal flight events. No remaining participant should become a replacement simulation host or silently start a new flight. Crash/disconnect recovery requires a fresh host and code.

### Options and presentation

Add `ConnectionMethod` and transient `JoinCode` to launch options. Direct Host/Join retain IPv4/port validation; Online Join requires a trimmed nonempty code, and Online Host requires no code or endpoint. Avoid inventing a fixed code length; let the service validate its current format. Preserve assistance/recording/scene validation for all modes and force Solo to Player + Direct + loopback.

Persist a v2 envelope under `HeliSim.MainMenu.SessionOptions.v2`, reading the current v1 key as a migration fallback only when v2 is absent. A valid v1 record becomes Direct without rewriting storage during load. Save only after successful launch, strip `JoinCode`, and validate persisted preferences separately from an active Online Join request so saving a method without a code remains valid. Keep invalid/unknown stored data intact until an explicit successful save; do not silently consume v1 when a present v2 record is corrupt. Preserve existing Direct address/port preferences when Online succeeds.

The Multiplayer page adds a clearly labeled Local network / Online choice:

- Local network shows the existing address, port, Host and Join controls.
- Online shows Create online flight and Join by code, with a join-code field and a short internet requirement hint.
- Pilot/Spectator selection applies to either method. Disable role/method/start controls during transitions; keep Cancel available where meaningful.
- Validation identifies the relevant field, errors explain retry or switching method, and keyboard navigation lands on the first usable control. Retain full-field dropdown interaction and aligned option lists from the current menu repairs.

Show the host's active join code as soon as connection succeeds, then keep it accessible with a Copy button and connection method/status in the in-flight Flight/Session page. Include a modest in-flight notice directing a new host to that page. A dedicated `MultiplayerSessionPanel` presents this read-only state; avoid mixing cloud operations into `FlightSessionPage`'s flight/subtitle controls. Hide code controls for Solo/Direct and after disconnection. Copy uses the local clipboard only; never send invitations automatically.

### Rollout and limits

Keep Direct as the default during development and retain it for installations without cloud access. Shipping Online requires the Unity project/environment to be linked and the account's applicable services/quotas and billing settings to be checked by an authorized project operator. Store no service secrets in the repo or client preferences.

Relay forwards traffic; it does not host the helicopter simulation. Measure join time, round-trip latency, loss and bandwidth with representative player/spectator counts. Link current service pricing rather than freezing today's quota or price into code. Keep recordings local, map streaming on its existing services and voice on its own supported voice path; do not route recording files or media through this connection feature.

If Online acceptance fails, retain Direct and hide/disable Online with a clear reason until repaired. Do not remove working direct code or migrate scene authority. Future public discovery, dedicated servers, host migration, IPv6, WebGL/WSS and automatic reconnect need separate designs.

## File map and ownership for execution

Paths below are repository-relative. “New” files do not exist yet.

| Owner / area | Files | Responsibility |
|---|---|---|
| Options writer | `Assets/Code/Scripts/UI/FrontEnd/MenuSessionOptions.cs`, `MenuSessionOptionsStore.cs`; new sibling `MenuConnectionMethod.cs` | Validation and persistence migration |
| Lead, network lifecycle | `Assets/Code/Scripts/Multiplayer/RoleSelectionUI.cs`; new sibling `SimulatorConnection.cs` | Common preparation, exact-manager startup/stop, legacy UI compatibility |
| Lead, Online adapter | New `Assets/Code/Scripts/Multiplayer/RelaySessionConnection.cs`, `IOnlineSessionService.cs`, `UnityOnlineSessionService.cs` | Attempt handler, SDK adapter, lazy authentication and service cleanup |
| Lead, integration | `Assets/Code/Scripts/UI/FrontEnd/MenuSessionCoordinator.cs`, `MenuTransitionView.cs` | Attempt state, progress, cancellation, scene/recording teardown |
| UI writer | `Assets/Code/Scripts/UI/FrontEnd/MainMenuScreen.cs`; new `Assets/Code/Scripts/UI/InFlightMenu/MultiplayerSessionPanel.cs` | Method controls and active session/code presentation |
| Lead, assets | `Assets/Code/Tests/Editor/MenuSceneBuilder.cs`, `Assets/Level/Prefabs/UI/MainMenu.prefab`, `Assets/Level/Prefabs/UI/Menu.prefab`, `Assets/Level/Scenes/MainMenu.unity`, `Assets/Level/Scenes/Multiplayer.unity` | Supported targeted component/UI wiring; preserve GUIDs and current repaired layout |
| Tests, co-owned with feature | Existing fixtures named below; new `Assets/Code/Tests/Editor/SimulatorConnectionTests.cs`, `RelaySessionConnectionTests.cs`, `MultiplayerSessionPanelTests.cs` | Deterministic lifecycle, migration, UI and real transport coverage |

Only delegate disjoint files after agreeing interfaces. Lead owns difficult networking and integration; reviewers are read-only. One named operator owns all Unity/test/build operations. Existing `CustomConnectionManager`, helicopter physics, NOVA and authority scripts should need no behavior changes for this feature.

## Implementation sequence

Before Task 1, re-inspect the working tree, relevant source and package pins, then obtain independent networking/lifecycle and local/spectator UX review of this design under the repository's high-risk policy. Resolve design blockers before writing runtime code. Check the linked Unity project/environment prerequisites before attempting any live Online verification; deterministic tests should not require cloud credentials.

### Task 1: Connection options and backward-compatible preferences

**Files:** The options row above; `Assets/Code/Tests/Editor/MenuSessionOptionsTests.cs`.

**Interfaces:** `MenuConnectionMethod { Direct = 0, Relay = 1 }`; `MenuSessionOptions.ConnectionMethod`, `string JoinCode`; retain `TryValidate(Func<string, bool>, out string)` for launch validation. Add a distinct preference-validation path in the store that does not require a transient code.

- [ ] Add failing tests for Direct's existing validation; Online with empty/irrelevant IP and port; trimmed/missing join codes; invalid enum values; Solo normalization; v1 migration; v2 round trip without codes; corrupt/unknown v2 preservation; retained LAN endpoint after Online success.
- [ ] Discover and run `MenuSessionOptionsTests`, confirming the new assertions fail for the intended missing behavior.
- [ ] Implement only the new options and v2 migration, preserving current serialized launch/role values and existing setting meanings.
- [ ] Rerun the fixture; inspect stored JSON to confirm codes/credentials are absent.

### Task 2: Share role preparation and NGO lifecycle

**Files:** `SimulatorConnection.cs`, `RoleSelectionUI.cs`; `SimulatorConnectionTests.cs`, existing `MultiplayerConnectionUITests.cs`, `MultiplayerRoleApprovalTests.cs`, `MultiplayerIntegrationTests.cs`.

**Interfaces:** `SimulatorConnection.Prepare(ClientRole role)` validates and installs the role before transport start; `Task StartDirectAsync(string address, ushort port, bool host, bool loopbackOnly, CancellationToken token)`; `Task StartRelayAsync(NetworkConfiguration configuration, CancellationToken token)`; idempotent `Task StopAsync()`. Preparation/start failures carry a user-safe reason to the caller. Preparation binds one attempt to one captured manager and rejects an active/stopping manager.

- [ ] Add failing tests for role-before-start order, Player and Spectator hosts/clients, duplicate start rejection, cancellation during NGO connection and repeated stop. Include a test using the pinned transport APIs that proves Direct configuration restores its protocol after Relay configuration.
- [ ] Run the affected unit fixtures and establish expected failures.
- [ ] Extract the shared non-visual lifecycle while preserving `RoleSelectionUI` public callbacks, disabled-control behavior, error reasons and menu-owned presentation suppression. Use Unity-safe async waits and detach callbacks at terminal completion.
- [ ] Run those fixtures plus the existing direct host/client integration scenarios. Verify there is still one spawned role object per approved client and no new UI prompt after menu entry.

### Task 3: Attempt-owned Online sessions

**Files:** `RelaySessionConnection.cs`, `IOnlineSessionService.cs`, `UnityOnlineSessionService.cs`; `RelaySessionConnectionTests.cs`.

**Interfaces:** Keep a narrow fakeable service seam: `Task EnsureReadyAsync()`, `Task<IOnlineSession> CreateAsync(int maxPlayers, INetworkHandler handler)`, `Task<IOnlineSession> JoinAsync(string code, INetworkHandler handler)`. The SDK adapter implements `IOnlineSession` with `string Code`, a terminal-change notification, and idempotent `Task CloseAsync()` that deletes if host and leaves if client. `RelaySessionConnection.ConnectAsync(MenuSessionOptions options, CancellationToken token)` owns the custom handler and calls the shared `SimulatorConnection`; `CloseAsync()` invalidates and closes that attempt. Define `IOnlineSession` alongside the service interface; do not fake all SDK internals.

- [ ] Use controllable fake service tasks to test auth failure, invalid/full/expired code, rate limiting, create/join failure, NGO approval rejection, duplicate clicks, cancel before/after allocation, timeout then late success, late exceptions, stop failure and repeated close.
- [ ] Add tests proving host close deletes, client close leaves, no host migration/restart is accepted, and an old attempt cannot stop a replacement manager. Direct/Solo must make zero calls to this seam.
- [ ] Run the new fixture and confirm failures before implementing the adapter/attempt owner.
- [ ] Implement lazy initialization/authentication, private session creation with the proposed capacity, DTLS options on both paths, and `.WithNetworkHandler(...)`. Preserve SDK lifecycle ownership for service resources; use only the shared component for NGO startup/stop.
- [ ] Implement bounded foreground cleanup plus observed late compensation; distinguish primary connection failure from cleanup failure. Avoid blind retries and logging codes, allocation data or tokens.
- [ ] Rerun the fixture. Review installed SDK behavior against cancellation and terminal-event ordering before integration; mocked tests alone cannot establish Relay connectivity.

### Task 4: Integrate progress, cancellation and safe scene return

**Files:** `MenuSessionCoordinator.cs`, `MenuTransitionView.cs`; `Assets/Code/Tests/Editor/MainMenuFlowTests.cs`, `MenuRecordingLifecycleTests.cs`; use fake Online service injection in tests.

**Interfaces:** Keep `MenuSessionCoordinator.Launch(MenuSessionOptions)` as the menu entry point. Coordinator chooses Direct or Online once, owns its attempt and exposes a read-only active-session snapshot: method, role, host flag, connection state and ephemeral join code. Publish snapshot changes for presentation without exposing SDK handles.

- [ ] Extend failing flow tests for Online Host/Join with both roles, one common startup, stage feedback, cancelled scene load, pending service cancellation, timeout, transport failure, host/session loss and Return to Menu.
- [ ] Cover Online → Direct → Online, cancellation followed by Solo, cloud cleanup still pending, stale terminal callbacks and menu re-entry without a second role prompt. Verify preserved recording finalization and motion-output disable order without enabling any hardware.
- [ ] Run focused flow/lifecycle fixtures through the existing safe EditMode runner and confirm expected failures.
- [ ] Replace direct calls from the coordinator to legacy role/start UI with shared preparation and the selected attempt owner. Implement the budgets and cleanup ordering in the design contract; expose safe progress and the read-only snapshot.
- [ ] Rerun focused fixtures and real direct transport integration. Confirm late completions leave no new scene roots, player objects, handlers or persistent managers.

### Task 5: Menu controls and persistent session information

**Files:** `MainMenuScreen.cs`, `MultiplayerSessionPanel.cs`, listed UI assets/builder; `MainMenuAssetTests.cs`, `MainMenuPageRouterTests.cs`, `MultiplayerSessionPanelTests.cs`.

**Interfaces:** Main menu builds launch options from the selected method and relevant fields; it must not unconditionally parse the hidden port. Session panel consumes the coordinator's read-only snapshot, supports Copy, and subscribes/unsubscribes with its own lifecycle. It issues no networking operations.

- [ ] Add failing tests for conditional fields, Online launch without a valid hidden port, offline method switching, first-control focus, locked repeated starts, code display/copy/clearing and no code controls in Solo/Direct. Retain dropdown hit-area/alignment assertions and existing settings-entry focus checks.
- [ ] Run affected menu/panel fixtures and confirm the missing behavior fails.
- [ ] Implement the controls and host notice; update only the affected serialized assets through supported Editor tooling. Keep common role/assistance/recording preferences intact, and update Help text for both connection methods.
- [ ] Rerun fixtures, then inspect desktop and seated/head-tracked menu rendering at representative resolutions. Check keyboard navigation, dropdowns, code readability, Copy feedback, Cancel and in-flight access for a spectator host as well as a pilot host.

### Task 6: Integrated verification, deployment notes and release decision

**Files:** Extend `MultiplayerIntegrationTests.cs` where useful; create `docs/hybrid-multiplayer-connections.md` as the operator/user guide during implementation. Record actual evidence in this plan or a linked task record.

- [ ] Confirm the pre-implementation reviews were completed. Before release, obtain independent review of the integrated diff with those same boundaries covered; resolve blockers with focused rechecks.
- [ ] Inspect/discover current affected fixtures rather than copying historical case counts. Run all affected options, menu, role, connection, recording and flow fixtures after integration. Record real XML results, source identity, cleanup and any omissions in a unique `TestResults/hybrid-connections-<run>/` directory.
- [ ] Produce a Windows player using the pinned Editor and current build scenes. Confirm build report and matching source identity, then run separate processes with distinct authentication profiles; keep one operator in control of Editor/player operations.
- [ ] Run the acceptance matrix below. Cloud-dependent tests require a linked test project/environment and internet; if unavailable, record them as pending rather than substituting fake/loopback results.
- [ ] Document project/environment setup, session capacity, outbound connectivity requirements using current Unity guidance, code sharing, LAN address/firewall troubleshooting, host exit semantics, cleanup limits, pricing links and rollback to Direct.
- [ ] Measure representative Online latency/bandwidth at the proposed capacity, including spectators and late joins; compare with LAN and check current account quotas before enabling Online in a release. Do not interpret an 8-participant configuration as proof of acceptable flight performance.
- [ ] Verify Git diff hygiene, `.meta` pairing, package pins, unchanged unrelated work, restored authoring state and absence of secrets. Commit only task-owned paths if the user authorizes a commit; do not push automatically.

## Acceptance matrix

| Scenario | Required result / evidence |
|---|---|
| Solo and LAN with internet disconnected | Startup succeeds without UGS/auth calls; existing streamed-content limits are identified separately |
| LAN host + remote pilot + spectator | Existing ownership, late join, disconnect/rejoin and camera/input isolation hold across processes |
| Online host + remote pilot + spectator | Join code works; selected roles are approved once; no repeated role prompt; host code remains accessible |
| Online spectator host | Host owns the session without taking pilot authority; remote pilot can fly and spectator views remain correct |
| Different home networks / NAT | Host/join succeeds without user port forwarding on tested networks; record actual topology and latency |
| Empty, mistyped, expired or full-session code | Clear recoverable feedback; no stuck controls, leaked role object or automatic Direct fallback |
| Cancel, timeout or double click at each stage | One terminal outcome; no late flight start; late session cleanup observed; Retry/LAN/Solo usable |
| Host exit, host crash, client internet loss | Remaining flight returns safely; no replacement simulation host; recordings finalized once where process remains alive |
| Return/relaunch and Direct ↔ Relay repeatedly | Correct transport, fresh code where hosting anew, no stale callbacks or duplicate persistent roots |
| Cloud outage, sign-in failure or quota failure | Online error is useful; Direct/Solo remain available; no account/service prerequisite added to those paths |
| Normal desktop + unavailable simulator hardware | Graceful operation without NOVA, HOTAS or HMD; no hardware commands during scene loading/closing |

Real HMD/HOTAS acceptance remains an equipment check if the changed presentation affects those flows. Physical NOVA operation is separate and requires explicit authorization. Local loopback, fake SDK tests and a successful build do not prove separate-machine Relay or hardware behavior.

## References and planning checks

Implementation must reconcile current documentation with the pinned package sources. Sources consulted 2026-10-20:

- [Initialize services and authenticate](https://docs.unity.com/en-us/mps-sdk/init-ugs).
- [Create a session](https://docs.unity.com/en-us/mps-sdk/create-session) and [join by code](https://docs.unity.com/en-us/mps-sdk/join-session).
- [Network connection management and custom handlers](https://docs.unity.com/en-us/mps-sdk/manage-session-network-connection). Installed 2.2.2 source confirms `INetworkHandler`, create/join handler overrides and `WithNetworkOptions`; installed NGO transport source confirms `SetConnectionData` resets direct protocol.
- [Host migration](https://docs.unity.com/en-us/mps-sdk/session-host-migration): service host selection is not a transfer of this simulator's NGO objects or flight state.
- [Unity Gaming Services pricing](https://unity.com/products/gaming-services/pricing); check current terms and the actual project's usage before release.

Planning completion means source/package inspection, document consistency, link/path checks and diff hygiene only. Every implementation checkbox above remains pending until authorized execution supplies its own evidence.
