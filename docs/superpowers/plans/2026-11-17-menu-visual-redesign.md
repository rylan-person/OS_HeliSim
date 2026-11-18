# Menu visual redesign: Unity port plan — 2026-11-17

Port the approved HTML preview ([menu-redesign-preview.html](../../design/menu-redesign-preview.html),
published as "HeliSim Menu Redesign") into `Menu.prefab` (in-flight) and
`MainMenu.prefab` (main menu). The preview's **Design spec** tab holds the palette, type
scale, spacing and state rules; this plan says how to apply them in uGUI.

## Problem and intended result

The design review (same date) found invisible keyboard/HOTAS focus, hover states that
darken, disabled dialog buttons tinted like hover, four reds with no fixed meaning,
the default LiberationSans font with 18 near-identical sizes, and uneven padding and
heights. Both menus should end up with:

- visible, consistent states: hover and focus brighten, focus adds a teal ring,
  disabled is 40% alpha and never looks highlighted;
- one palette (teal = active/selectable, amber = caution, red = destructive only);
- Barlow / Barlow Condensed / JetBrains Mono on a six-step type scale;
- symmetric padding on a 6/12/18/24/36/48/60 px spacing scale, two control heights;
- the same components in both menus, because MainMenu already clones its settings
  pages from Menu.

## Preserved behaviour and exclusions

Keep every page, control, `onClick`/`onValueChanged` binding, serialized reference,
page id, initial-focus rule, dirty-setting confirmation, dropdown repairs from
2026-10-20, raycast targets, and the GameObject names that tests and presenters look up.
`SettingsToggleVisual` stays the segmented Off/On control (asserted by
`BooleanSettings_UseSegmentedOffOnVisuals`).

Not in scope: flight dynamics, NOVA output or limits, XR tracking/recenter hierarchy,
networking/role authority, telemetry, Cesium runtime behaviour, legacy JoinUI. The
`UnlockCollectivePopup` (NOVA collective lock) is restyled only; its button wiring and
when it appears do not change.

## Risk and ownership

**Medium**: bounded UI presentation plus input-binding display, across two large
serialized prefabs. Escalate to high if any step would change session flow, role
handling, the NOVA popup's behaviour or XR camera/canvas placement.

- Independent UI/UX review (complex tier) of this plan before implementation and of
  rendered screenshots after integration.
- Independent technical review (complex tier) of the final diff for broken serialized
  references, lost listeners, assembly placement and per-frame allocations.
- The lead is the only Unity operator: imports, prefab edits, tests, builds, screenshots.

Delegable code with disjoint ownership (writers never share files):

| Packet | Owner writes | Tier |
|---|---|---|
| A. Theme and state runtime | `Assets/Code/Scripts/UI/Theme/` (new), `SettingsToggleVisual.cs`, their tests | Complex |
| B. Restyle Editor tool | `Assets/Code/Tests/Editor/MenuThemeApplier.cs` (new), `MenuThemeAssetTests.cs` (new) | Complex |
| C. Builder alignment | `MenuSceneBuilder.cs` helpers only | Simple, after A |

Asset creation (fonts, sprites, theme asset) and every prefab write stay with the lead.

## Implementation steps

### 1. Foundations (packet A, then lead imports assets)

1. **Fonts.** Add Barlow (Regular, Medium, SemiBold), Barlow Condensed (SemiBold, Bold)
   and JetBrains Mono (Medium) TTFs with their OFL licence files under
   `Assets/Art/Fonts/`. Create TMP font assets in the Editor (dynamic atlas,
   LiberationSans SDF as fallback). Downloading the TTFs needs the user's approval;
   the source is the official Google Fonts / JetBrains GitHub releases.
2. **Sprites.** Have an Editor script write three PNGs to `Assets/Art/Textures/UI/Menu/`:
   rounded rectangle radius 9 (controls), radius 15 (panels), and a 2 px ring radius
   11 (focus). Set them to Sprite (2D and UI) with 9-slice borders. These replace the
   current mix of `box.png` and plain rects. The existing dropdown tests require a null
   sprite on dropdown fields and popups, so those keep a null sprite (square) unless
   the tests are deliberately updated with the review.
