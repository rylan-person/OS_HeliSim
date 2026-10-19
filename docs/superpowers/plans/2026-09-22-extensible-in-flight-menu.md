# Extensible In-Flight Menu Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the competing Escape/settings overlay with a modular, non-pausing uGUI menu whose pages are registered in the Inspector.

**Architecture:** `PauseMenuController` owns overlay state, Escape, cursor restoration, page selection, and dirty-page confirmation. Each page root carries an `InFlightMenuPage`; settings presenters derive from it and own their draft/application logic. The existing `Menu.prefab` is restructured in place while `MainMenu` and `PrefSettings` remain domain/persistence backends.

**Tech Stack:** Unity 6.4, C#, uGUI, TextMeshPro, Input System, NUnit EditMode tests, existing PlayerPrefs/JSON persistence.

**Spec:** `docs/superpowers/specs/2026-09-22-extensible-in-flight-menu-design.md`

## Global Constraints

- Keep the Screen Space Overlay `Menu.prefab`; do not introduce UI Toolkit, external UI packages, or generated art.
- Opening/closing must not set `Time.timeScale`, pause physics, halt Cesium, halt the timer, or globally disable helicopter/XR input.
- Escape has exactly one owner: `PauseMenuController`.
- Preserve every existing menu action by moving it to a named page; preserve its UnityEvent target unless replaced by a planned page presenter.
- Return to Menu must not call `SceneManager.LoadScene` before a destination scene exists.
- Use TMP, fully qualified `UnityEngine.UI` types in new C# code, and anchors/layout groups instead of coordinated absolute child positions.
- Do not touch unrelated dashboard, camera, render-texture, scene, package, or project-setting changes already in the workspace.

## Review Focus

- Duplicate page IDs: Task 1 test `DuplicatePageIds_AreRejectedWithoutOpeningTwoPages` rejects them without displaying two page roots.
- Missing UI infrastructure: Task 1 test `Open_WithoutAnEventSystem_KeepsTheOverlayUsableAndLogsAWarning` makes a missing EventSystem diagnosable.
- Dirty closure: Task 1 close-confirmation tests and Task 2 Apply/Discard tests prevent silent loss of edits.
- Bad Cesium numbers: Task 2 tests prove invalid input leaves persisted values unchanged and displays field feedback.
- No destination scene: Task 1 return-action test proves the visible button does not navigate.

---

## File Structure

| File | Responsibility |
| --- | --- |
| `Assets/Code/Scripts/UI/InFlightMenu/InFlightMenuPage.cs` | Base page id, root, initial selection, lifecycle, and draft hooks. |
| `Assets/Code/Scripts/UI/InFlightMenu/PauseMenuController.cs` | Sole Escape, overlay, cursor, selection, and dirty-confirmation owner. |
| `Assets/Code/Scripts/UI/InFlightMenu/ReturnToMenuAction.cs` | Intentionally non-navigating action. |
| `Assets/Code/Scripts/UI/InFlightMenu/NumericSettingParser.cs` | Pure integer/range parser. |
| `Assets/Code/Scripts/UI/InFlightMenu/GeneralSettingsPage.cs` | Draft/Apply/Revert for control scheme, volume, and Nova popup. |
| `Assets/Code/Scripts/UI/InFlightMenu/CesiumSettingsPage.cs` | Draft/Apply/Revert for current Cesium settings. |
| `Assets/Code/Scripts/UI/MainMenu.cs` | Retains session/flight actions; loses Escape/`Time.timeScale` ownership. |
| `Assets/Code/Scripts/UI/PrefSettings.cs` | Retains persistence/runtime application; loses monolithic UI reference. |
| `Assets/Code/Tests/EditMode/PauseMenuControllerTests.cs` | Controller, registration, close-confirmation, and return-action tests. |
| `Assets/Code/Tests/EditMode/SettingsPageValidationTests.cs` | Validation and application tests. |
| `Assets/Level/Prefabs/UI/Menu.prefab` | New uGUI overlay hierarchy and serialized wiring. |
| `Assets/Level/Scenes/Multiplayer.unity` | Scoped cleanup of removed serialized fields/overrides. |

### Task 1: Implement the generic page contract and controller

