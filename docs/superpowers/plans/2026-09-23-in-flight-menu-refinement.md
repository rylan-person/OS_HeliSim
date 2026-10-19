# In-Flight Menu Refinement Plan

**Goal:** Refine the shipped in-flight menu into a clearer simulator-standard interface, add configurable input bindings, and expand safe settings coverage.

**Scope boundary:** Implement only after the current menu migration, keyboard-navigation fix, and Unity Play-mode verification are complete. Keep the overlay non-pausing; do not alter helicopter physics, NOVA safety limits, or Cesium runtime behavior except through validated settings.

## Flight & Session

- Remove the control-scheme selector, camera-view selector, and Location selector from this page. Location belongs in the main menu; camera view is not a menu setting.
- Keep aircraft/session actions grouped into concise sections: **Aircraft**, **Assists**, **Time Trial**, **World & Map**, **Journey**, and **VR**.
- Place Auto Trim, torque mode/assist, roll-rate limiter, and stabilization assist under one collapsed **Advanced Control Settings** disclosure. Preserve their existing domain targets and defaults.
- Add a subtitles toggle and a subtitle scale option. Persist these as presentation preferences; subtitle availability must gracefully disable when no subtitle source exists.

## Controls

Replace the Keyboard/Mouse-versus-HOTAS switch with an Input System rebinding screen.

- Show a row per action: cyclic pitch, cyclic roll, collective/throttle, pedals/yaw, trim, engine start, reset, pause/menu, map, VR recenter, and camera controls.
- Each row shows its current primary binding and provides **Rebind**, **Clear**, and **Reset to Default** actions.
- Rebind supports button and axis/composite bindings, rejects unsupported control types with inline feedback, and never requires a HOTAS, pedals, HMD, or NOVA to be connected.
- Persist binding overrides using the Input System override JSON path; load them on startup and allow profile reset. Maintain keyboard fallback bindings.
- Keep a read-only hardware profile summary, but do not use a device-ID-based scheme selector.

## Settings

Add common helicopter-simulator settings, grouped and staged with Apply/Revert:

| Group | Settings |
| --- | --- |
| Audio | Master, engine, environment, UI, and radio/intercom volumes; mute; subtitles and subtitle scale. |
| Display | Fullscreen/window mode, resolution, VSync, frame-rate cap, quality preset, brightness, UI scale, colour-blind mode, and field-of-view where the active camera supports it. |
| VR | Enable VR when available, render scale, refresh-rate choice when exposed by runtime, seated height/offset, recenter action, comfort vignette, and spectator-display enablement. |
| Simulation assistance | Assistance preset, auto trim, stabilization, roll-rate limiter, torque assistance, control sensitivity, response curves, and optional dead zones. These change simulation assistance only through existing authoritative systems. |
| Gameplay/session | Units (metric/imperial), time-trial hints, subtitles, confirmation prompts, and autosave/recording preferences. |
| Accessibility | UI scale, high contrast, text scale, colour-blind palette, reduced motion, subtitle options, and remappable controls. |
| Hardware integration | NOVA popup behavior, safe connection/status display, and non-blocking diagnostics only. Do not add live motion scaling, limits, or emergency behavior to this page without a separate safety-reviewed design. |

Every setting must have a defined owner, persistence key/schema, valid range, default, and hardware-unavailable fallback before implementation.

## Cesium

- Change **Maximum Simultaneous Tile Loads** and **Loading Descendant Limit** to sliders with inclusive range **0–1000**.
- Update `PrefSettings` validation, presenter validation, slider labels, and EditMode boundary tests together.
- Keep invalid-input protection and Apply/Revert semantics; document that zero is accepted only if Cesium’s runtime API supports it, otherwise display an inline minimum-value diagnostic.

## Implementation Order

1. Confirm the current menu migration passes Play-mode, navigation, and test verification.
2. Simplify Flight & Session and add subtitle preferences with focused presenter tests.
3. Build the Input System rebinding presenter, persistence adapter, and disconnected-device test coverage.
4. Add Settings groups one at a time, beginning with audio/display/accessibility; use existing subsystem owners rather than a monolithic settings controller.
5. Apply Cesium slider/range changes with parser and backend boundary tests.
6. Verify at desktop and narrow resolutions, with VR/HOTAS/NOVA absent; then manually validate connected hardware separately where available.

## Acceptance Criteria

- Flight & Session contains no control-scheme, camera-view, or location selector.
- Advanced flight assists are discoverable but collapsed by default.
- Every listed input action can be rebound, cleared, reset, persisted, and used without connected flight hardware.
- Settings remain safe and usable with VR, NOVA, or physical controls unavailable.
- Both revised Cesium sliders accept their documented range and never mutate applied values on invalid input.
