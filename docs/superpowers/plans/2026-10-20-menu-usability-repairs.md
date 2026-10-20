# Menu usability repairs — 2026-10-20

User reports arrow-only dropdown hits, displaced option lists, settings entering
at the bottom, and a second role prompt after Host/Join from the main menu.

## Investigation and scope

Live inspection in the saved MainMenu scene reproduced disabled dropdown Images,
right-edge fixed-width templates, and Controls entry selecting ResetProfile with
scroll position 0.113. Existing scroll reset is overridden by focus reveal.
Independent role-path review confirmed options already propagate correctly:
JoinUI's opaque, raycastable Canvas order 254 covers transition order 200 while
loading/connecting, until successful connection hides it.

Fix presentation and initial focus; preserve dirty-setting confirmation, keyboard
focus reveal, role authority/spawning, direct gameplay-scene entry and retry.
No physics, NOVA, XR tracking, map package or telemetry changes. Existing Mapbox
repairs, recovery assets and controls-debugging document remain untouched.

Root owns dropdown prefab configuration, builder, focus and UI tests. Independent
role specialist owns coordinator/role/transition presentation and focused tests;
root retains sole Unity/test/build operation ownership. Medium presentation risk;
technical review must verify role authority is unchanged and legacy fallback works.

## Steps and acceptance

1. Add failing asset and runtime checks for full-field raycasts, popup alignment,
   top-first settings focus, and menu-owned role prompt suppression.
2. Repair existing dropdown hierarchies in Menu and MainMenu through Editor APIs;
   update builder so regenerated assets retain corrections. Set main-menu settings
   focus to the first editable control while preserving in-flight behavior.
3. Explicitly assign menu startup presentation ownership before the connection
   wait, keep legacy role scripts active, and render transition above gameplay UI.
4. Run focused tests, independently review integrated result, then applicable
   full EditMode suite and Windows build. Render and inspect dropdown/settings
   views; safe role fixtures substitute harmless prefabs/inactive world, never
   launch NOVA output. Keep actual hardware acceptance separate.

Evidence: unique files in `TestResults/menu-usability-20261020/`. Confirm discovery,
actual XML, scene restoration, XR preloads and temporary fixture cleanup. Preserve
the user's saved scene, generate new metadata via Editor, and leave fixes uncommitted.

## Results

- Observed six expected RED failures in `regression-red.xml`: both prefabs lacked
  full-field graphics and correct template anchors, Controls entered at scroll
  0.11346, and menu startup left the legacy role Canvas visible. An initial test
  compile used unavailable `Assert.Multiple`; replaced it with ordinary assertions
  for the pinned NUnit version before collecting RED evidence.
- Repaired existing prefabs via `MenuSceneBuilder.RepairUsabilityAssets`, without
  rebuilding either hierarchy. Enabled field graphics, aligned field-width popup
  templates below the field, and applied left-aligned captions. The builder also
  applies this configuration to newly generated dropdowns and cloned pages.
- Main-menu settings registrations now leave initial focus to the router, which
  resolves the first active/interactable control after page initialization. The
  Controls page selects JoystickTab at scroll 0.9999992; in-flight initial focus
  and explicit focus for ordinary main-menu pages remain unchanged.
- Added explicit menu ownership of role-panel visibility at scene load, retained
  startup scripts, and raised transition sorting order to 1000 above JoinUI's 254.
  Connection role/payload, validation, spawning and authority logic are unchanged.
  Failure presentation remains hidden for menu startup; direct scene entry retains
  its role panel and retry behavior.
- Independent UI and role-path pre/post reviews found no remaining blockers.
  Focused menu/router/flow/legacy integration run: **33/33 passed**, zero skips.
- Rendered acceptance caught a further inherited popup background defect. Two
  added opacity assertions failed in `opacity-red.xml` (seven passed, two failed).
  The viewport Image was disabled and its Mask hid the graphic; the inherited
  UIMask texture's center alpha measured 0.051 even after enabling them. Solid
  Simple images with no sprite, enabled mask graphic and opaque panel color fixed
  the actual rendering. Reviewed screenshot `opaque-popup-proof.png` confirms
  the options obscure underlying text. This configuration is persisted in both
  prefabs and covered by the final asset assertions.
- Live GraphicRaycaster checks hit Role at 10%, 50% and 95% of the field. Center
  pointer dispatch opened options; field and popup x bounds both measured
  317.3501–1410.648 pixels at 1468×826. Quality's six-option popup matched field
  bounds 325.7617–1409.883 and remained inside the screen (y 18.478–202.006).
  Controls screenshot `after-controls-top.png` shows the title, tabs and first
  bindings at page entry. Automated direct activation/Show in a single Editor
  command ran before TMP Start/render initialization; repeating after a frame
  verified normal interaction. No production lifecycle patch was needed.
- Editor prefab updates produced menu-scene TMP/layout serialization overrides.
  Removed incidental font/cache overrides via PrefabUtility; remaining saved
  scroll/scrollbar state is Editor-generated. Source/document whitespace checks
  pass; global diff check reports Unity's emitted empty-scalar trailing spaces
  in Menu.prefab, retained rather than hand-editing serialized YAML.
- Final integrated run: **376/376 passed, zero failed or skipped** in
  `full-editmode.xml`. Discovered and executed names match after normalizing
  Unity's generated suffixes for colliding byte-array display names; all 376
  result IDs are unique. `selection-validation.json` records the comparison.
  All 32 frozen code/asset/data hashes remained unchanged through verification.
- Restored original two XR preloaded assets after the test runner and persisted
  through Save Project. No settings, XR, font or network-prefab diff remained.
  Incidental Cesium ion Editor connection cancellation logs occurred on domain
  reload; they did not fail the actual NUnit run and no Cesium changes were made.
- Windows player build **Succeeded**, zero errors, four warnings, 607581315 bytes;
  BuildReport reported 9.348 seconds. Evidence: `build-result.txt`,
  `build-scenes.txt` and `Windows/HeliSim.exe`. Scenes: MainMenu then Multiplayer.
  Warnings are existing OpenXR interaction-profile/optional API migration advice
  and missing optional Pipeline runtime configuration. The MCP wrapper flags
  warnings as an error result, but the saved BuildReport confirms success.
- Final cleanup verified no active tests/build or temporary fixture folders and
  the saved MainMenu scene restored clean. Restored the two original XR preloads
  after build cleanup. Reverted only the build's incidental fallback-font migration
  to the original Git blob, then verified the font imports and no font/settings/XR
  or network-prefab diff remains. A first blob-export command did not write the
  temporary file; corrected with a validated YAML/length-checked export before
  reimport. All 32 frozen inputs still match. Reopened the menu-only Play preview
  to match the user's original working state. The standalone executable was built
  but not launched; desktop visual acceptance used the connected Editor.
- Source identity is menu commit `4c65175` plus the recorded menu repairs and existing Mapbox test changes.
  Hardware/HMD and separate-machine network acceptance remain unperformed; safe
  tests use harmless prefabs with the world inactive. Changes remain uncommitted.
