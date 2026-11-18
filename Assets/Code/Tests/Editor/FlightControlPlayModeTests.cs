using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Oyedoyin.Common;
using Oyedoyin.RotaryWing;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

/// <summary>
/// Run from EditMode through MultiplayerVerificationRunner. Each case enters real
/// Play mode in an empty scene and uses the shipped R66 flight components, engine,
/// gearbox, rotors and dynamic Rigidbody. External integrations are removed before
/// activation; these tests never communicate with hardware or start networking.
/// </summary>
public sealed class FlightControlPlayModeTests
{
    private const string SetupKey = "HeliSim.FlightControlPlayModeTests.Scenes";
    private const string FolderKey = "HeliSim.FlightControlPlayModeTests.Folder";
    private const string StickLayout = "FlightControlPlayModeJoystick";
    private GameObject container;
    private RotaryController controller;
    private PlayerInput player;
    private Keyboard keyboard;
    private Joystick joystick;
    private InputActionAsset actions;
    private InputRebindingService service;
    private bool registeredLayout;
    private System.Collections.Generic.List<InputDevice> factoryDevices;
    private System.Collections.Generic.List<string> factoryLayouts;
    private InputSettings originalInputSettings;
    private InputSettings testInputSettings;
    private int sampleSerial;

    [Serializable] private sealed class SceneRecord
    {
        public string path;
        public bool isLoaded;
        public bool isActive;
    }

    [Serializable] private sealed class SavedSetup { public SceneRecord[] scenes; }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Assert.That(EditorApplication.isPlaying, Is.False);
        Assert.That(SessionState.GetString(FolderKey, ""), Is.Empty, "An earlier flight integration test needs cleanup.");
        string authoring = SessionState.GetString(MultiplayerVerificationRunner.AuthoringSetupKey, "");
        Assert.That(authoring, Is.Not.Empty, "Use MultiplayerVerificationRunner to preserve saved authoring scenes.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            Assert.That(SceneManager.GetSceneAt(i).isDirty, Is.False);
        SessionState.SetString(SetupKey, authoring);
        string folder = "Assets/__FlightControlIntegration_" + Guid.NewGuid().ToString("N");
        SessionState.SetString(FolderKey, folder);
        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Assert.That(EditorSceneManager.SaveScene(scene, folder + "/Isolated.unity"), Is.True);
        yield return new EnterPlayMode();

        service = null;
        joystick = null;
        registeredLayout = false;
        sampleSerial = 0;
        factoryDevices = new System.Collections.Generic.List<InputDevice>();
        factoryLayouts = new System.Collections.Generic.List<string>();
        originalInputSettings = InputSystem.settings;
        testInputSettings = Object.Instantiate(originalInputSettings);
        testInputSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings = testInputSettings;
        container = new GameObject("Flight input integration isolation");
        container.SetActive(false);
        container.AddComponent<AudioListener>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Helicopters/R66_Default.prefab");
        Assert.That(prefab, Is.Not.Null);
        GameObject helicopter = Object.Instantiate(prefab, container.transform);
        helicopter.transform.SetPositionAndRotation(new Vector3(0, 100, 0), Quaternion.identity);
        controller = helicopter.GetComponent<RotaryController>();
        player = helicopter.GetComponent<PlayerInput>();
        Assert.That(controller, Is.Not.Null);
        Assert.That(player, Is.Not.Null);
        // Prevent all external starts/Awake handlers before making the prefab active.
        // Flight components are driven by RotaryController's normal Update/FixedUpdate.
        var external = new System.Collections.Generic.List<MonoBehaviour>();
        foreach (MonoBehaviour behaviour in helicopter.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null || behaviour == controller || behaviour == player) continue;
            if (behaviour.GetType().Namespace != null && behaviour.GetType().Namespace.StartsWith("Oyedoyin", StringComparison.Ordinal))
                behaviour.enabled = false;
            else
                external.Add(behaviour);
        }
        while (external.Count > 0)
        {
            int before = external.Count;
            for (int i = external.Count - 1; i >= 0; i--)
            {
                MonoBehaviour candidate = external[i];
                if (candidate != null && IsRequired(candidate)) continue;
                if (candidate != null) Object.DestroyImmediate(candidate);
                external.RemoveAt(i);
            }
            Assert.That(external.Count, Is.LessThan(before), "External component dependency cycle prevents safe fixture activation.");
        }
        // Presentation is not part of the input/physics scenario. Removing its driver
        // also prevents camera initialization from enabling spectator or XR views.
        foreach (SilantroCamera view in helicopter.GetComponentsInChildren<SilantroCamera>(true))
            Object.DestroyImmediate(view);
        foreach (Camera camera in helicopter.GetComponentsInChildren<Camera>(true)) camera.enabled = false;
        foreach (AudioListener listener in helicopter.GetComponentsInChildren<AudioListener>(true)) listener.enabled = false;

