# Multiplayer implementation record — 2026-10-20

Request: implement `MULTIPLAYER_SYSTEM_CHANGES.md` in the existing checkout.
Baseline: `6a948a9`; unrelated untracked `docs/controls-debugging.md` is preserved.

The plan's historical completion claims are unsupported: `fb03c9b` committed only
the document and the current scripts still contain the described defects. No old
test counts, build reports or implementation are treated as proof.

Risk: high (NGO role/spawn/disconnect, remote simulation isolation, shared camera
lifecycle). Independent pre-review covered authority, missing hardware and local
spectator UX. Main owns integration, cameras/data, serialized assets and all Unity
operations. Separate writers own connection UI and approval/spawn, each with tests.

Plan: (1) endpoint/retry UI, (2) validated role/spawn lifecycle, (3) local helicopter
registry and scene-owned follower, (4) dependent cameras/data cleanup, (5) prefab
registration/wiring audit, (6) focused tests, transport integration, player build,
independent final review. Preserve dynamics, transform authority, NOVA mappings,
VR hierarchy and telemetry units. No physical platform operation or commit.

Ruling: reuse the current checkout — project instructions prefer reuse and its
matching Editor is open; a new worktree would duplicate the large Unity import.
Ruling: implement the documented requirements afresh — no runtime implementation
exists in the named commit; historic checkbox claims will be replaced with actual
evidence. Empty role payloads retain the documented legacy Player default.

Verification operator: main only. Connected MCP Editor: this exact project,
6000.4.5f1, idle, no import/compile, clean Multiplayer scene at initial inspection.
CLI pipeline discovery reports no instances, but MCP is connected; all serialized
changes use supported Editor APIs. Tests use EditMode discovery including fixtures
that explicitly enter/exit Play mode. Evidence goes under unique `TestResults/`.

Pending: full rendered multi-view/flight scene acceptance.
Separate-machine LAN/firewall, physical NOVA, seated HMD and HOTAS acceptance remain
equipment checks and cannot be represented by local loopback tests.

## Integration and review findings

Implemented Tasks 1–5: one endpoint/startup path, strict role approval and manual
spawn, explicit prefab role components, local registry and scene-owned follower,
camera/data refresh and destruction cleanup, serialized remote flight-controller
disable, and removal of only the legacy prefab's network registration. The legacy
prefab asset/GUID remain. A full Assets/ProjectSettings reference search found no
other legacy GUID consumer. Three guarded NUnit fixtures and their original metas
were moved from `Tests/EditMode` into the predefined Editor assembly.

Independent final review found no blocking runtime/asset defect. Originally deferred minor
(fixed in the authorized follow-up below):
manually disabling/re-enabling `CameraTargetPovSwitcher` after selecting a spectator
target needs Tab or a registry change to resume selection. No current production
flow toggles it. This does not affect the tested shutdown/restart path.

Initial test results are retained as diagnostics, not green proof. One run used an
old loaded assembly during a registry compile error. A valid expanded run passed
all 35 multiplayer unit cases but the integration precondition rejected Unity Test
Runner's own transient scene. The runner now checks clean saved authoring scenes
and snapshots their setup before starting; fixtures isolate Play mode and restore
that setup. EditMode camera callbacks use reflection because Unity disallows
runtime `SendMessage` dispatch there. Telemetry destruction tests explicitly invoke
the Netcode callback for the same EditMode lifecycle reason; they do not claim a
real Play-mode destruction event.

`integration-03.xml`: both real host/client socket scenarios passed; the real JoinUI
fixture caught a missing retry hint on disconnect with a transport reason. The UI
now preserves that reason and appends a clear retry hint. Full project regression
selection is 185 discovered cases across 18 project fixtures; 105 third-party
Mapbox cases are excluded. Source hashes and discovery/selection are retained with
the XML under `TestResults/multiplayer-20261020/`.

## Initial regression result