3. **`MenuTheme` ScriptableObject** (`Assets/Code/Scripts/UI/Theme/MenuTheme.cs` plus one
   asset at `Assets/Level/Prefabs/UI/Theme/HeliSimMenuTheme.asset`): palette tokens,
   font assets, the type scale and the sprites. It is runtime-safe, with no Editor references.
4. **`MenuSelectableStyle`** runtime component: set the Selectable transition to None,
   and drive the target Image and label colours from the theme for Normal, Hover,
   Pressed, Selected and Disabled using `IPointerEnter/Exit/Down/Up` and `ISelect/IDeselect`.
   Check `IsInteractable()` in `LateUpdate` only when it changes (no allocations),
   following the `SettingsToggleVisual` pattern. A child `FocusRing` Image is enabled
   only while selected, so keyboard and HOTAS focus is visible.
   Variants: Default, Primary, Ghost, Destructive, NavItem, Row.
5. **`SettingsToggleVisual`**: replace the single selected colour with separate
   `selectedOnBackground` (accent) and `selectedOffBackground` (bg-4) colours, sourced
   from the theme. `SettingsToggleVisualTests` currently asserts the On and Off
   selected colours are equal. Update that assertion on purpose, record the reason in
   the test, and add a case showing On and Off are now distinguishable.
6. Focused tests for 4–5: state mapping, disabled never equal to highlighted, focus
   ring only while selected, missing theme/graphic tolerated without exceptions.

### 2. Restyle both prefabs in place (packet B tool, lead runs it)

Write an idempotent Editor command, **HeliSim › Apply Menu Theme**, that opens each prefab
with `PrefabUtility.LoadPrefabContents`, applies roles, saves and unloads. It must not
rebuild hierarchies, so GUIDs, fileIDs and listeners survive. It refuses to run while the
Editor is playing, compiling or has dirty scenes, as `MenuSceneBuilder.Build` does.

- **Text roles by name and parent:** `PageTitle` → Display (60). `*Heading`,
  `SectionHeading`, `CategoryHeadingTemplate` → Eyebrow (19, caps, tx-3).
  `Description`/`*Help`/`*Unavailable` → Hint (21, tx-2). Row `Label` → Body (24).
  Button labels → Body SemiBold. Values, inputs and bindings → Mono (21).
  `ValidationError`/`Diagnostic` → caution.
  Any unmapped TMP is logged, not guessed.
- **Surfaces:** backdrop, rail, content panel, rows group, inputs → bg tokens and sprites.
  Replace the off-palette `#1A1A1A` slider tracks and the blue `#99D9FF` tints.
- **Selectables:** add `MenuSelectableStyle` with the variant chosen from name and context.
  Apply, Start, Join and Resume are Primary. Quit, Reset all bindings and Return to menu
  are Destructive. Rail buttons are NavItem.
- **Layout:** page padding 54/60 symmetric (replacing 5/15/5/30). Spacing only from the
  scale. Setting rows min 80 (see decisions). Buttons 66, controls 60, nav items 66.
  Binding rows stay ≤ 56 (asserted by `ControlsPage_HasDeviceTabsCompactRowsAndDialogOutsideScrollViewport`).
- **Dialogs:** one dialog style (bg-2 panel, radius 15, 36 padding, actions right-aligned)
  for BindingDialog, UnsavedChangesDialog, ConfirmChanges, ConfirmQuit and the NOVA popup.
- **Copy fixes:** capitalisation as in the preview ("Reset all bindings", "Cancel").
  NOVA popup text becomes "Click to enable the helicopter. Make sure NOVA is running."
  Rename `Asissts` objects only where no test, presenter or serialized lookup uses the
  name. Check by grep first.
- Also update `MenuTransitionView`'s hard-coded loading colours to theme tokens.

Asset tests (`MenuThemeAssetTests`) cover both prefabs:
- every TMP uses a theme font asset and a scale size;
- every Image colour is a palette token (alpha variants allowed);
- every Selectable has `MenuSelectableStyle` or `SettingsToggleVisual`;
- no ColorTint transition remains;
- disabled ≠ highlighted;
- page padding is symmetric;
- sprites come only from the theme set (except dropdowns, as above).

Write these tests first and record their RED run as evidence.

### 3. Structure the preview adds (lead, after step 2 is green)

