# HeliSim Unity project instructions

## Project
- Unity 6 URP helicopter simulation, supporting desktop, VR/OpenXR, multiplayer spectators, camera dashboards, and telemetry.
- Primary language: C#.
- Prefer small, focused, reversible changes. Explain the intended approach before editing multiple systems.

## Unity safety
- Do not modify `.meta` files, `Library/`, `Temp/`, `Logs/`, `obj/`, generated solution files, or package lock files unless explicitly requested.
- Do not edit Unity scenes, prefabs, ScriptableObject assets, Input Action assets, ProjectSettings, or URP assets unless the task explicitly requires it.
- Never rename/move Unity assets without preserving their `.meta` file.
- Avoid adding packages or changing package versions unless explicitly requested.

## Code conventions
- Use clear C# names and keep MonoBehaviours focused.
- Use `[SerializeField] private` rather than public fields for Inspector configuration.
- Validate required Inspector references in `Awake` or `OnValidate` and produce useful errors.
- Avoid per-frame allocations, LINQ, repeated `GetComponent`, and expensive searches in `Update`, FixedUpdate, or networking hot paths.
- Prefer `TryGetComponent`, caching references, and explicit null handling.
- Use XML docs only for public APIs or non-obvious behaviour.

## Networking
- Netcode for GameObjects is used.
- Keep authority explicit: distinguish owner, server, and local player.
- Do not make physics/network-transform changes without explaining authority, prediction/interpolation implications, and host/client impact.
- Treat spectator playback, buffered transforms, and camera-only clients as separate from player-controlled helicopters.

## VR and rendering
- HeliSim uses OpenXR/Meta Quest and URP.
- Preserve VR rendering behaviour; do not add duplicate active cameras or render textures without considering GPU cost.
- Prefer profiling evidence before performance changes.
- Do not alter URP renderer features, XR settings, or quality settings without explicit confirmation.

## Verification
- After code changes, report affected files, likely Unity Inspector wiring, and how to test in Play Mode.
- When possible, add or update focused edit-mode tests for pure logic.
- Do not claim Unity compilation or runtime verification unless it was actually run.