`project-suite-01.xml`: **180/185 passed, 5 failed, 0 skipped**. All **38 multiplayer
cases passed**, including the 3 real transport/JoinUI Play-mode scenarios. Original
Multiplayer scene restored cleanly; fixture-created assets and owned managers were
removed. Unity was idle, outside Play mode, targeting StandaloneWindows64 afterward.

Independent diagnosis checked baseline Git blob identities. Four failures have
pre-existing source/fixture causes; the fifth remains an Input System setup issue
with no identified multiplayer link. This is not a claim that a baseline suite ran:

- Controls directional composite: legacy joystick yaw group filtering in unchanged
  `InputRebindingService` hides a direction (production controls defect).
- Map tracker: unchanged relocated EditMode fixture relies on runtime `Awake` to
  initialize its anchor.
- Settings toggle: unchanged EditMode fixture relies on runtime `Awake/OnEnable`
  before checking image colors.
- Waypoint lap average: unchanged relocated fixture reflects a public field with
  `NonPublic` flags only and fails before lap logic executes.
- Input device tabs: `AddDevice<Gamepad>()` cannot construct its Dpad child; failure
  occurs before the production assertion. Exact layout-state cause is unresolved.

These unrelated issues remain visible; assertions were not weakened and controls,
settings, map and lap implementation were not changed to get a green broad suite.

## Build and scene observation

Windows development player: **Succeeded, 0 errors, 4 warnings**, Unity 6000.4.5f1,
StandaloneWindows64, scene `Assets/Level/Scenes/Multiplayer.unity`, output
`TestResults/multiplayer-20261020/windows-build-01/HeliSim.exe` (701,044,375 bytes).
The Editor API request timed out after 300 seconds during shader work; it did not
cancel the build. The subsequently written `summary.txt` and `messages.txt` confirm
the terminal result, and an Editor query confirmed idle state before further work.
Warnings concern missing OpenXR interaction profiles, optional OpenXR input control
migrations, and missing Pipeline runtime configuration. Package/settings pins were
unchanged. `source-identity-final.txt` records the tested/built task sources and pins.

Disconnected `Multiplayer` startup was observed in Play mode at 1468×826: editable
address, role/host/client controls and status text render without overlap. No network
session was started, zero NOVA instances existed, and no Console errors were reported
during this observation. Screenshot: `TestResults/multiplayer-20261020/Multiplayer-startup.png`.
Play mode was exited. An earlier static preview attempt failed to render the canvas
and produced a preview RenderTexture cleanup error; it is not accepted as visual proof.

The performance-test package's temporary build resources were cleaned by its normal
post-build hook. No source edits followed the passing multiplayer tests/build;
documentation now reflects actual evidence. No commit, push, physical motion, or
separate-machine networking was performed. Full R66 flight/remote rendering, all
dashboard camera modes and seated OpenXR, HOTAS and physical NOVA acceptance remain
pending. The implementation is not presented as full equipment/system acceptance.

Final cleanup confirmed the Editor idle, outside Play mode, with the original clean
Multiplayer scene, one scene NetworkManager, and zero NOVA instances. An incidental
TMP fallback-font serialization change from the build/preview was restored from its
exact baseline blob through Editor reimport; its original meta/GUID was untouched.
Source/document whitespace checks pass. Unity-generated empty serialized fields
retain the Editor's normal whitespace. Unrelated `docs/controls-debugging.md` remains
untouched, and no package or project-version pin changed.

## Authorized follow-up: six remaining issues

The user requested fixes for the five full-suite failures and spectator re-enable,
followed by fresh tests. Existing dirty multiplayer changes and the unrelated
controls-debugging document remain preserved. Main is the sole Editor/test/build
operator; disjoint writers handle yaw row presentation and the three fixture fixes.
Independent pre-review covered input mode compatibility and spectator selection.