**Files:**
- Create: `Assets/Code/Scripts/UI/InFlightMenu/InFlightMenuPage.cs`
- Create: `Assets/Code/Scripts/UI/InFlightMenu/PauseMenuController.cs`
- Create: `Assets/Code/Scripts/UI/InFlightMenu/ReturnToMenuAction.cs`
- Create: `Assets/Code/Tests/EditMode/PauseMenuControllerTests.cs`

**Interfaces:**
- Consumes: `UnityEngine.EventSystems.EventSystem`, `UnityEngine.InputSystem.Keyboard`, `UnityEngine.UI.Selectable`, and `UnityEngine.UI.Button`.
- Produces: `InFlightMenuPage.PageId`, `ContentRoot`, `InitialSelectable`, `IsDirty`, `TryApplyChanges()`, `DiscardChanges()`, and controller `Open()`, `Close()`, `Toggle()`, `SelectPage(string)`, `ResolvePendingClose(DirtyPageChoice)`.

- [ ] **Step 1: Write failing EditMode tests**

```csharp
[Test]
public void DuplicatePageIds_AreRejectedWithoutOpeningTwoPages()
{
    controller.Open();

    Assert.That(firstPage.ContentRoot.activeSelf, Is.True);
    Assert.That(secondPage.ContentRoot.activeSelf, Is.False);
    LogAssert.Expect(LogType.Error, new Regex("Duplicate in-flight menu page id"));
}

[Test]
public void RequestClose_WhenActivePageIsDirty_ShowsConfirmationAndKeepsOverlayOpen()
{
    dirtyPage.SetDirty(true);
    controller.Open();
    controller.Close();

    Assert.That(overlayRoot.activeSelf, Is.True);
    Assert.That(confirmationRoot.activeSelf, Is.True);
}

[Test]
public void RequestReturnToMenu_LogsWarningAndDoesNotLoadAnotherScene()
{
    string activeScene = SceneManager.GetActiveScene().name;
    LogAssert.Expect(LogType.Warning, new Regex("Return to Menu is unavailable"));
    returnAction.RequestReturnToMenu();
    Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(activeScene));
}
```

Add cases for selecting a page (exactly one root), missing `EventSystem.current` (warning/no exception), cursor restoration, and Apply/Discard/Cancel outcomes.

- [ ] **Step 2: Run the failing proof**

Run: `dotnet build Assembly-CSharp.csproj --no-restore`

Expected: FAIL because controller, page, choice enum, and return action do not exist.

- [ ] **Step 3: Write the smallest controller implementation**

```csharp
public abstract class InFlightMenuPage : UnityEngine.MonoBehaviour
{
    [UnityEngine.SerializeField] private string pageId;
    [UnityEngine.SerializeField] private UnityEngine.GameObject contentRoot;
    [UnityEngine.SerializeField] private UnityEngine.UI.Selectable initialSelectable;

    public string PageId => pageId;
    public UnityEngine.GameObject ContentRoot => contentRoot;
    public UnityEngine.UI.Selectable InitialSelectable => initialSelectable;
    public virtual bool IsDirty => false;
    public virtual void OnPageSelected() { }
    public virtual void OnPageDeselected() { }
    public virtual bool TryApplyChanges() => true;
    public virtual void DiscardChanges() { }
}

[System.Serializable]
public sealed class PauseMenuPageRegistration
{
    [UnityEngine.SerializeField] private InFlightMenuPage page;
    [UnityEngine.SerializeField] private UnityEngine.UI.Button navigationButton;
    public InFlightMenuPage Page => page;
    public UnityEngine.UI.Button NavigationButton => navigationButton;
}
```

Implement `PauseMenuController` with an ordered `List<PauseMenuPageRegistration>`, overlay, confirmation modal, Resume button, and explicit EventSystem reference. In `Awake`, reject null/duplicate ids, wire button listeners once, and deactivate all page roots. In `Update`, use only `Keyboard.current.escapeKey.wasPressedThisFrame`. `Open()` captures cursor state, enables overlay, selects the first valid page and Resume, and never writes `Time.timeScale`. `Close()` defers close/page change when the active page is dirty. `ResolvePendingClose` applies, discards, or cancels the deferred operation and restores cursor state only when closing.

Implement `ReturnToMenuAction.RequestReturnToMenu()` as:

