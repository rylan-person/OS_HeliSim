# Flight and session regression coverage

The release player starts in MainMenu and flies in Multiplayer. This inventory covers the systems reachable from those scenes and their player, spectator, dashboard and course prefabs. Test sources are under [Assets/Code/Tests/Editor](../Assets/Code/Tests/Editor/). Use the project's [verification runner](TESTING.md) to preserve saved authoring scenes.

The fixtures compile in the Editor assembly. A normal NUnit case checks data, production methods or serialized references in Edit mode. A Unity coroutine that yields `EnterPlayMode` runs the game loop. Some of those tests deliberately use a local physics scene, synthetic input devices, an isolated aircraft or loopback network peers. Those choices determine what the result establishes.

| System | Edit-mode coverage | Play-mode coverage |
| --- | --- | --- |
| Main menu, hardware status, shared settings and launch | Menu assets and theme, keyboard focus, page routing, dirty-state dialogs, settings validation, launch/recording options and input rebinding | MainMenuFlowTests opens pages before a helicopter exists and exercises launch, cancellation and return. |
| Keyboard, joystick and throttle | Shipped action asset, processors, control conflicts, defaults, persisted overrides and fallback bindings | FlightControlPlayModeTests routes device events through PlayerInput on the isolated R66 and runs the flight controller, engine, gearbox and rotors across physics ticks. |
| Flight assistance and trim | FlightAssistanceTests checks preset configuration, SAS response, deadbands, directions, trim limits and baseline restoration | A running AutoTrim responds to body rates over fixed ticks, yields to pilot input and returns to manual. |
| Waypoints, sectors and lap timing | Shipped course geometry and endpoint wiring, gate order and eligibility, best/average times, resets and display formatting | WaypointCoursePhysicsTests moves the R66 collider shapes and Rigidbody through the shipped gate triggers, checks rejected out-of-order entry, all fifteen crossings, five sectors and a persisted finished lap. |
| Session clock, aircraft reset and collective lock | Menu prefab command wiring, page validation and serialized aircraft dependencies | MainMenuFlowTests runs the reset coroutine, checks the body pose and collective lock, and resumes the countdown through the pilot control. |
| Direct-IP multiplayer and spectator roles | Role approval, address/port validation, local ownership, source switching and cleanup | MultiplayerIntegrationTests connects independent peers through loopback, including late joins, disconnections and restart. MainMenuFlowTests checks the menu-to-session handoff. |
| Dashboard, cameras, HUD and map | Dashboard slots and registry, built-in panel wiring, R66 instrument references, tracked targets and timing labels | DashboardWorldPlayTests checks camera images, fullscreen layout, hidden cameras, RenderTexture lifetime, Street Map resources, map following, FPV target switching and loss, and instrument readouts. |
| World, sound and display | Validation of streaming limits, settings persistence, display modes and scale | Runtime scene settings exercise a Cesium tileset and audio volume. Offline/online presentation and isolated UI scaling can be exercised without changing the workstation display. |
| Telemetry and flight recording | FlightTelemetryTests checks input callbacks and the CSV schema. MenuRecordingLifecycleTests covers flush-on-disable and retry. | The session setup creates a recorder on the local helicopter. Live samples, persisted CSV, retention and network source selection are checked on running objects. |
| Seated VR and motion integration | R66 seat and camera references, absence of NOVA from menu/spectator prefabs, and offline basic motion cueing direction and acceleration limits | A synthetic tracked eye recenters relative to the aircraft. A NOVA component with no body stays inactive and opens no socket across fixed ticks. |

### Flight CSV compatibility

In v0.1.4 the flight CSV retains its 18-column header and units. The
`pedal_input` column now records the signed value of the `Yaw Input` action,
including its return to neutral. In earlier builds that column could stay at
zero throughout a flight because the recorder had no yaw callback. Numeric
values use a period as the decimal separator in every locale.

### Coverage boundaries

The course test moves the collision body along a repeatable approach at each gate.
The separate R66 control test uses the aircraft engine, transmission and rotors,
with synthetic devices and a gravity-free test setup to measure control response.
The complete course has not been flown by a human pilot as part of these checks.

Loopback verifies local network behavior. Operating a physical NOVA, measuring control calibration or handling fidelity, seated HMD tracking and testing between machines require separate acceptance. A local tileset or map fixture does not measure the availability of a live imagery service.

Disabled development scenes, third-party sample scenes and scripts without a release launch path are outside this inventory. Examples include the commented-out collective override, the yaw-override prototype and standalone journey playback tools. A passing feature row provides evidence for the stated scenarios, not every possible interaction or failure.