Plan: reproduce the five cases plus a real disable/re-enable spectator scenario;
repair fixture initialization/reflection, legacy yaw filtering/labels, and the
selection-cache lifecycle; resolve the input test's device setup from evidence;
review the combined changes, run all project fixtures, and refresh the player build
if runtime source changes. Keep all existing behavioral assertions, input masks,
network authority, XR feature configuration, and hardware output unchanged.

Dpad diagnosis: live `InputSystem.LoadLayout("Dpad")` resolves to OpenXR's
`DPadInteraction.DPad`, a device derived from XRController, rather than DpadControl.
The pinned OpenXR Editor registration registers every interaction layout, even the
disabled D-Pad feature; its case-insensitive name collides with the built-in control.
The device-tab predicate only classifies device types and does not require device
registration. Its unit test can exercise real device classes without constructing
their globally registered layouts. No XR feature or package modification is needed.

Fresh pre-fix reproduction: `six-fixes-20261020/red-01.xml` ran 18 cases, with 11
passing and 7 failing: the original five, the additional legacy-yaw Axis case, and
the real spectator disable/re-enable case. The grouped yaw-mode preservation cases
already passed. Two virtual devices created before the old Gamepad fixture threw
were identified against a pre-run device-ID snapshot and removed; existing devices
and global layout registrations were preserved.

Integrated fixes: invalidate the spectator selection cache on enable; only apply
yaw-mode filtering to actions with mode groups and only label explicit yaw-button
parts Left/Right; explicitly initialize/clean up EditMode component lifecycle;
search both public/private reflected waypoint fields with readable diagnostics;
test device classification using unregistered Keyboard, Mouse and Gamepad instances,
retaining all original classification assertions. Legacy composite row assignment
uses individual binding APIs, so unrelated direct-binding grouping logic is unchanged.

Independent final delta review found no blockers. The first affected-fixture run
passed 48/49 cases. Its remaining map assertion revealed a second EditMode setup
issue: assigning Rigidbody.position does not update Transform until a physics tick.
A live isolated diagnostic confirmed this. The fixture now sets the transform and
explicitly synchronizes physics, asserting the body reaches its intended nonzero
pose before calling the tracker. Native rotation synchronization also changes the
quaternion's floating-point representation; the fixture uses Unity's quaternion
equality comparer instead of requiring bit-identical components. Position,
rotation, successful sync and valid latitude assertions remain.

Final follow-up test result: `TestResults/six-fixes-20261020/project-suite-final.xml`
reports **188/188 passed, 0 failed, 0 skipped**, including all 38 multiplayer cases
and the three real transport/JoinUI scenarios. The count increased by three yaw
presentation cases; the existing spectator fixture now additionally checks re-enable.
`map-green-03.xml` also confirms the corrected map fixture passes. All six requested
issues are resolved; the earlier five failures and deferred camera issue above are
historical evidence, not outstanding items. Discovery, the explicit fixture selection,
source hashes and every failed/green attempt are retained under the follow-up folder.
No global input layout was overwritten, no assertion was removed, and no package,
XR feature, hardware mapping, simulation authority or telemetry schema was changed.

Current-source Windows development build:
`TestResults/six-fixes-20261020/windows-build-final/summary.txt` reports **Succeeded,
0 errors, 45 warnings**, 701,044,383 bytes, Unity 6000.4.5f1. The warning list comprises
the four existing OpenXR/Pipeline configuration warnings and 41 compiler warnings
about deprecated APIs, redundant `new` keywords and unused fields in code unchanged
by this follow-up. The complete messages are retained alongside the build report.
The executable was not auto-run. The build package cleaned its temporary resources;
incidental TMP fallback-font serialization was restored to the exact baseline and
reimported without altering its meta. The Editor is idle outside Play mode with the
original clean Multiplayer scene and zero NOVA components. No commit or push was made.

The OpenXR package's Editor-only Dpad layout collision remains package behavior;
the repaired device classification test no longer depends on that unrelated layout
registration. Real gamepad/VR device integration is not claimed by this pure type
classification test. All previously recorded LAN and hardware acceptance limits
remain, without affecting the now-green project regression suite.
