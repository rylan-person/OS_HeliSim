using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using HeliSim.FrontEnd;
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

// Isolated recorders only. No scene helicopters, hardware, PlayerPrefs or existing CSVs are modified.
public sealed class FlightTelemetryTests
{
    private const string SceneStateKey = "HeliSim.FlightTelemetryTests.SceneSetup";
    private const string SceneFolderKey = "HeliSim.FlightTelemetryTests.SceneFolder";
    [Serializable] private sealed class SavedSetup { public SavedScene[] scenes; }
    [Serializable] private sealed class SavedScene { public string path; public bool isLoaded; public bool isActive; }
    private GameObject root;
    private DataExtractor recorder;
    private InputActionAsset actions;
    private Keyboard keyboard;
    private Joystick joystick;
    private InputSettings originalSettings;
    private InputSettings testSettings;
    private readonly List<string> ownedCsv = new List<string>();
    private CultureInfo originalCulture;
    private ClientTelemetryCollector originalCollector;
    private string outputDirectory;
    private string joystickLayout;
    private const string Header = "time,xJoy_input,yJoy_input,pedal_input,collective_input,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,pitch_rate,roll_rate,yaw_rate,turn_rate,vel_x,vel_y,vel_z";

    [SetUp]
    public void SetUp()
    {
        originalCulture = CultureInfo.CurrentCulture;
        originalCollector = ClientTelemetryCollector.ActiveCollector;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        outputDirectory = Path.GetFullPath(Path.Combine("TestResults", "Telemetry-" + Guid.NewGuid().ToString("N")));
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (root != null)
            foreach (var ownedRecorder in root.GetComponentsInChildren<DataExtractor>(true)) ownedRecorder.isRecording = false;
        if (root != null) Object.DestroyImmediate(root);
        if (actions != null) Object.DestroyImmediate(actions);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        if (joystick != null) InputSystem.RemoveDevice(joystick);
        if (joystickLayout != null) InputSystem.RemoveLayout(joystickLayout);
        if (originalSettings != null) InputSystem.settings = originalSettings;
        if (testSettings != null) Object.DestroyImmediate(testSettings);
        ClientTelemetryCollector.SetActiveCollector(originalCollector);
        CultureInfo.CurrentCulture = originalCulture;
        foreach (string file in ownedCsv) if (File.Exists(file)) File.Delete(file);
        ownedCsv.Clear();
        if (Directory.Exists(outputDirectory) && Directory.GetFileSystemEntries(outputDirectory).Length == 0)
            Directory.Delete(outputDirectory);
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        string setupJson = SessionState.GetString(SceneStateKey, "");
        string folder = SessionState.GetString(SceneFolderKey, "");
        if (!string.IsNullOrEmpty(setupJson))
        {
            var scenes = JsonUtility.FromJson<SavedSetup>(setupJson).scenes;
            EditorSceneManager.RestoreSceneManagerSetup(scenes.Select(scene => new SceneSetup
                { path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive }).ToArray());
        }
        if (!string.IsNullOrEmpty(folder)) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "Temporary telemetry scene cleanup failed.");
        SessionState.EraseString(SceneStateKey);
        SessionState.EraseString(SceneFolderKey);
    }

    [TestCase(Key.S, 2, 1f)]
    [TestCase(Key.D, 1, 1f)]
    [TestCase(Key.Q, 3, -1f)]
    [TestCase(Key.E, 3, 1f)]
    public void KeyboardActionValuesAndRecorderReceiverContract(Key key, int column, float expected)
    {
        CreateEditorActions();
        string actionName = column == 1 ? "Roll Input" : column == 2 ? "Pitch Input" : "Yaw Input";
        InputAction action = actions.FindAction(actionName, true);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
        InputSystem.Update();
        Assert.That(action.ReadValue<float>(), Is.EqualTo(expected).Within(0.0001f));
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        Assert.That(action.ReadValue<float>(), Is.Zero.Within(0.0001f));
        Assert.That(typeof(DataExtractor).GetMethod("On" + actionName.Replace(" ", ""), new[] { typeof(InputValue) }),
            Is.Not.Null, "The recorder must receive PlayerInput's shipped action message.");
    }

    [TestCase("Pitch Input", "stick/y", 2, -1f)]
    [TestCase("Roll Input", "stick/x", 1, 1f)]
    [TestCase("Yaw Input", "twist", 3, 1f)]
    [TestCase("Collective Lever", "throttle", 4, -1f)]
    public void JoystickActionValuesAndRecorderReceiverContract(string actionName, string path, int column, float expected)
    {
        CreateEditorActions();
        AddJoystick();
        actions.devices = new InputDevice[] { keyboard, joystick };
        InputAction action = actions.FindAction(actionName, true);
        // Override a physical slot, preserving shipped processors and PlayerInput callbacks.
        int slot = -1;
        for (int i = 0; i < action.bindings.Count; i++)
            if (!action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite &&
                (actionName != "Yaw Input" || action.bindings[i].groups == "YawAxis")) { slot = i; break; }
        Assert.That(slot, Is.GreaterThanOrEqualTo(0));
        action.ApplyBindingOverride(slot, "<Joystick>/" + path);
        var axis = (AxisControl)joystick[path];
        InputSystem.QueueDeltaStateEvent(axis, 1f);
        InputSystem.Update();
        Assert.That(action.ReadValue<float>(), Is.EqualTo(expected).Within(0.0001f));
        Assert.That(typeof(DataExtractor).GetMethod("On" + actionName.Replace(" ", ""), new[] { typeof(InputValue) }), Is.Not.Null);
    }

    [Test]
    public void CsvSchemaRemainsEighteenInvariantColumnsInCommaDecimalLocale()
    {
        root = new GameObject("Isolated telemetry CSV");
        recorder = root.AddComponent<DataExtractor>();
        recorder.OutputDirectory = outputDirectory;
        recorder.heliDataList.Add(new DataExtractor.HeliData
        {
            time = 1.25f, xJoy_input = -0.5f, rotation = Quaternion.identity,
            position = new Vector3(2.5f, 3.75f, -4.125f), pitch_rate = 0.125f
        });
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
        string[] lines = Save();
        Assert.That(lines[0], Is.EqualTo(Header));
        float[] row = Parse(lines[1]);
        Assert.That(row[0], Is.EqualTo(1.25f));
        Assert.That(row[1], Is.EqualTo(-0.5f));
        Assert.That(row[5], Is.EqualTo(2.5f));
    }

    [Test]
    public void FailedSaveRetainsSessionAndSuccessfulRetryFlushesTheSameSamples()
    {
        root = new GameObject("Isolated telemetry retry");
        root.SetActive(false);
        recorder = root.AddComponent<DataExtractor>();
        recorder.OutputDirectory = outputDirectory;
        recorder.heliDataList.Add(new DataExtractor.HeliData
            { time = 0.375f, pedal_input = -0.625f, rotation = Quaternion.identity });
        recorder.isRecording = true;
        Directory.CreateDirectory(outputDirectory);
        string blocker = Path.Combine(outputDirectory, "blocked-directory");
        File.WriteAllText(blocker, "Owned test file, preventing directory creation.");
        try
        {
            recorder.OutputDirectory = blocker;
            LogAssert.Expect(LogType.Error, new Regex("DataExtractor: failed to save recording"));
            Assert.That(recorder.TryStopRecording(out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
            Assert.That(recorder.isRecording, Is.True, "A storage failure must allow a later retry.");
            Assert.That(recorder.heliDataList.Count, Is.EqualTo(1));
            Assert.That(recorder.heliDataList[0].pedal_input, Is.EqualTo(-0.625f));
        }
        finally
        {
            recorder.OutputDirectory = outputDirectory;
            File.Delete(blocker);
        }
        string[] lines = Save(() => Assert.That(recorder.TryStopRecording(out string error), Is.True, error));
        Assert.That(lines.Length, Is.EqualTo(2));
        Assert.That(Parse(lines[1])[0], Is.EqualTo(0.375f));
        Assert.That(Parse(lines[1])[3], Is.EqualTo(-0.625f));
        Assert.That(recorder.isRecording, Is.False);
        Assert.That(recorder.heliDataList, Is.Empty);
    }

    [UnityTest]
    public IEnumerator RuntimeRecordingSamplesPhysicsUnitsAndDisablingFlushesLastSession()
    {
        PrepareIsolatedScene();
        yield return new EnterPlayMode();
        outputDirectory = Path.GetFullPath(Path.Combine("TestResults", "Telemetry-" + Guid.NewGuid().ToString("N")));
        root = new GameObject("Isolated runtime telemetry");
        root.SetActive(false);
        var controller = root.AddComponent<RotaryController>();
        controller.enabled = false;
        var core = root.AddComponent<SilantroCore>();
        core.enabled = false;
        var body = root.GetComponent<Rigidbody>();
        body.useGravity = false;
        root.transform.position = new Vector3(10, 20, 30);
        root.transform.rotation = Quaternion.Euler(12, 24, 36);
        Assert.That(HelicopterComponents.Instance, Is.Null, "Do not replace an existing helicopter singleton.");
        var components = root.AddComponent<HelicopterComponents>();
        components.rotaryController = controller;
        var setup = root.AddComponent<MenuGameplaySetup>();
        setup.Initialize(new MenuSessionOptions { Role = ClientRole.Player, RecordFlightData = true, AssistancePreset = -1 });
        root.SetActive(true);
        yield return null;
        yield return null; // The owning setup creates the recorder; its real Start captures origin and Rigidbody.
        recorder = root.GetComponent<DataExtractor>();
        if (recorder != null) recorder.OutputDirectory = outputDirectory;
        Assert.That(recorder, Is.Not.Null);
        Assert.That(recorder.isRecording, Is.True);
        Assert.That(setup.enabled, Is.False);
        PlayerInput player = AttachRuntimeInput();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.D, Key.E));
        InputSystem.Update();
        controller._collectiveInput = 0.625f;
        core.q = 0.25;
        core.p = -0.5;
        core.r = 0.75;
        core.ωф = -1.25;
        root.transform.position += new Vector3(2, 3, 4);
        body.linearVelocity = new Vector3(1, -2, 3);
        recorder.isRecording = true;
        float began = Time.time;
        yield return null;
        yield return null;
        Assert.That(recorder.heliDataList.Count, Is.GreaterThanOrEqualTo(1));
        DataExtractor.HeliData sample = recorder.heliDataList.Last();
        Assert.That(sample.time, Is.GreaterThanOrEqualTo(began));
        Assert.That(sample.time, Is.LessThanOrEqualTo(Time.time));
        Assert.That(sample.collective_input, Is.EqualTo(0.625f));
        Assert.That(sample.pitch_rate, Is.EqualTo(0.25f));
        Assert.That(sample.roll_rate, Is.EqualTo(-0.5f));
        Assert.That(sample.yaw_rate, Is.EqualTo(0.75f));
        Assert.That(sample.turn_rate, Is.EqualTo(-1.25f));
        Assert.That(Vector3.Distance(sample.position, root.transform.position - new Vector3(10, 20, 30)), Is.LessThan(0.1f));
        Assert.That(Vector3.Distance(sample.velocity, new Vector3(1, -2, 3)), Is.LessThan(0.001f));
        int count = recorder.heliDataList.Count;
        string[] first = Save(() => Assert.That(recorder.TryStopRecording(out string error), Is.True, error));
        Assert.That(first.Length, Is.EqualTo(count + 1));
        float[] row = Parse(first.Last());
        Assert.That(row[1], Is.EqualTo(1f), "Real PlayerInput roll callback must reach production Update and CSV.");
        Assert.That(row[2], Is.EqualTo(1f), "Real PlayerInput pitch callback must reach production Update and CSV.");
        Assert.That(row[3], Is.EqualTo(1f), "Real PlayerInput yaw callback must reach production Update and CSV.");
        Assert.That(row[8], Is.EqualTo(12f).Within(0.01f), "Rotation CSV units remain degrees.");
        Assert.That(row[11], Is.EqualTo(0.25f), "Core rate units must not be silently converted.");
        Assert.That(row[15], Is.EqualTo(1f).Within(0.001f));
        Assert.That(recorder.isRecording, Is.False);
        Assert.That(recorder.heliDataList, Is.Empty);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        InputSystem.Update();
        AddJoystick();
        InputUser.PerformPairingWithDevice(joystick, player.user);
        foreach (string name in new[] { "Pitch Input", "Roll Input", "Yaw Input", "Collective Lever" })
        {
            InputAction action = player.actions.FindAction(name, true);
            int slot = Enumerable.Range(0, action.bindings.Count).First(i =>
                !action.bindings[i].isComposite && !action.bindings[i].isPartOfComposite &&
                (name != "Yaw Input" || action.bindings[i].groups == "YawAxis"));
            string path = name == "Pitch Input" ? "stick/y" : name == "Roll Input" ? "stick/x" :
                name == "Yaw Input" ? "twist" : "throttle";
            action.ApplyBindingOverride(slot, "<" + joystickLayout + ">/" + path);
        }
        InputSystem.QueueDeltaStateEvent((AxisControl)joystick["stick/x"], 1f);
        InputSystem.QueueDeltaStateEvent((AxisControl)joystick["stick/y"], 1f);
        InputSystem.QueueDeltaStateEvent((AxisControl)joystick["twist"], -1f);
        InputSystem.QueueDeltaStateEvent((AxisControl)joystick["throttle"], 1f);
        InputSystem.Update();
        Assert.That(recorder.collective_input, Is.EqualTo(-1f), "Lever callback retains its processed bipolar value.");
        recorder.isRecording = true;
        yield return null;
        yield return null;
        string[] lever = Save(() => Assert.That(recorder.TryStopRecording(out string error), Is.True, error));
        float[] leverRow = Parse(lever.Last());
        Assert.That(leverRow[1], Is.EqualTo(1f));
        Assert.That(leverRow[2], Is.EqualTo(-1f));
        Assert.That(leverRow[3], Is.EqualTo(-1f));
        Assert.That(leverRow[4], Is.EqualTo(controller._collectiveInput),
            "Recorded collective is the authoritative normalized controller value, rather than the raw lever callback.");
        foreach (string path in new[] { "stick/x", "stick/y", "twist", "throttle" })
            InputSystem.QueueDeltaStateEvent((AxisControl)joystick[path], 0f);
        InputSystem.Update();
        recorder.isRecording = true;
        yield return null;
        yield return null;
        string[] last = Save(() => root.SetActive(false));
        Assert.That(last.Length, Is.GreaterThan(1), "Disabling must flush the final session.");
        Assert.That(Parse(last.Last()).Skip(1).Take(3), Is.All.EqualTo(0f), "Released input must also reach the saved final session.");
        Assert.That(recorder.isRecording, Is.False);
        Assert.That(recorder.heliDataList, Is.Empty);
        Assert.That(ownedCsv.Distinct().Count(), Is.EqualTo(3), "Rapid sessions must have distinct files.");
        Assert.That(File.ReadAllLines(ownedCsv[0]), Is.EqualTo(first), "A later session must never overwrite the first.");
    }

    [UnityTest]
    public IEnumerator CollectorUsesUnityTicksSignedDegreesRetentionAndMissingSourceFallback()
    {
        PrepareIsolatedScene();
        yield return new EnterPlayMode();
        root = new GameObject("Isolated collector ticks");
        root.SetActive(false);
        var computer = root.AddComponent<RotaryComputer>();
        computer.enabled = false;
        computer.m_roll = -0.625;
        computer.m_pitch = 0.375;
        var collector = root.AddComponent<ClientTelemetryCollector>();
        collector.autoAssignForLocalPlayer = false;
        collector.rotaryComputer = computer;
        collector.retentionSeconds = 0.1f;
        root.transform.rotation = Quaternion.Euler(-10, 220, -30);
        Vector3 expected = root.transform.eulerAngles;
        root.SetActive(true);
        float deadline = Time.realtimeSinceStartup + 3;
        while (collector.GetSamples(TelemetrySeries.XJoy).Count == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(collector.GetSamples(TelemetrySeries.XJoy), Is.Not.Empty);
        float firstTime = collector.GetSamples(TelemetrySeries.XJoy)[0].time;
        Assert.That(collector.GetSamples(TelemetrySeries.XJoy).Last().value, Is.EqualTo(-0.625f));
        Assert.That(collector.GetSamples(TelemetrySeries.YJoy).Last().value, Is.EqualTo(0.375f));
        Assert.That(collector.GetSamples(TelemetrySeries.RotationPitch).Last().value, Is.EqualTo(Signed(expected.x)).Within(0.001f));
        Assert.That(collector.GetSamples(TelemetrySeries.RotationYaw).Last().value, Is.EqualTo(Signed(expected.y)).Within(0.001f));
        Assert.That(collector.GetSamples(TelemetrySeries.RotationRoll).Last().value, Is.EqualTo(Signed(expected.z)).Within(0.001f));
        deadline = Time.realtimeSinceStartup + 3;
        while (Time.time <= firstTime + 0.25f && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Time.time, Is.GreaterThan(firstTime + 0.25f), "Actual simulation time must advance before checking retention.");
        yield return null;
        foreach (TelemetrySeries series in Enum.GetValues(typeof(TelemetrySeries)))
        {
            var samples = collector.GetSamples(series);
            Assert.That(samples, Is.Not.Empty);
            Assert.That(samples[0].time, Is.GreaterThan(firstTime), "Old values must expire on actual ticks.");
            Assert.That(samples.All(s => s.time >= Time.time - 0.12f && s.time <= Time.time), Is.True);
            Assert.That(samples.Select(s => s.time), Is.Ordered);
        }
        collector.rotaryComputer = null;
        collector.rotationTarget = null;
        float changedAt = Time.time;
        deadline = Time.realtimeSinceStartup + 3;
        while (collector.GetSamples(TelemetrySeries.XJoy).Last().time <= changedAt && Time.realtimeSinceStartup < deadline)
            yield return null;
        foreach (TelemetrySeries series in Enum.GetValues(typeof(TelemetrySeries)))
            Assert.That(collector.GetSamples(series).Last().value, Is.Zero);
        Assert.That(ClientTelemetryCollector.ActiveCollector, Is.EqualTo(originalCollector));
    }

    private void CreateEditorActions()
    {
        originalSettings = InputSystem.settings;
        testSettings = Object.Instantiate(originalSettings);
        testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
        InputSystem.settings = testSettings;
        keyboard = InputSystem.AddDevice<Keyboard>();
        actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Code/InputActions/DefaultHeliAction.inputactions"));
        actions.devices = new InputDevice[] { keyboard };
        actions.FindActionMap("Default", true).Enable();
    }

    private PlayerInput AttachRuntimeInput()
    {
        keyboard = InputSystem.AddDevice<Keyboard>();
        actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Code/InputActions/DefaultHeliAction.inputactions"));
        var player = root.AddComponent<PlayerInput>();
        player.actions = actions;
        player.defaultActionMap = "Default";
        player.notificationBehavior = PlayerNotifications.BroadcastMessages;
        player.neverAutoSwitchControlSchemes = true;
        Assert.That(player.user.valid, Is.True, "Runtime PlayerInput must create a real InputUser.");
        player.user.UnpairDevices();
        InputUser.PerformPairingWithDevice(keyboard, player.user);
        player.ActivateInput();
        return player;
    }

    private void AddJoystick()
    {
        // Optional twist/lever axes on a synthetic logical joystick; this does not
        // validate any physical USB descriptor, pedal calibration or HOTAS hardware.
        joystickLayout = "TelemetryJoystick" + Guid.NewGuid().ToString("N");
        InputSystem.RegisterLayout("{\"name\":\"" + joystickLayout + "\",\"extend\":\"Joystick\",\"controls\":[" +
            "{\"name\":\"twist\",\"layout\":\"Axis\",\"format\":\"FLT\",\"usages\":[\"Twist\"]}," +
            "{\"name\":\"throttle\",\"layout\":\"Axis\",\"format\":\"FLT\"}]}");
        joystick = (Joystick)InputSystem.AddDevice(joystickLayout);
    }

    private string[] Save(Action save = null)
    {
        Assert.That(outputDirectory, Is.Not.Null.And.Not.Empty);
        Assert.That(recorder.OutputDirectory, Is.EqualTo(outputDirectory), "Tests must never write into the user's recording directory.");
        string directory = outputDirectory;
        string[] before = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.csv") : Array.Empty<string>();
        string[] created;
        try { if (save == null) recorder.SaveData(); else save(); }
        finally
        {
            created = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.csv").Except(before).ToArray() : Array.Empty<string>();
            ownedCsv.AddRange(created);
        }
        Assert.That(created.Length, Is.EqualTo(1));
        return File.ReadAllLines(created[0]);
    }

    private static float[] Parse(string row)
    {
        string[] cells = row.Split(',');
        Assert.That(cells.Length, Is.EqualTo(18), "Recorded rows must match the shipped CSV schema.");
        return cells.Select(cell => float.Parse(cell, CultureInfo.InvariantCulture)).ToArray();
    }

    private static float Signed(float angle) => angle > 180f ? angle - 360f : angle;

    private static void PrepareIsolatedScene()
    {
        Assert.That(EditorApplication.isPlaying, Is.False, "Run this fixture from EditMode.");
        Assert.That(SessionState.GetString(SceneFolderKey, ""), Is.Empty, "Previous telemetry scene cleanup is incomplete.");
        string authoring = SessionState.GetString(MultiplayerVerificationRunner.AuthoringSetupKey, "");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            Assert.That(scene.isDirty, Is.False, "Save authoring scenes before verification.");
            if (string.IsNullOrEmpty(scene.path)) Assert.That(authoring, Is.Not.Empty,
                "Run through MultiplayerVerificationRunner to preserve authoring scenes.");
        }
        SessionState.SetString(SceneStateKey, !string.IsNullOrEmpty(authoring) ? authoring :
            JsonUtility.ToJson(new SavedSetup { scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SavedScene
                { path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive }).ToArray() }));
        string folder = "Assets/__TelemetryTests_" + Guid.NewGuid().ToString("N");
        SessionState.SetString(SceneFolderKey, folder);
        Assert.That(AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length)), Is.Not.Empty);
        var isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Assert.That(EditorSceneManager.SaveScene(isolated, folder + "/Isolated.unity"), Is.True);
        Assert.That(isolated.rootCount, Is.Zero, "Only the saved empty isolated scene enters Play.");
    }
}
