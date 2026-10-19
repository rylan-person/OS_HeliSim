# Repository Guidelines

## Start here

- Classify the request before acting: implementation, review, research/planning,
  documentation, or lookup. A review alone does not authorize fixes or Unity runs.
- Inspect applicable instructions, Git status/diff, and affected source/assets;
  preserve existing dirty and staged work. Plans describe intent, not proof of behavior.
- For substantial work, read [the workflow](docs/AGENT_WORKFLOW.md) and relevant
  current plans. Read only affected sections; consult history when needed.
- Before selecting checks or operating Unity, read [testing and Editor safety](docs/TESTING.md).
  Use the Editor and package versions pinned in `ProjectSettings/ProjectVersion.txt`,
  `Packages/manifest.json`, and `Packages/packages-lock.json`; do not silently upgrade.
- Keep small tasks proportionate. Documentation-only changes need document checks,
  not a Unity build. Finish authorized work and ask only for material unresolved intent
  or required permission; do not add routine confirmation gates.

## Multi-agent workflow

For large implementation tasks, act as the lead developer. Use
[the risk and review policy](docs/AGENT_WORKFLOW.md#risk-and-review) to select
independent perspectives before implementation and against the integrated result.

- First inspect the relevant architecture and create a plan.
- Break implementation into small, well-scoped tasks.
- Delegate independent implementation work to subagents whenever practical.
- Prefer parallel subagents when tasks do not depend on one another.
- Assign explicit file ownership; never give concurrent writers shared files.
- Reviewers are read-only.
- Keep Unity/test/build operations under one operator at a time, including manual runs.
- Keep architectural decisions, difficult debugging, and integration work in the main agent.
- Review all subagent changes before considering the task complete.
- Run the affected tests and applicable build/scene checks after integrating changes.
  Reviews do not replace automated proof, human acceptance, or hardware verification.

### Sub-agent model selection

When delegating work to sub-agents:

- Use `gpt-6.1-sol` with medium reasoning for complex tasks requiring substantial reasoning, including:
  - architecture and design;
  - difficult debugging;
  - multi-file implementation;
  - networking or multiplayer;
  - VR/XR;
  - physics;
  - Eight360 NOVA integration;
  - code review where correctness is important.

- Use `gpt-6-luna` for simple, isolated, one-off tasks, including:
  - locating relevant files or code;
  - straightforward investigation;
  - simple mechanical edits;
  - documentation changes;
  - small tasks whose output will be reviewed by the lead agent.

- Set model and reasoning explicitly when delegating; use medium reasoning for
  simple tasks too unless the task warrants another supported setting. Use clean
  context with a bounded handoff. Keep the lead's user-selected model/settings.
- When uncertain whether a task is simple or complex, use `gpt-6.1-sol` with medium reasoning.
- If a selected model is unavailable, report the limitation before substituting.

- Do not use `gpt-6-terra` or `gpt-6-astra` for sub-agents unless explicitly instructed otherwise.

## Project Information

HeliSim is a Unity 6 helicopter simulation application designed for use with the
Eight360 NOVA motion simulator, VR headsets, physical flight controls, and
conventional desktop/spectator displays.

The application is used both as a standalone helicopter simulator and as a
hardware-integrated simulation system. Changes should therefore preserve clear
separation between helicopter simulation logic, presentation, physical input,
VR, networking, data recording, and motion-platform integration.

### Eight360 NOVA Motion Platform

The Eight360 NOVA is an untethered motion platform capable of continuous 360°
rotation in roll, pitch, and yaw. HeliSim can drive the platform so that its
orientation follows or represents the simulated helicopter's motion.

Treat motion-platform code as hardware-integration and safety-sensitive code.

When modifying NOVA/E360 functionality:

- Do not assume the motion platform is connected or available.
- The simulator must remain usable without the NOVA connected.
- Hardware communication failures should fail safely and should not break the
  rest of the simulator.
- Avoid tightly coupling helicopter physics directly to hardware communication.
  Prefer a clear interface between simulation state and motion-platform output.
- Do not introduce blocking hardware/network operations on Unity's main thread.
- Preserve existing connection, disconnect, reset, stop, and emergency/failsafe
  behavior unless the task explicitly requires changing it.
- Avoid sending motion commands during editor startup, scene loading, shutdown,
  or other states where the simulator is not actively running.
- Be conservative when changing scaling, coordinate conversions, axis mappings,
  limits, offsets, or motion-command rates. These changes can result in
  significant real-world platform movement.
- Do not remove clamps, validation, limits, failsafes, or connection checks
  without understanding why they exist.
- Keep motion-platform state separate from simulated helicopter state. The
  helicopter simulation should remain authoritative; hardware feedback should
  not silently alter flight physics unless explicitly designed to do so.
- Where practical, make hardware-dependent logic testable without requiring the
  physical NOVA.

Pay particular attention to coordinate systems. Unity, helicopter flight
dynamics, VR tracking, and the NOVA interface may use different axis conventions,
rotation order, handedness, units, or reference frames. Do not change rotation
conversion code without tracing the complete transformation path.

### Helicopter Simulation

The helicopter is a simulation system rather than a conventional character or
vehicle controller. Avoid simplifying or replacing flight-dynamics behavior
unless specifically requested.

Keep these concerns separate where practical:

- helicopter/flight dynamics
- player control input
- assistance/control systems
- environment and disturbances
- visual representation
- camera systems
- motion-platform output
- networking
- telemetry/data recording

Do not use visual transforms as authoritative physics state when an existing
flight-dynamics or Rigidbody state is available.

### VR and OpenXR

The project supports seated VR using OpenXR, including Meta Quest headsets via
PC VR runtimes. VR may be used primarily for viewing rather than motion-controller
interaction.

When changing VR functionality:

- Do not assume hand controllers are present.
- Preserve seated/head-tracked use cases.
- Keep VR recenter/reset functionality working.
- Avoid coupling gameplay functionality exclusively to XR input.
- Consider both HMD rendering and desktop/spectator rendering.
- Test changes against the project's configured OpenXR setup rather than adding
  dependencies on a specific vendor SDK unless required.
- Be cautious when changing camera hierarchy or transforms because XR tracking
  may modify camera transforms at runtime.

### Physical Controls

The simulator may use physical flight controls such as the Thrustmaster HOTAS
Warthog joystick and throttle through Unity's Input System.

Input code should:

- tolerate devices being disconnected or unavailable;
- avoid depending unnecessarily on transient Unity device IDs;
- prefer stable device characteristics, layouts, bindings, or capabilities;
- preserve keyboard/test input where available so development does not require
  simulator hardware.

Do not assume every developer or CI environment has the physical controls
connected.

### Multiplayer and Spectator Modes

HeliSim uses Netcode for GameObjects and supports different client roles,
including player and spectator behavior.

Networked helicopter state, locally controlled state, cameras, and spectator
presentation should remain clearly separated. When modifying networking:

- distinguish ownership from local-player state;
- do not enable player cameras, input, or UI for remote objects;
- do not make spectator clients authoritative over helicopter simulation;
- consider host behavior separately from remote-client behavior;
- avoid assuming every connected client owns a helicopter;
- preserve graceful behavior when clients connect, disconnect, or join late.

### Cameras and Dashboard

The application supports multiple simultaneous camera and information views,
including FPV, follow, cinematic, orbit, tactical/top-down, and data panels.
Dashboard panels may be dynamically reassigned and individual views may be
expanded to full screen.

Camera changes should avoid unnecessary rendering cost. A camera that is not
currently needed should not continue rendering expensive views or RenderTextures
without a reason.

Do not assume `Camera.main` uniquely represents the user's active view.

### Telemetry and Recording

HeliSim can record simulator and flight data for later analysis. Recorded data
may be used for research, debugging, or performance analysis.

When modifying recorded fields or logging:

- preserve existing field meanings and units where possible;
- avoid silently changing the interpretation of an existing column;
- prefer adding a new field over repurposing an existing field;
- keep timestamps and sampling behavior consistent;
- avoid introducing expensive per-frame allocations or excessive logging;
- document changes to recorded schemas.

Simulation frame rate may vary, so calculations that depend on time should use
timestamps or `deltaTime` rather than assuming a fixed update frequency.

### Hardware-Independent Development

Most development should remain possible on a normal workstation without the
NOVA, VR headset, HOTAS, or multiple network clients connected.

When implementing hardware-facing functionality, provide graceful fallbacks or
clear disabled states rather than allowing missing hardware to cause exceptions.

Before changing a subsystem, identify whether it is:

1. simulation logic,
2. hardware integration,
3. presentation/UI,
4. networking, or
5. telemetry/data recording.

Avoid crossing these boundaries unnecessarily.

## Project Structure & Module Organization

This is a Unity 6 VR helicopter simulator. Runtime code lives in
`Assets/Code/Scripts/`, organized by feature (for example, `VR/`, `UI/`,
`Multiplayer/`, `helicopter/`, `Camera/`, and `TimeTrial/`). Primary scenes
are `Assets/Level/Scenes/HeliTesting.unity`, `MelbTesting.unity`, and
`Multiplayer.unity`. Keep project-owned art, prefabs, and scenes in `Assets/`;
third-party content is generally under `Assets/Packages/` and samples under
`Assets/Samples/`. Unity dependencies are declared in `Packages/manifest.json`;
editor and runtime settings are in `ProjectSettings/`.

## Build, Test, and Development Commands

Open the repository in Unity Hub using the version pinned in
`ProjectSettings/ProjectVersion.txt` (currently `6000.4.5f1`). Let Unity restore
packages, then open the relevant scene and use Play mode for local verification.
Build from **File > Build Profiles** after selecting the target and scenes.

Run automated tests from **Window > General > Test Runner**. For CI or a local
headless run, use the pinned Unity executable only when this project is not
already open in another Editor. Use a unique result/log directory and validate
the actual XML results; process exit or tool success alone is not a test pass.
See [TESTING.md](docs/TESTING.md) for the command and selection/recovery rules.

Project tests live under `Assets/Code/Tests/Editor/` in the predefined Editor
assembly. The multiplayer integration fixtures enter and exit Play mode from the
EditMode runner and restore the original scene setup. Keep NUnit test code out of
the runtime assembly so it is not included in player builds. Add focused tests
with new behavior rather than relying only on manual checks.

## Coding Style & Naming Conventions

Write C# consistent with nearby scripts: four-space indentation, Allman braces,
PascalCase for types, public members, and methods, and camelCase for private
fields and parameters. Name each `MonoBehaviour` file after its class (for
example, `FlightDynamicsControl.cs`). Keep Unity serialized references explicit
and validate scene-dependent changes in the owning scene. No formatter or linter
configuration is committed, so do not introduce wholesale style churn.

## Testing & Asset Changes

Exercise the affected scene in Play mode, including VR/OpenXR, input, networking,
or camera flows when relevant. Check the Console for errors and warnings. Commit
every Unity asset together with its `.meta` file; never manually regenerate GUIDs
or commit generated `Library/`, `Temp/`, or `Logs/` content.

Prefer supported Editor/Unity tooling for serialized scenes, prefabs and assets;
edit code and documentation normally. Preserve unsaved authoring, serialized field
compatibility and GUIDs. Asset/settings changes needed for the authorized task are
in scope; do not expand into unrelated scene rebuilding, rebaking or package changes.
New assets need their Editor-generated `.meta` files. Never enable NOVA output to
verify an unrelated change; hardware acceptance remains separate and explicit.

## Commits & Pull Requests

Recent commits use short, imperative, descriptive subjects such as `Adds OpenXR
Recentering` and `Fix critical robustness/safety issues in Camera...`. Keep each
commit focused and explain the affected system. Pull requests should summarize
the change, list verification steps and affected scenes, link related issues, and
include screenshots or a short recording for visible UI, scene, or VR changes.

Do not import commit authorization from another project's workflow. Commit only
when requested or authorized in this project's session; inspect and stage only
task-owned paths/hunks. Never automatically push, amend, blanket-stage, or include
unrelated edits. Report actual checks, omissions and pending human/hardware acceptance,
then stop when the authorized scope and required proof are satisfied.
