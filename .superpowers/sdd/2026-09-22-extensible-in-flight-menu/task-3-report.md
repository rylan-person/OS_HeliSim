# Task 3: prefab migration and legacy Escape retirement

## Implemented

- Migrated `Assets/Level/Prefabs/UI/Menu.prefab` with the live Unity MCP `RunCommand` API and `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset`.
- Added one scrim and overlay, 360-pixel navigation rail, Resume and Return actions, header, masked vertical ScrollRect, four registered pages in flight-session / controls / settings / cesium order, and initially inactive Apply / Discard / Cancel confirmation dialog.
- Retained existing flight, session, timer, map, reset, engine, VR, scene-selection, camera-selection, and journey-file control objects and their surviving UnityEvents. MainMenu's component identity and scene links remain on the always-active SessionActions object.
- Added keyboard reference from the supplied action bindings, NOVA popup toggle, per-presenter Apply / Revert, diagnostics, and numeric validation labels. GeneralSettingsPage's explicit scope separates controls from volume/NOVA settings; its default remains All for existing tests/consumers.
- Added `FlightSessionPage` for immediate domain actions, a void ApplyChanges UnityEvent adapter, controller scrim wiring, and `InFlightSettingSlider` to retain invalid intermediate input for presenter validation. Removed stale `UISliderController.OnTextInputChanged` persistent callbacks after replacing those components.
- Removed MainMenu's legacy Escape/time-scale code and retired SettingMenuOpener and SettingsMenu with their metadata after prefab replacement. Removed PrefSettings' now-unused `using Code.Scripts.UI` that otherwise prevented compilation after retiring SettingsMenu.
- Preserved Multiplayer's scene-added PrefSettings root and existing valid overrides; removed overrides and stripped references whose prefab targets were deleted by the migration. The final Multiplayer diff is 300 deleted lines only. Unrelated camera serialization defaults introduced by Unity's save were removed from this diff.

## Recovery of prior partial work

- The pre-existing MelbTesting diff contained an added Menu instance plus an independent `panelCameraType: 3` addition. Removed only the accidental Menu instance through the live editor. MelbTesting retains only the independent one-line camera addition and is excluded from this commit.
- Restored the three LogAssert.Expect calls commented out by the prior migration attempt. Unity's generated runtime project included both new tests under Tests/EditMode, which cannot resolve LogAssert. Moved only those two new menu test sources and their existing metadata to Tests/Editor. Existing Dashboard tests were not touched.
- The generated, ignored csproj Compile lists were synchronized locally to the test move for verification while editor refresh was unavailable. Generated project files are not committed.

## Verification completed

- Live editor confirmed the prefab initially lacked a controller, then successfully saved the migration and subsequent stale-listener repair.
- Live serialized-object audit confirmed all four pages, controller registration references, MainMenu timer text/assist/slider/map/popup references, presenter fields, and exactly one Multiplayer EventSystem with InputSystemUIInputModule. PrefSettings remained on the Multiplayer Menu root.
- `dotnet build Assembly-CSharp-Editor.csproj --verbosity quiet`: passed, zero errors, 33 existing/deprecation/unused-field warnings across runtime/editor compilation; includes both relocated test sources with LogAssert assertions intact.
- `dotnet build Assembly-CSharp.csproj --no-restore --verbosity quiet`: passed, zero errors and zero warnings on the final incremental build.
- Static prefab integrity check: 1,174 serialized object IDs, zero unresolved local fileID references, four unique page IDs, and no legacy menu or OnTextInputChanged callback names.
- Search for legacy `GetKeyDown(KeyCode.Escape)` and `Time.timeScale` under Assets/Code/Scripts returned no matches.
- `git diff --check`: passed after mechanically trimming Unity-generated trailing whitespace from Menu.prefab.

## Verification not completed / known inherited limitations

The live editor stopped answering MCP after script refresh. Both refresh and console requests remained pending and a bounded retry after fixing the compiler issues also failed to return; those requests were stopped. Editor.log recorded an asset refresh / script-compilation request but no subsequent completed compiler pass at inspection time. The user-owned editor was not restarted or closed. No second batch editor was launched against the locked project.

Consequently, the requested Play-mode baseline, manual input/navigation and dirty-dialog exercise, simulation/timer/Cesium continuity, Unity Test Runner execution, and visual checks at 1920x1080 / narrow width remain unverified. A read-only render helper is prepared at `.superpowers/sdd/2026-09-22-extensible-in-flight-menu/verify-menu-layout.cs`, but no rendered screenshots were produced. Compilation is not reported as test execution.

The original prefab already contains `MainMenu.CinematicCameraToggleCheck`, while MainMenu has no matching method. Its original event was preserved, not repaired by this task. The original prefab/Multiplayer also has no SceneTransition component binding; the scene-selector control was preserved without inventing a new domain action. Those inherited behaviors and the final visual layout need review once the editor connection is available.
