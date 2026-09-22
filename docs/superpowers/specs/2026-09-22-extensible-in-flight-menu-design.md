# Extensible In-Flight Menu Design

## Intent

Replace the existing Escape-driven settings overlay with an extensible in-flight menu for the Unity 6 helicopter simulator. The menu must present a conventional left-side navigation rail and a dimmed, still-visible simulation behind it. It must make future pages inexpensive to add and preserve every currently exposed menu control.

The user has explicitly chosen a non-pausing menu: opening it must not change `Time.timeScale`, pause physics, halt Cesium streaming, or stop the time-trial timer. The project currently has no front/menu scene, so **Return to Menu** must be visible but deliberately perform no navigation; its handler records a source-code TODO for the future scene destination.

## Current-State Findings

- `Assets/Code/Scripts/UI/SettingMenuOpener.cs` toggles an entire settings object on Escape.
- `Assets/Code/Scripts/UI/MainMenu.cs` also reacts to Escape and changes `Time.timeScale`, creating competing ownership of the key.
- `Assets/Level/Prefabs/UI/Menu.prefab` is a Screen Space Overlay uGUI Canvas with one large, mixed hierarchy: flight controls, maps, scene selection, time controls, general preferences, and Cesium settings.
- `SettingsMenu` binds both general preferences and all Cesium fields; `PrefSettings` owns persistence and application of those values.
- Build Settings include `Multiplayer.unity` only; no menu destination exists.

## UX and Interaction

Opening the menu displays a full-screen dark scrim at approximately 55% opacity. The running game remains visible beneath it. A fixed, opaque charcoal left rail contains navigation, while a translucent right content area contains the active page. The existing uGUI/TMP styling is retained and normalised through shared button, heading, row, field, and destructive-action styles; no third-party or generated artwork is required.

The rail order is:

1. **Resume** — primary action; closes the overlay.
2. **Flight & Session** — existing assist, aircraft, timing, map/world, scene, journey, and VR-reset operations, grouped into titled sections.
3. **Controls** — control-scheme selection and a future-friendly control-reference page.
4. **Settings** — general runtime preferences such as volume and Nova popup behaviour.
5. **Cesium** — advanced streaming and culling preferences, clearly labelled as advanced.
6. **Return to Menu** — visually separated at the bottom; it does nothing except log a clear TODO until a destination scene is specified.

Escape opens the menu when it is closed and closes it when it is open. Resume closes it. Opening selects Resume for keyboard/UI navigation; closing restores the previous cursor lock and visibility state. The overlay uses the existing EventSystem and `InputSystemUIInputModule` for UI focus. It must not globally disable helicopter/XR input or alter simulation time, because the user explicitly requires live simulation behind the overlay. The implementation must ensure Escape itself has only one owner.

Controls in forms are accessible by mouse and keyboard navigation, show hover/selected/disabled feedback, and use TextMeshPro labels with readable contrast. Non-interactive images must not block raycasts. The overlay remains responsive from the existing CanvasScaler reference resolution (1920x1080); page content scrolls independently when it exceeds the available height.

## Architecture

`PauseMenuController` becomes the sole menu state owner. It owns overlay visibility, Escape, default selection, cursor state, page selection, and page lifecycle notifications. It exposes only high-level operations such as `Open`, `Close`, `Toggle`, and `SelectPage`.

Each selectable page implements a small `InFlightMenuPage` component. The component has an Inspector-facing page title, optional description, root content reference, and lifecycle methods to refresh visible values only when the page becomes active. The controller stores an ordered serialized list of page registrations (page component, navigation button, and visible label). It validates duplicate/missing registrations in the editor/runtime and activates exactly one content root at a time. Adding a future tab therefore requires a new page prefab/component and one registration entry—not a controller code change or a switch statement.

Page-specific behaviour stays out of `PauseMenuController`:

- Flight & Session invokes existing domain methods on `MainMenu` and `SceneTransition` through explicit Inspector references.
- Controls and Settings use focused presenters bound to `PrefSettings` rather than exposing `PrefSettings` fields directly in the menu controller.
- The Cesium page owns the existing Cesium form fields and invokes the existing persistence/application backend through a dedicated presenter.
- Return to Menu is a dedicated action component with an intentionally non-navigating handler and TODO.