These are small presentation additions with existing data sources:

1. **Settings action bar.** Group Apply and Revert into a bottom bar on Display,
   Settings and Cesium. It shows "Unsaved changes" or "All changes applied" from the
   existing `IsDirty`. Keep the existing button objects and listeners, and only
   reparent them. Apply is disabled while a `ValidationError` is showing, which matches
   the presenters' current refusal to apply invalid input.
2. **Unavailable notes** become one collapsed disclosure per page. Reuse the existing
   Advanced Control Settings toggle pattern: a toggle activates a child list.
3. **Main-menu Settings hub.** Turn the four category buttons into 2×2 tiles with icon,
   title and description. Summary lines need a small `MainMenuScreen` addition that
   reads the applied settings; a static description is enough if the reviewer prefers.
4. **Rail.** Brand block, nav items with icons (new 24 px stroke icon sprites
   exported from the preview), and Quit / Return to menu separated at the bottom.
5. **Time trial.** Order the buttons −30 s, play/pause, +30 s and align the clock. Check
   the `Plus 30s Button` / `Minus 30s Button` wiring before renaming anything, because
   the labels and names are currently swapped.

### 4. Approved behaviour additions (user approved all on 2026-11-17; each is its own small change)

- Confirmation dialog for **Return to menu** (it currently returns immediately).
- Binding conflict warning and a "Changed" badge on rebound commands.
- Header status chips (input, VR, NOVA) built from `MenuHardwareStatus`. NOVA stays
  read-only and "unverified".
- Quick-start card that summarises the stored last setup from `MenuSessionOptionsStore`.
- Static topographic backdrop texture for the main menu.

### 5. Builder alignment (packet C)

Point `MenuSceneBuilder`'s `Label`, `Button`, `Toggle`, `Input`, `Dropdown`, `Card` and
`Modal` helpers at the theme, so a future **Build Main Menu Assets** run produces the
same look. Do not run Build on the real prefab. Verify it in a throwaway output path
and compare it with the restyled asset through `MenuThemeAssetTests`.

## Verification

Evidence goes in unique folders under `TestResults/menu-redesign-<step>/`, following
[TESTING.md](../../TESTING.md). Confirm the Editor is idle, the scenes are saved, and the
user's unsaved authoring is preserved before every run.

1. Focused tests: MenuTheme/Selectable/SettingsToggleVisual, `MenuThemeAssetTests`,
   `MenuPrefabRefinementTests`, `MainMenuAssetTests`, `MainMenuFlowTests`,
   `MainMenuPageRouterTests`, `PauseMenuControllerTests`, `ControlsRebindingPageTests`,
   `SettingsPageValidationTests`. Confirm discovery before running.
2. Full EditMode suite (376 passed at the last baseline) with discovered/executed
   comparison, then a Windows player build. Restore XR preloads and revert incidental
   font migrations as in the 2026-10-20 run.
3. Rendered checks in the MainMenu scene and the in-flight overlay at 1920×1080 and
   1280×720, captured from the connected Editor:
   - every main-menu page and settings sub-page;
   - the in-flight pages;
   - Binding, Unsaved, Quit and NOVA dialogs;
   - a dropdown popup open.

   Compare each with the matching preview view.
4. Keyboard-only pass: arrows, Enter and Esc through every page. The focus ring must
   always be visible, initial focus unchanged, and Esc must still cancel rebinding.
5. Reviews, plus pending human acceptance: seated-VR legibility of the new type sizes
   in an HMD and a HOTAS navigation pass. No NOVA output is enabled for any of this.

Stop when steps 1–2 (and any approved step 4 items) pass every check above, or report
the blocker. Changes stay uncommitted until the user authorises a commit. The existing
uncommitted raycast-target fix in `Menu.prefab` and its tests are kept and committed
separately from this work.

## Decisions

Recorded 2026-11-17:

1. **Density:** setting rows are 80 px in Unity, not the preview's 96 px. Binding rows stay ≤ 56.
2. **Behaviour additions:** include every step 4 item. The Return-to-menu confirmation
   changes session flow, so its review covers the dirty-settings path and the
   multiplayer return path. Status chips stay read-only.