        actions = Object.Instantiate(player.actions);
        player.actions = actions;
        player.neverAutoSwitchControlSchemes = true;
        keyboard = InputSystem.AddDevice<Keyboard>();
        controller.m_startMode = Controller.StartMode.Cold;
        controller.m_inputType = Controller.InputType.Default;
        controller.m_inputLogic = Controller.InputLogic.InputSystem;
        helicopter.GetComponentInChildren<RotaryComputer>(true).m_mode = Computer.Mode.Manual;
        Rigidbody body = helicopter.GetComponent<Rigidbody>();
        body.useGravity = false;
        body.isKinematic = false;
        body.constraints = RigidbodyConstraints.None;
        container.SetActive(true);
        yield return null;
        Assert.That(controller.isInitialized && controller.allOk, Is.True, "Real prefab prerequisites must initialize.");
        Assert.That(controller.m_shafts, Has.Length.GreaterThan(0));
        Assert.That(controller.m_rotors, Has.Length.GreaterThanOrEqualTo(2));
        InputUser user = player.user;
        user.UnpairDevices();
        InputUser.PerformPairingWithDevice(keyboard, user);
        service = new InputRebindingService(player.actions, new MemoryStore(), new MemoryYawStore());
        foreach (AudioSource source in helicopter.GetComponentsInChildren<AudioSource>(true)) source.mute = true;
        player.ActivateInput();
        RestrictToOwnedDevices();
        // Initialization observes the unconstrained dynamic body. Hold the
        // initialized airframe while the real engine and input checks spool up;
        // each motion sample restores a dynamic body before measuring physics.
        Vector3 initializedInertia = body.inertiaTensor;
        Assert.That(initializedInertia.x > 0 && initializedInertia.y > 0 && initializedInertia.z > 0, Is.True);
        body.isKinematic = true;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlaying)
        {
            service?.Dispose();
            if (container != null) Object.Destroy(container);
            if (actions != null) Object.Destroy(actions);
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            if (joystick != null) InputSystem.RemoveDevice(joystick);
            if (factoryDevices != null)
                foreach (InputDevice device in factoryDevices) InputSystem.RemoveDevice(device);
            if (factoryLayouts != null)
                foreach (string layout in factoryLayouts) InputSystem.RemoveLayout(layout);
            if (registeredLayout) InputSystem.RemoveLayout(StickLayout);
            if (originalInputSettings != null) InputSystem.settings = originalInputSettings;
            if (testInputSettings != null) Object.Destroy(testInputSettings);
            yield return null;
            yield return new ExitPlayMode();
        }
        string folder = SessionState.GetString(FolderKey, "");
        string setup = SessionState.GetString(SetupKey, "");
        if (!string.IsNullOrEmpty(setup))
        {
            SavedSetup saved = JsonUtility.FromJson<SavedSetup>(setup);
            EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(scene => new SceneSetup
            {
                path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive
            }).ToArray());
        }
        if (!string.IsNullOrEmpty(folder)) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True);
        SessionState.EraseString(SetupKey);
        SessionState.EraseString(FolderKey);
    }

    [UnityTest]
    public IEnumerator Keyboard_StartsRealEngineAndDrivesRotorAndPhysics()
    {
        return Sequential(KeyboardScenario());
    }

    private IEnumerator KeyboardScenario()
    {
        QueueKeys(Key.F1, Key.Digit1, Key.PageDown);
        yield return WaitForRunningEngine();
        QueueKeys(Key.Digit1, Key.PageDown);
        yield return Ticks(3);
        Assert.That(controller._throttleInput, Is.GreaterThan(0.99f));
        Assert.That(controller.m_shafts[0].throttle, Is.GreaterThan(0.99));
        yield return CheckFlightDirections(false);
        yield return CheckAttitudeResponse(false);
        yield return CheckLiftAndStop(false);
    }

    [UnityTest]
    public IEnumerator ReboundJoystick_StartsRealEngineAndDrivesRotorAndPhysicsWithKeyboardFallback()
    {
        return Sequential(JoystickScenario());
    }

    private IEnumerator JoystickScenario()
    {
        InputSystem.RegisterLayout("{\"name\":\"" + StickLayout + "\",\"extend\":\"Joystick\",\"controls\":[" +
            "{\"name\":\"collective\",\"layout\":\"Axis\",\"format\":\"FLT\"}," +
            "{\"name\":\"throttle\",\"layout\":\"Axis\",\"format\":\"FLT\"}," +
            "{\"name\":\"pedal\",\"layout\":\"Axis\",\"format\":\"FLT\"}]}");
        registeredLayout = true;
        joystick = (Joystick)InputSystem.AddDevice(StickLayout);
        Bind("Pitch Input", "stick/y");
        Bind("Roll Input", "stick/x");
        Bind("Collective Lever", "collective");
        Bind("Throttle Lever", "throttle");
        Bind("Start Engine Global", "trigger");
        service.SetYawBindingMode(YawBindingMode.Axis);
        Bind("Yaw Input", "pedal");
        // Exercise persistence on the same live PlayerInput before real events.
        player.actions.RemoveAllBindingOverrides();
        service.Load();
        service.PairConnectedDevices(joystick);
        QueueAxis("throttle", 1f);
        QueueAxis("collective", -1f);
        QueueButton(joystick.trigger, 1f);
        yield return WaitForRunningEngine();
        QueueButton(joystick.trigger, 0f);
        yield return CheckFlightDirections(true);
        yield return CheckAttitudeResponse(true);
        yield return CheckLiftAndStop(true);
        Assert.That(joystick, Is.Not.Null, "Joystick scenario must finish all samples before disposing its device.");
        TestContext.WriteLine("Joystick disposal after completed sample serial " + sampleSerial + "; device=" + joystick.deviceId);
        InputSystem.RemoveDevice(joystick);
        joystick = null;
        RestrictToOwnedDevices();
        QueueKeys(Key.W, Key.D, Key.E);
        yield return Ticks(3);
        Assert.That(controller.m_computer.m_pitch, Is.EqualTo(1).Within(0.01));
        Assert.That(controller.m_computer.m_roll, Is.EqualTo(-1).Within(0.01));
        Assert.That(controller.m_computer.m_yaw, Is.EqualTo(1).Within(0.01));
        QueueKeys();
        yield return Ticks(3);
        Assert.That(controller.m_computer.m_pitch, Is.Zero.Within(0.01));
        Assert.That(controller.m_computer.m_roll, Is.Zero.Within(0.01));
        Assert.That(controller.m_computer.m_yaw, Is.Zero.Within(0.01));
        yield return CheckFactoryDeviceCallbacks();
    }

    private IEnumerator WaitForRunningEngine()
    {
        float deadline = Time.realtimeSinceStartup + 45f;
        SilantroTurboshaft engine = controller.m_shafts[0];
        while ((engine.state != SilantroTurboshaft.State.RUN || controller.m_gearbox.m_primarySpeed <= 1 ||
            engine.m_RPM < engine.designRPM * 0.85) && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(engine.state, Is.EqualTo(SilantroTurboshaft.State.RUN), "Normal engine start must complete in 45 seconds.");
        Assert.That(engine.m_RPM, Is.GreaterThan(1));
        Assert.That(controller.m_gearbox.m_primarySpeed, Is.GreaterThan(1));
        Assert.That(controller.m_gearbox.m_secondarySpeed, Is.GreaterThan(1));
        Assert.That(controller.m_gearbox.m_primary.Ω, Is.GreaterThan(1));
        Assert.That(engine.m_RPM, Is.GreaterThanOrEqualTo(engine.designRPM * 0.85), "Full-throttle stock engine must spool before comparing flight responses.");
    }

    private IEnumerator CheckFactoryDeviceCallbacks()
    {
        service.ResetProfile();
        QueueKeys();
        InputDevice warthog = AddFactoryDevice("HID::Thustmaster Joystick - HOTAS Warthog");
        InputDevice x52 = AddFactoryDevice("HID::Saitek Saitek X52 Flight Control System");
        foreach (InputDevice device in new[] { warthog, x52 })
        {
            for (int sign = -1; sign <= 1; sign += 2)
            {
                InputSystem.QueueDeltaStateEvent((AxisControl)device["stick/y"], (float)sign);
                InputSystem.QueueDeltaStateEvent((AxisControl)device["stick/x"], (float)sign);
                yield return Ticks(3);
                Assert.That(controller.m_input.m_pitchInput, Is.EqualTo(-sign).Within(0.01), device.layout);
                Assert.That(controller.m_input.m_rollInput, Is.EqualTo(sign).Within(0.01), device.layout);
                Assert.That(controller.m_computer.m_pitch, Is.EqualTo(sign).Within(0.01), device.layout);
                Assert.That(controller.m_computer.m_roll, Is.EqualTo(-sign).Within(0.01), device.layout);
            }
            InputSystem.QueueDeltaStateEvent((AxisControl)device["stick/y"], 0f);
            InputSystem.QueueDeltaStateEvent((AxisControl)device["stick/x"], 0f);
            yield return Ticks(3);
            Assert.That(controller.m_input.m_pitchInput, Is.Zero.Within(0.01));
            Assert.That(controller.m_input.m_rollInput, Is.Zero.Within(0.01));
        }
        InputDevice throttleDevice = AddFactoryDevice("HID::Thrustmaster Throttle - HOTAS Warthog",
            ",\"controls\":[{\"name\":\"z\",\"layout\":\"Axis\",\"format\":\"FLT\"}," +
            "{\"name\":\"slider\",\"layout\":\"Axis\",\"format\":\"FLT\"}," +
            "{\"name\":\"button16\",\"layout\":\"Button\",\"format\":\"FLT\"}]");
        InputSystem.QueueDeltaStateEvent((AxisControl)throttleDevice["z"], -1f);
        InputSystem.QueueDeltaStateEvent((AxisControl)throttleDevice["slider"], 1f);
        yield return Ticks(3);
        Assert.That(controller._collectiveInput, Is.Zero.Within(0.01));
        Assert.That(controller._throttleInput, Is.EqualTo(1).Within(0.01));
        QueueButton((ButtonControl)throttleDevice["button16"], 1f);
        yield return Ticks(3);
        Assert.That(controller.m_shafts[0].state, Is.Not.EqualTo(SilantroTurboshaft.State.OFF),
            "Factory throttle button must start the real initialized engine.");
        QueueButton((ButtonControl)throttleDevice["button16"], 0f);
        yield return WaitForRunningEngine();
        controller.TurnOffEngines();
        yield return Ticks(3);
        Assert.That(controller.m_shafts[0].state, Is.EqualTo(SilantroTurboshaft.State.OFF));
    }

    private InputDevice AddFactoryDevice(string layoutName, string extraJson = "")
    {
        if (!InputSystem.ListLayouts().Contains(layoutName))
        {
            InputSystem.RegisterLayout("{\"name\":\"" + layoutName + "\",\"extend\":\"Joystick\"" + extraJson + "}");
            factoryLayouts.Add(layoutName);
        }
        InputDevice device = InputSystem.AddDevice(layoutName);
        factoryDevices.Add(device);
        service.PairConnectedDevices(device);
        RestrictToOwnedDevices();
        return device;
    }

    private IEnumerator CheckFlightDirections(bool useJoystick)
    {
        for (int sign = -1; sign <= 1; sign += 2)
        {
            if (useJoystick)
            {
                QueueAxis("stick/y", sign);
                QueueAxis("stick/x", sign);
                QueueAxis("pedal", sign);
            }
            else QueueKeys(sign < 0 ? Key.S : Key.W, sign < 0 ? Key.A : Key.D,
                sign < 0 ? Key.Q : Key.E, Key.Digit1, Key.PageDown);
            yield return Ticks(3);
            string evidence = "sign=" + sign + "; pitch action=" + player.actions.FindAction("Pitch Input").ReadValue<float>() +
                "; Silantro input=" + controller.m_input.m_pitchInput + "; base=" + controller.m_computer.b_pitchInput +
                "; mode=" + controller.m_computer.m_mode + "; W/S=" + keyboard.wKey.isPressed + "/" + keyboard.sKey.isPressed;
            Assert.That(controller.m_computer.m_pitch, Is.EqualTo(sign).Within(0.01), evidence);
            Assert.That(controller.m_computer.m_roll, Is.EqualTo(-sign).Within(0.01));
            Assert.That(controller.m_computer.m_yaw, Is.EqualTo(sign).Within(0.01));
            Assert.That(controller.m_gearbox.m_primary.Bɪc, Is.EqualTo(controller.m_computer.Bɪc).Within(0.0001));
            Assert.That(controller.m_gearbox.m_primary.Aɪc, Is.EqualTo(controller.m_computer.Aɪc).Within(0.0001));
        }
        if (useJoystick)
        {
            QueueAxis("stick/y", 0);
            QueueAxis("stick/x", 0);
            QueueAxis("pedal", 0);
        }
        else QueueKeys(Key.Digit1, Key.PageDown);
        yield return Ticks(3);
        Assert.That(controller.m_computer.m_pitch, Is.Zero.Within(0.01));
        Assert.That(controller.m_computer.m_roll, Is.Zero.Within(0.01));
        Assert.That(controller.m_computer.m_yaw, Is.Zero.Within(0.01));
    }

    private IEnumerator CheckLiftAndStop(bool useJoystick)
    {
        MotionSample low = default;
        MotionSample high = default;
        yield return SampleMotion(useJoystick, 0, 0, 0, 0, value => low = value);
        double lowCollective = controller.m_computer.θom;
        yield return SampleMotion(useJoystick, 0, 0, 0, 1, value => high = value);
        Assert.That(controller.m_computer.θom, Is.GreaterThan(lowCollective));
        Assert.That(controller.m_gearbox.m_primary.θom, Is.GreaterThan(0));
        TestContext.WriteLine("Collective low/high force Y: " + low.Force.y + "/" + high.Force.y +
            "; acceleration Y: " + low.LinearAcceleration.y + "/" + high.LinearAcceleration.y +
            "; engine RPM: " + low.Rpm + "/" + high.Rpm);
        Assert.That(high.Force.y, Is.GreaterThan(low.Force.y), "Collective must increase upward aerodynamic force.");
        Assert.That(high.LinearAcceleration.y, Is.GreaterThan(low.LinearAcceleration.y),
            "Collective must increase upward acceleration of the actual dynamic Rigidbody.");
        Rigidbody body = controller.m_rigidbody;
        Assert.That(body.isKinematic, Is.False);
        Assert.That(float.IsNaN(body.linearVelocity.magnitude) || float.IsInfinity(body.linearVelocity.magnitude), Is.False);
        // Stop uses the existing supported simulator API. No new stop binding is invented.
        controller.TurnOffEngines();
        yield return Ticks(3);
        Assert.That(controller.m_shafts[0].state, Is.EqualTo(SilantroTurboshaft.State.OFF));
    }

    private struct MotionSample
    {
        public Vector3 AngularAcceleration;
        public Vector3 LinearAcceleration;
        public Vector3 Force;
        public double Rpm;
        public bool Rotated;
        public int Serial;
    }

    private IEnumerator CheckAttitudeResponse(bool useJoystick)
    {
        for (int axis = 0; axis < 3; axis++)
        {
            MotionSample neutral = default;
            MotionSample negative = default;
            MotionSample positive = default;
            yield return SampleMotion(useJoystick, 0, 0, 0, 0.5f, value => neutral = value);
            yield return SampleMotion(useJoystick, axis == 0 ? -1 : 0, axis == 2 ? -1 : 0,
                axis == 1 ? -1 : 0, 0.5f, value => negative = value);
            yield return SampleMotion(useJoystick, axis == 0 ? 1 : 0, axis == 2 ? 1 : 0,
                axis == 1 ? 1 : 0, 0.5f, value => positive = value);
            Assert.That(neutral.Serial, Is.GreaterThan(0), "Neutral sample callback must complete before comparisons.");
            Assert.That(negative.Serial, Is.EqualTo(neutral.Serial + 1));
            Assert.That(positive.Serial, Is.EqualTo(negative.Serial + 1));
            Assert.That(neutral.Rpm, Is.GreaterThan(0));
            Assert.That(negative.Rpm, Is.GreaterThan(0));
            Assert.That(positive.Rpm, Is.GreaterThan(0));
            Assert.That(negative.Rpm / neutral.Rpm, Is.InRange(0.85, 1.15), "Compare attitude samples at comparable stock-engine RPM.");
            Assert.That(positive.Rpm / neutral.Rpm, Is.InRange(0.85, 1.15), "Compare attitude samples at comparable stock-engine RPM.");
            float minus = negative.AngularAcceleration[axis] - neutral.AngularAcceleration[axis];
            float plus = positive.AngularAcceleration[axis] - neutral.AngularAcceleration[axis];
            TestContext.WriteLine("Angular axis " + axis + " neutral/minus/plus acceleration: " +
                neutral.AngularAcceleration[axis] + "/" + negative.AngularAcceleration[axis] + "/" + positive.AngularAcceleration[axis]);
            Assert.That(minus * plus, Is.LessThan(0f), "Opposing pilot inputs must produce opposite angular-acceleration changes around neutral.");
            Assert.That(negative.Rotated && positive.Rotated, Is.True, "Released Rigidbody constraints must permit actual attitude changes.");
        }
    }

    private IEnumerator SampleMotion(bool useJoystick, float pitch, float roll, float yaw, float collective,
        Action<MotionSample> completed)
    {
        int serial = ++sampleSerial;
        Rigidbody body = controller.m_rigidbody;
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints.None));
        if (body.isKinematic) body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        // Hold only while the input/rotor equations settle. Never freeze rotation:
        // constraints can affect the inertia reported during stock initialization.
        body.isKinematic = true;
        body.position = new Vector3(0, 100, 0);
        body.rotation = Quaternion.identity;
        SetPilotInput(useJoystick, pitch, roll, yaw, collective);
        // Settle the actual stock rotor/flapping calculation at a controlled pose.
        // No engine, rotor, force or inertia parameters are changed by the fixture.
        yield return Ticks(20);
        MotionSample sample = new MotionSample
        {
            // Conventional main-rotor lift is applied directly at its hub;
            // RotaryController intentionally clears centralForce.y afterwards.
            Force = controller.m_gearbox.m_primary.m_force.y * controller.m_gearbox.m_primary.transform.up,
            Rpm = controller.m_shafts[0].m_RPM,
            Serial = serial
        };
        Assert.That(sample.Rpm, Is.GreaterThan(0), "Each motion sample must capture an actually running engine.");
        body.isKinematic = false;
        body.position = new Vector3(0, 100, 0);
        body.rotation = Quaternion.identity;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        Vector3 inertia = body.inertiaTensor;
        Assert.That(inertia.x > 0 && inertia.y > 0 && inertia.z > 0, Is.True, "Stock initialized body must have positive unconstrained inertia.");
        Assert.That(body.maxAngularVelocity, Is.GreaterThan(0));
        float started = Time.fixedTime;
        body.WakeUp();
        TestContext.WriteLine("Sample " + serial + " start fixedTime=" + started + "; RPM=" + sample.Rpm +
            "; moment=" + controller.centralMoment.ToString("F6") + "; force=" + sample.Force.ToString("F6") +
            "; kinematic=" + body.isKinematic + "; sleeping=" + body.IsSleeping() + "; physics=" + Physics.simulationMode +
            "; inertia=" + inertia.ToString("F6") + "; mass=" + body.mass + "; maxAngularVelocity=" + body.maxAngularVelocity +
            "; angularDamping=" + body.angularDamping + "; constraints=" + body.constraints);
        yield return Ticks(3);
        float elapsed = Time.fixedTime - started;
        Assert.That(elapsed, Is.GreaterThan(0));
        Assert.That(body.constraints, Is.EqualTo(RigidbodyConstraints.None), "No other sample may freeze this sample's body.");
        Assert.That(body.isKinematic, Is.False);
        Assert.That(Physics.simulationMode, Is.EqualTo(SimulationMode.FixedUpdate), "Real automatic fixed-step physics is required.");
        sample.AngularAcceleration = body.angularVelocity / elapsed;
        sample.LinearAcceleration = body.linearVelocity / elapsed;
        Quaternion attitude = body.rotation;
        sample.Rotated = Mathf.Abs(attitude.x) + Mathf.Abs(attitude.y) + Mathf.Abs(attitude.z) > 0f;
        TestContext.WriteLine("Sample " + serial + " end fixedTime=" + Time.fixedTime + "; elapsed=" + elapsed +
            "; angular velocity=" + body.angularVelocity.ToString("F6") + "; linear velocity=" + body.linearVelocity.ToString("F6") +
            "; moment=" + controller.centralMoment.ToString("F6") + "; sleeping=" + body.IsSleeping() + "; rotated=" + sample.Rotated);
        Assert.That(float.IsNaN(sample.AngularAcceleration.magnitude) || float.IsInfinity(sample.AngularAcceleration.magnitude), Is.False);
        completed(sample);
    }

    private void SetPilotInput(bool useJoystick, float pitch, float roll, float yaw, float collective)
    {
        if (useJoystick)
        {
            QueueAxis("stick/y", pitch);
            QueueAxis("stick/x", roll);
            QueueAxis("pedal", yaw);
            QueueAxis("collective", 2f * collective - 1f);
            QueueAxis("throttle", 1f);
            return;
        }
        var keys = new System.Collections.Generic.List<Key> { Key.Digit1 };
        if (pitch != 0) keys.Add(pitch > 0 ? Key.W : Key.S);
        if (roll != 0) keys.Add(roll > 0 ? Key.D : Key.A);
        if (yaw != 0) keys.Add(yaw > 0 ? Key.E : Key.Q);
        if (collective == 0) keys.Add(Key.PageDown);
        if (collective == 1) keys.Add(Key.PageUp);
        QueueKeys(keys.ToArray());
    }

    private void Bind(string actionName, string control)
    {
        var rows = service.GetBindingRows(actionName, BindingDeviceTab.Joystick);
        Assert.That(rows, Is.Not.Empty, actionName);
        Assert.That(service.TryApplyJoystickBindingPath(actionName, rows[0].BindingIndex,
            "<" + StickLayout + ">/" + control, out string error), Is.True, error);
    }

    private static bool IsRequired(Component candidate)
    {
        Type type = candidate.GetType();
        foreach (Component dependent in candidate.GetComponents<Component>())
        {
            if (dependent == null || dependent == candidate) continue;
            foreach (RequireComponent requirement in dependent.GetType().GetCustomAttributes(typeof(RequireComponent), true))
            {
                if ((requirement.m_Type0 != null && requirement.m_Type0.IsAssignableFrom(type)) ||
                    (requirement.m_Type1 != null && requirement.m_Type1.IsAssignableFrom(type)) ||
                    (requirement.m_Type2 != null && requirement.m_Type2.IsAssignableFrom(type))) return true;
            }
        }
        return false;
    }

    private void QueueAxis(string control, float value)
    {
        Assert.That(joystick, Is.Not.Null, "The virtual joystick must remain alive through the entire flight sample.");
        RestrictToOwnedDevices();
        AxisControl axis = (AxisControl)joystick[control];
        InputSystem.QueueDeltaStateEvent(axis, value);
        InputSystem.Update();
        Assert.That(axis.ReadValue(), Is.EqualTo(value).Within(0.0001f), "Injected axis state must reach the virtual device immediately.");
    }

    private void QueueKeys(params Key[] keys)
    {
        RestrictToOwnedDevices();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
        foreach (Key key in keys) Assert.That(keyboard[key].isPressed, Is.True, "Injected keyboard state must arrive immediately: " + key);
    }

    private void QueueButton(ButtonControl button, float value)
    {
        RestrictToOwnedDevices();
        // The EditMode runner may resume while the editor input state buffer is
        // current. Select/process the player buffer before copying whole device
        // state; otherwise a button-only edit can reset held flight axes to the
        // editor buffer's neutral values.
        InputSystem.Update();
        AxisControl slider = button.device.TryGetChildControl<AxisControl>("slider");
        float beforeSlider = slider != null ? slider.ReadValue() : 0f;
        float beforeThrottle = controller.m_input._throttleInput;
        TestContext.WriteLine("Button " + button.path + "=" + value + " before: slider=" + beforeSlider +
            "; input throttle=" + beforeThrottle + "; controller throttle=" + controller._throttleInput +
            "; engine throttle=" + controller.m_shafts[0].throttle);
        using (StateEvent.From(button.device, out InputEventPtr stateEvent))
        {
            button.WriteValueIntoEvent(value, stateEvent);
            InputSystem.QueueEvent(stateEvent);
        }
        InputSystem.Update();
        Assert.That(button.isPressed, Is.EqualTo(value > 0.5f), "Whole-device state events must support bit-packed joystick buttons.");
        if (slider != null) Assert.That(slider.ReadValue(), Is.EqualTo(beforeSlider).Within(0.0001f), "Editing a button must preserve the throttle slider state.");
        Assert.That(controller.m_input._throttleInput, Is.EqualTo(beforeThrottle).Within(0.0001f), "A button event must not change flight throttle input.");
        TestContext.WriteLine("Button " + button.path + "=" + value + " after: slider=" + (slider != null ? slider.ReadValue() : 0f) +
            "; input throttle=" + controller.m_input._throttleInput + "; controller throttle=" + controller._throttleInput +
            "; engine throttle=" + controller.m_shafts[0].throttle);
    }

    private void RestrictToOwnedDevices()
    {
        var devices = new System.Collections.Generic.List<InputDevice> { keyboard };
        if (joystick != null) devices.Add(joystick);
        if (factoryDevices != null) devices.AddRange(factoryDevices);
        player.actions.devices = devices.ToArray();
    }

    private static IEnumerator Ticks(int count)
    {
        float target = Time.fixedTime + count * Time.fixedDeltaTime;
        float deadline = Time.realtimeSinceStartup + 10f;
        while (Time.fixedTime < target && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Time.fixedTime, Is.GreaterThanOrEqualTo(target), "Real player FixedUpdate ticks must advance within ten seconds.");
    }

    // The EditMode runner crosses into Play mode but still owns the test iterator.
    // Flatten helpers ourselves so it only observes null frame waits, never a
    // nested routine whose scheduling or disposal can overlap another sample.
    private static IEnumerator Sequential(IEnumerator scenario)
    {
        var pending = new System.Collections.Generic.Stack<IEnumerator>();
        pending.Push(scenario);
        try
        {
            while (pending.Count > 0)
            {
                IEnumerator current = pending.Peek();
                if (!current.MoveNext())
                {
                    pending.Pop();
                    (current as IDisposable)?.Dispose();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                {
                    pending.Push(nested);
                    continue;
                }
                Assert.That(current.Current, Is.Null, "Flight scenarios must yield only real-frame waits.");
                yield return null;
            }
        }
        finally
        {
            while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose();
        }
    }

    private sealed class MemoryStore : IBindingOverrideStore
    {
        private string json;
        public string Load() => json;
        public void Save(string value) => json = value;
        public void Clear() => json = null;
    }

    private sealed class MemoryYawStore : IYawBindingModeStore
    {
        private YawBindingMode mode;
        public YawBindingMode Load() => mode;
        public void Save(YawBindingMode value) => mode = value;
        public void Clear() => mode = YawBindingMode.Buttons;
    }
}