```csharp
public void RequestReturnToMenu()
{
    // Menu navigation intentionally remains unavailable until a destination scene exists.
    UnityEngine.Debug.LogWarning("Return to Menu is unavailable because no menu scene has been configured.");
}
```

- [ ] **Step 4: Run focused tests**

Run: `dotnet build Assembly-CSharp.csproj --no-restore`

Expected: PASS; then run `PauseMenuControllerTests` from Unity's EditMode Test Runner and expect all cases to pass.

- [ ] **Step 5: Commit the foundation**

Run: `git add Assets/Code/Scripts/UI/InFlightMenu/InFlightMenuPage.cs Assets/Code/Scripts/UI/InFlightMenu/PauseMenuController.cs Assets/Code/Scripts/UI/InFlightMenu/ReturnToMenuAction.cs Assets/Code/Tests/EditMode/PauseMenuControllerTests.cs; git commit -m "Add extensible in-flight menu controller"`

### Task 2: Implement settings drafts and validation

**Files:**
- Create: `Assets/Code/Scripts/UI/InFlightMenu/NumericSettingParser.cs`
- Create: `Assets/Code/Scripts/UI/InFlightMenu/GeneralSettingsPage.cs`
- Create: `Assets/Code/Scripts/UI/InFlightMenu/CesiumSettingsPage.cs`
- Create: `Assets/Code/Tests/EditMode/SettingsPageValidationTests.cs`
- Modify: `Assets/Code/Scripts/UI/PrefSettings.cs:61-214`

**Interfaces:**
- Consumes: Task 1 `InFlightMenuPage`, public `PrefSettings` fields, `PrefSettings.VariablesToObjects()`, TMP inputs/dropdowns, and uGUI toggles/buttons.
- Produces: `NumericSettingParser.TryParseInt(string, int, int, out int, out string)` and `TryApplyChanges()`/`DiscardChanges()` on both presenters.

- [ ] **Step 1: Write failing parser and apply tests**

```csharp
[TestCase("42", 10, 256, true, 42)]
[TestCase("nine", 10, 256, false, 0)]
[TestCase("9", 10, 256, false, 0)]
[TestCase("257", 10, 256, false, 0)]
public void TryParseInt_RequiresWholeValueWithinInclusiveRange(
    string input, int min, int max, bool expectedResult, int expectedValue)
{
    bool parsed = NumericSettingParser.TryParseInt(input, min, max, out int value, out string error);
    Assert.That(parsed, Is.EqualTo(expectedResult));
    Assert.That(value, Is.EqualTo(expectedValue));
    Assert.That(string.IsNullOrEmpty(error), Is.EqualTo(expectedResult));
}

[Test]
public void CesiumPage_ApplyWithInvalidLoadingLimit_DoesNotChangePrefSettings()
{
    prefSettings.LoadingDescendantLimit = 10;
    page.SetLoadingDescendantLimitForTest("invalid");
    Assert.That(page.TryApplyChanges(), Is.False);
    Assert.That(prefSettings.LoadingDescendantLimit, Is.EqualTo(10));
    Assert.That(page.LoadingDescendantLimitError, Does.Contain("whole number"));
}
```

Cover volume (0–200), screen-space error (10–256), tile loads (1–1000), descendant limit (0–1000), non-negative cached bytes, dirty state, Revert, and valid all-fields Apply.

- [ ] **Step 2: Run the failing proof**

Run: `dotnet build Assembly-CSharp.csproj --no-restore`

Expected: FAIL because the parser and presenters do not exist.

- [ ] **Step 3: Implement staged page presenters**

```csharp
public static class NumericSettingParser
{
    public static bool TryParseInt(string input, int minimum, int maximum, out int value, out string error)
    {
        if (!int.TryParse(input, out value))
        {
            error = "Enter a whole number.";
            return false;
        }
        if (value < minimum || value > maximum)
        {
            error = $"Enter a value from {minimum} to {maximum}.";
            return false;
        }
        error = string.Empty;
        return true;
    }
}
```

Both pages derive from `InFlightMenuPage`, use an optional serialized `PrefSettings` reference with `GetComponentInParent<PrefSettings>()` fallback, and disable Apply with a diagnostic when absent. `OnPageSelected()` snapshots only when clean; listeners set dirty when widgets differ from that snapshot. `DiscardChanges()` restores the snapshot and clears errors. `TryApplyChanges()` validates every field before changing any backend field, then copies the valid draft to `PrefSettings`, calls `VariablesToObjects()`, snapshots it, and clears dirty.