3. **Font download and start:** approved by the user on 2026-11-17 ("Start implementing").
4. **Page padding:** the viewport offsets already provide the outer 54/60 margins, so page
   layout groups use a symmetric 12/12/6/36 padding (enough to keep focus rings unclipped).
5. **Builder alignment:** instead of re-pointing each helper, `MenuSceneBuilder.Build` and
   `RepairUsabilityAssets` run the same `MenuThemeApplier` pass before saving, and the builder's
   colour constants are now palette tokens.

## Results

### 2026-11-17: steps 1, 2 and 5

- **Fonts:** Barlow (Regular, Medium, SemiBold), Barlow Condensed (SemiBold, Bold) and
  JetBrains Mono (Medium) from google/fonts and JetBrains/JetBrainsMono, with their OFL files,
  in `Assets/Art/Fonts/`. These are dynamic TMP font assets (90 pt sampling, 1024 atlas, ASCII
  pre-populated, LiberationSans SDF fallback). Each asset is about 2 MB because the atlas is
  serialised in it.
- **Theme runtime** in `Assets/Code/Scripts/UI/Theme/`:
  - `MenuPalette` holds the tokens.
  - `MenuTheme` is a ScriptableObject with the fonts, the type scale (60/36/24/21/19/21) and
    the sprites.
  - `MenuSelectableStyle` handles the Default, Primary, Ghost, Destructive, NavItem, Row, Field,
    Tab and FocusOnly variants. It shows a focus ring for navigation selection only, and keeps
    disabled at 40% alpha.
  - A Tab variant was added because `ControlsRebindingPage` disables the active tab, so that
    tab shows teal instead of looking dimmed.
  - `SettingsToggleVisual` now has separate On (teal) and Off (neutral) colours, uses the
    theme when one is assigned, and dims when disabled. Its test was deliberately updated to
    match.
- **Generated assets:** `MenuThemeAssets` (**HeliSim › Create Menu Theme Assets**) created the
  three 9-sliced sprites in `Assets/Art/Textures/UI/Menu/` and
  `Assets/Level/Prefabs/UI/Theme/HeliSimMenuTheme.asset`.
- **Restyle tool:** `MenuThemeApplier` (**HeliSim › Apply Menu Theme**) restyled both prefabs in
  place. Running it a second time left both prefab hashes unchanged.
  - The only mapping by fallback was the main-menu scrollbar track (to bg-2).
  - Text: the "Asissts" objects were renamed to "Label". Copy was moved to sentence case and
    the NOVA prompt was rewritten.
  - Dialog panels now fit their content.
  - The user's uncommitted raycast-target fix is preserved (`MenuPrefabRefinementTests` pass).
  - `MenuTransitionView` colours now come from the palette.
- **RED evidence:** `TestResults/menu-redesign-step2/red.xml`. Of the 15 `MenuThemeAssetTests`
  cases, 13 failed before the restyle.
- **Focused checks:** `TestResults/menu-redesign-step2/focused-final.xml` passed **150/150**.
  It covered MenuThemeAsset, MenuSelectableStyle, SettingsToggleVisual, MenuPrefabRefinement,
  MainMenuAsset, MainMenuFlow, MainMenuPageRouter, PauseMenuController, ControlsRebindingPage
  and SettingsPageValidation.
- **Full EditMode suite:** `TestResults/menu-redesign-full/full-editmode.xml` passed 407 of 409
  discovered tests. That is the 376 baseline plus 31 new tests and the user's 2 new
  refinement tests.
  - The two failures are Mapbox SDK live-API tests: `MapTest.ChangeTilesetId` and
    `TileTest.TileLoading`. They failed again when rerun on their own
    (`mapbox-rerun.xml`). They passed in the 2026-10-20 baseline, and this change touches
    nothing under Mapbox.
  - These failures need a separate look (token, network or API) and are not attributed to
    this work.
- **Rendered checks:** offscreen renders of the in-flight pages (Flight, Controls, Settings,
  Display, Cesium), the Unsaved, Binding and NOVA dialogs, and the main-menu Fly, Multiplayer,
  Settings, Hardware, Display and Quit views are in `TestResults/menu-redesign-step2/shots/`.
  These renders caught two problems, both now fixed: sliced centres were unfilled, and the
  destructive fill was too strong in linear colour space.
