# Multiplayer flight controls: debugging and verification

Verified with Unity 6000.4.5f1 on 30 September 2026.

## Changes

- Reconnected `R66_Default`'s `PlayerInput` to `DefaultHeliAction`. Multiplayer spawns this prefab. Its missing action asset prevented flight callbacks and made the controls menu edit its disconnected preview instead of live flight actions.
- Allowed mouse buttons to replace keyboard bindings, including individual composite directions, and preserved those assignments when loading saved overrides. Desktop control availability and Escape recovery remain protected.
- Corrected the keyboard throttle composite's clamp to accept its negative direction. The hardware bindings retain their existing processing.

## Verification

All 14 added regression cases passed: prefab wiring, both directions of the five shipped flight axes, interactive keyboard rebinding with live input, mouse composite input/save/load, and mouse command save/load with recovery protection.

The input fixture run passed 28 of 29 cases. The remaining existing case, `InputRebindingServiceTests.DeviceTabs_RejectControlsFromTheOtherTab`, fails when creating a virtual gamepad because OpenXR registers `Dpad` as a device layout.

An isolated editor check delivered synthetic keyboard events through `PlayerInput` BroadcastMessages to the existing `RotaryController` callbacks. Pitch, roll and yaw returned to zero on release. The controller was disabled to prevent simulation updates; no NOVA component was instantiated.

The synchronous project EditMode run passed 192 of 204 cases. The following existing tests failed; multi-frame tests were excluded by the synchronous runner:

- `ControlsRebindingPageTests.JoystickDirectionalComposite_KeepsBothDirectionsAssignable`
- `InputRebindingServiceTests.DeviceTabs_RejectControlsFromTheOtherTab`
- `Mapbox.CheapRulerCs.UnitTest.CheapRulerCsTest.FixturesLoaded`
- `Mapbox.CheapRulerCs.UnitTest.CheapRulerCsTest.DistanceInMiles`
- `Mapbox.CheapRulerCs.UnitTest.CheapRulerCsTest.DistanceInNauticalMiles`
- `Mapbox.CheapRulerCs.UnitTest.CheapRulerCsTest.FromTile`
- `Mapbox.MapboxSdkCs.UnitTest.ForwardGeocodeResourceTest.GetUrl`
- `Mapbox.MapboxSdkCs.UnitTest.SQLiteCacheTest.VerifyTilesFromConcurrentInsert`
- `Mapbox.ProbeExtractorCs.UnitTest.ProbeExtractorCsTest.FixturesLoaded`
- `Mapbox.ProbeExtractorCs.UnitTest.ProbeExtractorCsTest.ExtractFootTrace`
- `Mapbox.ProbeExtractorCs.UnitTest.ProbeExtractorCsTest.ExtractProbes`
- `SettingsToggleVisualTests.Refresh_HighlightsOnlyTheSelectedSideAndFollowsToggleChanges`

Full Multiplayer Play-mode flight, remote-client ownership, and physical hardware operation remain unverified.

## Play-mode follow-up

Start Multiplayer as a host with the player role. Check W/S pitch, A/D roll, Q/E yaw, Page Up/Page Down collective, 1/2 throttle, and F1 engine start. In the controls menu, assign a different key and a mouse button to a flight direction. Close the menu and check live response, then start a new session and check persistence. Restore the profile and confirm default bindings return.