`GeneralSettingsPage` owns control scheme, volume, and Nova popup. `CesiumSettingsPage` owns screen-space error, preload flags, forbid holes, tile loads, cached bytes, descendant limit, culling flags, culled error, and one TMP error label per numeric input. Remove `PrefSettings.settingsMenu`, old-reference preservation in `JSONToVariables`, and the startup UI call. Leave `SettingsMenu` in place until Task 3 rewires its UnityEvents.

- [ ] **Step 4: Run focused tests**

Run: `dotnet build Assembly-CSharp.csproj --no-restore`

Expected: PASS; then run `SettingsPageValidationTests` in Unity's EditMode Test Runner and expect all cases to pass.

- [ ] **Step 5: Commit settings pages**

Run: `git add Assets/Code/Scripts/UI/InFlightMenu/NumericSettingParser.cs Assets/Code/Scripts/UI/InFlightMenu/GeneralSettingsPage.cs Assets/Code/Scripts/UI/InFlightMenu/CesiumSettingsPage.cs Assets/Code/Scripts/UI/PrefSettings.cs Assets/Code/Tests/EditMode/SettingsPageValidationTests.cs; git commit -m "Split menu settings into validated pages"`

### Task 3: Migrate the prefab and retire legacy key ownership

**Files:**
- Modify: `Assets/Code/Scripts/UI/MainMenu.cs:106-131`
- Delete: `Assets/Code/Scripts/UI/SettingMenuOpener.cs`
- Delete: `Assets/Code/Scripts/UI/SettingMenuOpener.cs.meta`
- Delete: `Assets/Code/Scripts/UI/SettingsMenu.cs`
- Delete: `Assets/Code/Scripts/UI/SettingsMenu.cs.meta`
- Modify: `Assets/Level/Prefabs/UI/Menu.prefab`
- Modify: `Assets/Level/Scenes/Multiplayer.unity`

**Interfaces:**
- Consumes: Task 1 controller/page/action, Task 2 presenters, current `MainMenu`, `SceneTransition`, `PrefSettings`, and current UnityEvents.
- Produces: registered `flight-session`, `controls`, `settings`, and `cesium` pages and one overlay.

- [ ] **Step 1: Establish a manual baseline**

Open `Assets/Level/Scenes/Multiplayer.unity` in Unity 6000.4.5f1. In Play mode, record the effect of Resume, assist, map toggle, scene selector, reset, engine, VR reset, timer, control scheme, volume, Nova popup, every Cesium field, and Apply. Exit Play mode without saving.

- [ ] **Step 2: Retire only legacy Escape logic**

Remove `MainMenu.Update()` lines 123–129 which write `Time.timeScale`, but retain all domain methods. Delete `SettingMenuOpener` and `SettingsMenu`, including their meta files, only after Task 2 presenters replace every legacy binding. Add `PauseMenuController` before removing the legacy component. Run `rg -n "GetKeyDown\(KeyCode\.Escape\)|Time\.timeScale" Assets/Code/Scripts`; expect no legacy Escape listener and no menu-related time-scale write.

- [ ] **Step 3: Rebuild the Menu hierarchy in place**

Keep Canvas, GraphicRaycaster, and 1920×1080 CanvasScaler. Build this anchored/layout-driven hierarchy:

```text
Menu
├── Scrim (full stretch Image; #0D1018 at 55% alpha; raycast target enabled)
├── Overlay
│   ├── NavigationRail (left stretch, 360 px opaque charcoal)
│   │   ├── ResumeButton
│   │   ├── PageNavigation (VerticalLayoutGroup; 12 px spacing)
│   │   │   ├── FlightSessionButton
│   │   │   ├── ControlsButton
│   │   │   ├── SettingsButton
│   │   │   └── CesiumButton
│   │   └── ReturnToMenuButton (bottom anchored behind divider)
│   ├── ContentPanel (remaining width, translucent charcoal)
│   │   ├── Header (title/description TMP text)
│   │   └── PageViewport (ScrollRect > Viewport/Mask > Content)
│   │       ├── FlightSessionPage
│   │       ├── ControlsPage
│   │       ├── SettingsPage
│   │       └── CesiumPage
│   └── UnsavedChangesDialog (inactive; Apply, Discard, Cancel)
└── PauseMenuController
```