- **Not yet done** (as of steps 1, 2 and 5; step 3 has since landed, see below):
  - step 3 (structure) and step 4 (approved behaviours);
  - running the builder against a throwaway path;
  - the Windows player build;
  - the keyboard-only pass in Play mode;
  - the independent UI and technical reviews;
  - HMD and HOTAS acceptance.
  Nothing is committed.

### 2026-11-17: step 3 (paused for user review before step 4)

`MenuStructureApplier` (**HeliSim › Apply Menu Structure**) moves existing controls rather
than recreating them, then re-runs the theme pass. Running it a second time left both prefab
hashes unchanged. The builder and the dropdown repair command now run the same pass.

- **Action bar** on Display, Settings and Cesium in both menus. It holds a state dot, a
  "Unsaved changes" / "All changes applied" label, Revert and Apply. The existing buttons were
  only moved into the bar. `MenuActionBar` reads `IsDirty` and only updates when that value
  changes. The presenters still decide when Apply is enabled.
- **Not-available notes:** each page that has them (Settings: 5 notes, Display: 1) now has one
  collapsed `UnavailableDisclosure` row. `MenuDisclosure` shows the list and turns the chevron
  when the row is toggled open.
- **Settings hub:** the four category buttons are now 2×2 tiles with an icon, a title and a
  static description. They are still the router's page-entry buttons.
- **Rail:** both rails have icons, rasterised from the preview's own SVG set into
  `Assets/Art/Textures/UI/Menu/Icons/` (72 px source, drawn at 24 px). Labels are
  left-aligned. Both menu controllers now mark the current page's rail item, and Settings
  stays marked while one of its sub-pages is open.
- **Time trial:** one row ordered −30 s, play, pause, +30 s, with the clock centred.
  - The swapped object names were corrected: `Plus 30s Button` was the button that removes
    30 s. They are now `Remove30sButton` (Remove30Seconds) and `Add30sButton` (Add30Seconds).
  - The handlers are unchanged and asserted. The emptied Play/Pause row was removed.
- **RED evidence:** `TestResults/menu-redesign-step3/red.xml`. All 8 `MenuStructureAssetTests`
  cases failed before the tool ran. The 4 `MenuStructureRuntimeTests` cases passed.
- **Focused checks:** the twelve menu fixtures passed 160 of 162 in `focused.xml`. The 2
  failures were test expectations, now fixed, and those fixtures pass 23/23 in `focused2.xml`:
  - the hub tiles get their click listeners from the router at runtime, not as serialized
    listeners;
  - Apply has moved into the ActionBar, so its asserted path changed.
- **Renders:** `TestResults/menu-redesign-step3/shots/`.
- **Not run yet:** the full suite and the player build, which are deferred until after step 4.

### 2026-11-17: rebuild to match the reference (replaces the step 2–3 restyle tools)

The side-by-side review showed that restyling the old objects kept the old layout: stacked
labels, separate cards, full-width buttons, a full-screen in-flight panel. On the user's
request ("entirely remake them to match the reference") both menus are now rebuilt from the
preview's layout.

- **New tools:** `MenuKit` (component kit: rows, groups, segmented switches, sliders, fields,
  selects, buttons, chips, keycaps, notes, dialogs) and `MenuViewRebuilder`
  (menu item **HeliSim/Rebuild Menu Views**). `MenuSceneBuilder.BuildMainMenuPrefab` builds the
  main menu with the same kit in a scratch scene. Every size is 1.5 × the preview's CSS value.
- **Removed:** `MenuThemeApplier` and `MenuStructureApplier`. Running them on the new layout
  would have undone it.
- **Kept objects:** the in-flight rebuild recreates every visual object but keeps the objects
  that carry behaviour: presenters and controllers, every Selectable (persistent listeners
  and `UI.prefab` / `Multiplayer.unity` overrides target them by file ID), the labels presenters
  write to, and the session sections `UI.prefab` hides (`Aircraft`, `Time Trial`, `Journey`,
  `Advanced Control Settings`). Saving is refused if any serialized reference or persistent
  listener count would change.