`MainMenu` remains the legacy owner of helicopter/session domain actions during this migration, but loses its Escape/`Time.timeScale` handling. `SettingMenuOpener` is removed from the menu prefab and retired, so there is one owner for menu visibility. `PrefSettings` remains the persistence and runtime-application backend, but menu-binding references are split so it does not depend on a monolithic `SettingsMenu` object.

## Page Forms and Data Safety

Controls, Settings, and Cesium pages stage edits locally. When selected, a page reads current persisted/runtime values into fields. **Apply** validates every field, applies values through `PrefSettings`, saves using the existing persistence path, and then clears dirty state. **Revert** restores the last applied values. Switching pages preserves a draft; closing a dirty page prompts the user to Apply, Discard, or Cancel. Validation failures remain beside the affected field and never use direct `int.Parse`/`uint.Parse` exceptions as user feedback.

Current Cesium fields keep their backend ranges and types. Numeric values must be parsed with `TryParse`, bounded to the supported range where one exists, and rejected with an inline message when invalid. Missing Cesium or preferences references disable Apply and present an actionable diagnostic instead of throwing a null-reference exception.

## Existing-Control Migration

No existing menu action is intentionally removed.

| Existing control group | Destination |
| --- | --- |
| Assist preset, trim/torque/stabilisation, reset aircraft, start engine | Flight & Session |
| Time display, add/remove time, pause/resume/reset session | Flight & Session |
| Online/offline map, journey controls, scene selector, camera controls, VR reset | Flight & Session |
| Control scheme | Controls |
| Volume and Nova popup preference | Settings |
| Cesium screen-space error, streaming, caching, loading, and culling fields | Cesium |
| Apply Settings | Separate Apply action on Settings and Cesium, each scoped to its own fields |

## Unity Asset Structure

The current `Menu.prefab` is evolved in place to avoid breaking existing scene references. Its Canvas retains Screen Space Overlay, GraphicRaycaster, and CanvasScaler. Its new logical hierarchy is:

```text
Menu
├── Scrim
├── Overlay
│   ├── NavigationRail
│   │   ├── ResumeButton
│   │   ├── PageNavigation
│   │   └── ReturnToMenuButton
│   └── ContentPanel
│       ├── Header
│       └── PageViewport
│           ├── FlightSessionPage
│           ├── ControlsPage
│           ├── SettingsPage
│           └── CesiumPage
└── PauseMenuController
```

Page content that requires scrolling follows Unity's required ScrollRect arrangement: viewport/mask, content root, and a vertical layout group only on the content root. Layout groups and anchors, not manually coordinated positions, control responsive sizing.

## Error Handling

- A missing page registration is reported once with the page/button name and skipped without preventing the menu from opening.
- A duplicate page registration is rejected and logged with both registrations.
- The controller reports a missing EventSystem/GraphicRaycaster clearly; it does not silently present non-interactive UI.
- Invalid numeric settings leave the prior applied runtime value unchanged.
- The return action never attempts to load an invented scene name.

## Testing and Acceptance Criteria

Focused EditMode tests cover controller state transitions, duplicate/missing page validation, selection/default-page behaviour, Return to Menu's non-navigation contract, and form parsing/range validation. Tests also prove invalid Cesium input does not change `PrefSettings` values and valid input does.

Manual Unity verification in `Multiplayer.unity` (and any scene that instantiates `Menu.prefab`) covers:

1. Escape opens/closes one overlay with no competing menu state.
2. Physics, Cesium updates, and the timer continue while the overlay is open; `Time.timeScale` is unchanged.
3. The game remains visible through the scrim, and the rail/content layout is readable at 16:9 and a narrower resolution.
4. Keyboard/pointer navigation, buttons, fields, toggles, dropdowns, scrolling, and first selection work through the EventSystem.
5. Every migrated action retains its prior observable effect.
6. Apply/Revert and dirty-close confirmation preserve or discard settings exactly as selected.
7. Return to Menu emits only its documented TODO and leaves the active scene untouched.