Put `VerticalLayoutGroup` and `ContentSizeFitter` on the ScrollRect content root only; do not use them together where the layout group controls that same axis. Decorative images/text have Raycast Target off; interactive controls retain it. Give buttons readable normal/hover/selected/disabled states.

Reparent existing controls without changing their UnityEvents: assists/engine/reset/timer/maps/scenes/journey/VR reset to Flight & Session; control scheme to Controls; volume/Nova popup to Settings; Cesium fields to Cesium. On Controls, add a non-editable **Keyboard reference** section sourced from `Assets/Code/InputActions/DefaultHeliAction.inputactions`: Pitch `W`/`S`, Roll `A`/`D`, Throttle `1`/`2`, and Start Engine `F1`; label flight-hardware bindings as configured through the selected profile. Replace legacy Apply with each presenter’s Apply/Revert. Register each page in rail order. Wire Resume to `Close`, rail buttons to `SelectPage`, dialog buttons to `ResolvePendingClose`, and Return to `RequestReturnToMenu`.

- [ ] **Step 4: Repair scoped scene overrides**

In `Multiplayer.unity`, preserve intentional layout overrides and remove only overrides for deleted `PrefSettings`, `SettingsMenu`, and `SettingMenuOpener` fields. Keep scene-added `PrefSettings` on the Menu root for descendant lookup. Confirm exactly one EventSystem uses `InputSystemUIInputModule`. Save only `Menu.prefab` and this scoped scene change.

- [ ] **Step 5: Verify in Play mode**

Verify Escape opens/closes one overlay; `Time.timeScale`, physics, timer, and Cesium continue; cursor/default selection restores; scrolling, mouse, keyboard navigation, migrated controls, validation, dirty dialog, and no-op Return work. Repeat at 1920×1080 and a narrower Game view. Console must be clear except the expected Return warning.

- [ ] **Step 6: Commit the UI migration**

Run: `git add Assets/Code/Scripts/UI/MainMenu.cs Assets/Code/Scripts/UI/SettingMenuOpener.cs Assets/Code/Scripts/UI/SettingMenuOpener.cs.meta Assets/Code/Scripts/UI/SettingsMenu.cs Assets/Code/Scripts/UI/SettingsMenu.cs.meta Assets/Level/Prefabs/UI/Menu.prefab Assets/Level/Scenes/Multiplayer.unity; git commit -m "Replace legacy escape settings overlay"`

### Task 4: Final proof and scoped review

**Files:**
- Modify only a Task 1–3 file if verification identifies a scoped defect.

**Interfaces:**
- Consumes: controller, presenters, tests, and rebuilt prefab.
- Produces: build/test/manual verification evidence.

- [ ] **Step 1: Run build and EditMode tests**

Run: `dotnet build Assembly-CSharp.csproj --no-restore`

Expected: PASS. Run all EditMode tests in **Window > General > Test Runner**; existing `DashboardManagerTests` and both new suites pass.

- [ ] **Step 2: Run batch tests if the pinned editor is available**

Run: `Unity.exe -batchmode -quit -projectPath . -runTests -testPlatform EditMode`

Expected: exit code 0. If Unity.exe is unavailable from the shell, record that fact and retain the Test Runner result rather than use another editor version.

- [ ] **Step 3: Review final scope**

Run: `git diff --check`

Then inspect the diff limited to `Assets/Code/Scripts/UI`, `Assets/Code/Tests/EditMode`, `Assets/Level/Prefabs/UI/Menu.prefab`, and `Assets/Level/Scenes/Multiplayer.unity`.

Expected: no whitespace errors and no unplanned workspace changes.

- [ ] **Step 4: Commit only verification fixes when they exist**

Run: `git add Assets/Code/Scripts/UI Assets/Code/Tests/EditMode Assets/Level/Prefabs/UI/Menu.prefab Assets/Level/Scenes/Multiplayer.unity; git commit -m "Verify in-flight menu integration"`

Expected: create this commit only if verification required a scoped corrective change.