- **Shell:** the in-flight menu is a framed panel inset 60 px over the world, with the HELISIM
  brand, a breadcrumb top bar and the "simulation keeps running" indicator. Settings action
  bars are pinned below the scroll view and follow their page through `MenuPageAttachment`.
  Apply and Revert are disabled while a page is clean.
- **Runtime changes (presentation only):** `MenuBreadcrumb`, `MenuPageAttachment`,
  `MenuActionBar` dirty-only groups, the `SettingsToggleVisual.neutralSelection` option, and
  the multiplayer role as a Pilot / Spectator toggle (`MainMenuScreen.spectatorRole`).
  Hardware cards read live device and headset status. Readouts show bare values ("100%").
- **Main menu fits without scrolling:** at the user's request, main-menu pages use 42 px top and
  bottom padding and 36 px section spacing (the preview's 54 / 72 / 48 made Fly and Help overflow
  a 1080p viewport). The quick-start card padding is 24 px. Every top-level page now fits at
  1920 × 1080 (`MainMenuTopLevelPages_FitA1080pViewportWithoutScrolling`). The scroll view stays
  as a fallback for long settings sub-pages (Display, World streaming, Controls with bindings)
  and smaller canvases; its bar auto-hides when the page fits.
- **Still step 4:** header status chips, the quick-start summary values, the topographic
  backdrop, the return-to-menu confirmation, the engine status chip, binding conflict and
  "Changed" badges.
- **Evidence:** `TestResults/menu-rebuild/` (`sbs/` pairs, `refs-before.txt`, `backup/`
  pre-rebuild prefabs).

### 2026-11-17: step 4 (approved behaviour additions)

- **Return to menu asks first.** `PauseMenuController.RequestConfirmedAction` shows
  `Overlay/ReturnConfirmationDialog` ("Return to the main menu?", Stay in flight / Return to menu,
  with Stay focused). Confirming hands the action to the existing `RequestAction`, so a dirty
  settings page still gets Apply / Discard / Keep editing, and cancelling that returns focus to
  the rail button. Esc and Close cancel the confirmation first. `ReturnToMenuAction` keeps its
  Play-mode and build-settings guards; `MenuSessionCoordinator.RequestReturnToMenu` is unchanged,
  so host, client and spectator return the same way as before. Without the dialog fields the old
  direct path is used. `UI.prefab` and `Multiplayer.unity` don't override the controller.
- **Bindings.** A "Changed" badge shows when a binding's effective path differs from its default.
  The row's feedback line shows "Also bound to X." when another active binding on the same device
  tab uses the same path, and transient errors take precedence. The Controls footer counts commands
  that differ from the defaults on the selected tab. Rows stay 56 px. Conflict matching compares
  path strings (case-insensitive), so two different paths that name the same physical control are
  not detected; no two default commands share a path.
- **Main-menu header chips** (input, VR, NOVA) are read-only. Input and VR come from
  `MenuHardwareStatus` and refresh on device changes or when the headset state changes; NOVA always
  reads "NOVA unverified" and is never probed.
- **Quick start** summarises the stored last valid setup that "Fly last setup" launches
  (assistance and CSV recording from `MenuSessionOptionsStore`).
- **Backdrop:** `MenuTopographicBackdrop.png` (1920 × 1080, generated by
  `MenuThemeAssets.EnsureBackdrop` from the preview's `drawTopo`) fills the main menu behind the shell.
- **Tests:** full EditMode suite 447 / 450; the 3 failures were the two known Mapbox live-API tests
  and `MainMenuFlowTests.StartupAndSharedSettings_WorkBeforeAHelicopterExists`, whose scroll check
  assumed every settings sub-page overflows. The Audio page now fits after the main-menu tightening,
  so the test checks the content's top offset instead; it passes (4 / 4). Focused rerun after the
  review fixes: 135 / 135 (`TestResults/menu-rebuild/step4-focused.xml`).
- **Independent review** (complex tier, read-only): no high-severity findings. Fixed: focus after
  cancelling the unsaved dialog, Esc order with the binding dialog, and test coverage of the
  dialog under the overlay. Left as known: conflict matching by path string, and
  `RebuildRows` rebuilding display strings for each conflict lookup (only on rebind/refresh).
- **Not verified:** Play mode with real devices, HOTAS, HMD, Windows build, and long conflict text
  on narrow screens.
