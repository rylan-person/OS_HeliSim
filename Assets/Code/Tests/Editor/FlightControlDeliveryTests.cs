using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Users;

public class FlightControlDeliveryTests
{
    private const string StickLayout = "HID::Thustmaster Joystick - HOTAS Warthog";
    private const string ThrottleLayout = "HID::Thrustmaster Throttle - HOTAS Warthog";
    private const string X52Layout = "HID::Saitek Saitek X52 Flight Control System";
    private InputSettings originalSettings;
    private InputSettings testSettings;
    private InputActionAsset actions;
    private InputUser user;
    private Keyboard keyboard;
    private InputDevice stick;
    private InputDevice throttle;
    private InputRebindingService service;
    private bool registeredStick;
    private bool registeredThrottle;
    private bool registeredX52;

    [SetUp]
    public void SetUp()
    {
        stick = null;
        throttle = null;
        registeredStick = false;
        registeredThrottle = false;
        registeredX52 = false;
        originalSettings = InputSystem.settings;
        testSettings = Object.Instantiate(originalSettings);
        testSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
        InputSystem.settings = testSettings;
        keyboard = InputSystem.AddDevice<Keyboard>();
        actions = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/Code/InputActions/DefaultHeliAction.inputactions"));
        user = InputUser.PerformPairingWithDevice(keyboard);
        user.AssociateActionsWithUser(actions);
        service = new InputRebindingService(actions, new MemoryStore(), new MemoryYawStore());
        actions.Enable();
        RestrictToOwnedDevices();
    }

    [TearDown]
    public void TearDown()
    {
        service?.Dispose();
        if (user.valid) user.UnpairDevicesAndRemoveUser();
        if (actions != null) Object.DestroyImmediate(actions);
        if (stick != null) InputSystem.RemoveDevice(stick);
        if (throttle != null) InputSystem.RemoveDevice(throttle);
        if (keyboard != null) InputSystem.RemoveDevice(keyboard);
        InputSystem.settings = originalSettings;
        if (testSettings != null) Object.DestroyImmediate(testSettings);
        if (registeredStick) InputSystem.RemoveLayout(StickLayout);
        if (registeredThrottle) InputSystem.RemoveLayout(ThrottleLayout);
        if (registeredX52) InputSystem.RemoveLayout(X52Layout);
    }

    [TestCase("Pitch Input", Key.W, -1f, 0f)]
    [TestCase("Pitch Input", Key.S, 1f, 0f)]
    [TestCase("Roll Input", Key.A, -1f, 0f)]
    [TestCase("Roll Input", Key.D, 1f, 0f)]
    [TestCase("Yaw Input", Key.Q, -1f, 0f)]
    [TestCase("Yaw Input", Key.E, 1f, 0f)]
    [TestCase("Collective Lever", Key.PageUp, -1f, 0f)]
    [TestCase("Collective Lever", Key.PageDown, 1f, 0f)]
    [TestCase("Throttle Lever", Key.Digit1, -1f, 0f)]
    [TestCase("Throttle Lever", Key.Digit2, 1f, 0f)]
    public void KeyboardAxes_ReadShippedValuesOnPressAndRelease(
        string actionName, Key key, float pressed, float released)
    {
        Press(key);
        Assert.That(Delivered(actionName), Is.EqualTo(pressed).Within(0.0001f));
        Press();
        Assert.That(Delivered(actionName), Is.EqualTo(released).Within(0.0001f));
    }

    [TestCase("Pitch Input", "stick/y", -1f, 1f)]
    [TestCase("Roll Input", "stick/x", -1f, -1f)]
    [TestCase("Collective Lever", "z", -1f, 1f)]
    [TestCase("Throttle Lever", "slider", -1f, 1f)]
    [TestCase("Pitch Input", "stick/y", 1f, -1f)]
    [TestCase("Roll Input", "stick/x", 1f, 1f)]
    [TestCase("Collective Lever", "z", 1f, -1f)]
    [TestCase("Throttle Lever", "slider", 1f, -1f)]
    public void FactoryHotasAxes_DeliverThroughShippedBindings(
        string actionName, string controlPath, float raw, float expected)
    {
        AddHotas();
        InputDevice device = actionName == "Pitch Input" || actionName == "Roll Input" ? stick : throttle;
        AxisControl axis = device[controlPath] as AxisControl;
        Assert.That(actions.FindAction(actionName).controls, Does.Contain(axis),
            "Factory path must resolve without a binding override.");
        InputSystem.QueueDeltaStateEvent(axis, raw);
        InputSystem.Update();
        Assert.That(Delivered(actionName), Is.EqualTo(expected).Within(0.0001f));
        InputSystem.QueueDeltaStateEvent(axis, 0f);
        InputSystem.Update();
        float neutral = 0f;
        Assert.That(Delivered(actionName), Is.EqualTo(neutral).Within(0.0001f));
    }

    [TestCase("Pitch Input", "stick/y", -1f, 1f)]
    [TestCase("Pitch Input", "stick/y", 1f, -1f)]
    [TestCase("Roll Input", "stick/x", -1f, -1f)]
    [TestCase("Roll Input", "stick/x", 1f, 1f)]
    public void FactoryX52Cyclic_ResolvesAndReadsBothDirections(
        string actionName, string controlPath, float raw, float expected)
    {
        bool exists = false;
        foreach (string layout in InputSystem.ListLayouts()) exists |= layout == X52Layout;
        if (!exists)
        {
            InputSystem.RegisterLayout("{\"name\":\"" + X52Layout + "\",\"extend\":\"Joystick\"}");
            registeredX52 = true;
        }
        stick = InputSystem.AddDevice(X52Layout);
        service.PairConnectedDevices(stick);
        RestrictToOwnedDevices();
        AxisControl axis = (AxisControl)stick[controlPath];
        Assert.That(actions.FindAction(actionName).controls, Does.Contain(axis), "Factory X52 controls must resolve without overrides.");
        InputSystem.QueueDeltaStateEvent(axis, raw);
        InputSystem.Update();
        Assert.That(Delivered(actionName), Is.EqualTo(expected).Within(0.0001f));
        InputSystem.QueueDeltaStateEvent(axis, 0f);
        InputSystem.Update();
        Assert.That(Delivered(actionName), Is.Zero);
    }

    [Test]
    public void SavedJoystickOverride_PairsHardwareAndPreservesKeyboardDelivery()
    {
        AddHotas();
        Assert.That(service.TryApplyJoystickBindingPath("Pitch Input", 0,
            "<" + StickLayout + ">/stick/x", out string error), Is.True, error);
        actions.RemoveAllBindingOverrides();
        service.Load();
        service.PairConnectedDevices(stick);
        service.PairConnectedDevices(throttle);
        RestrictToOwnedDevices();
        InputSystem.QueueDeltaStateEvent((AxisControl)stick["stick/x"], 1f);
        InputSystem.Update();
        Assert.That(actions.FindAction("Pitch Input").ReadValue<float>(), Is.EqualTo(-1f));
        InputSystem.QueueDeltaStateEvent((AxisControl)stick["stick/x"], 0f);
        InputSystem.Update();
        InputSystem.RemoveDevice(stick);
        stick = null;
        Assert.That(user.pairedDevices, Does.Contain(keyboard), "The original paired keyboard must survive hardware removal.");
        // PairConnectedDevices intentionally picks up usable unpaired devices; keep
        // this fixture's action selection off the operator's physical keyboard.
        actions.devices = new InputDevice[] { keyboard, throttle };
        InputAction pitch = actions.FindAction("Pitch Input");
        Assert.That(pitch.enabled, Is.True, "Device removal must preserve the enabled action.");
        Assert.That(pitch.controls, Does.Contain(keyboard.sKey), "Device removal must preserve the paired keyboard control.");
        Press(Key.S);
        Assert.That(keyboard.sKey.isPressed, Is.True, "Synthetic keyboard event must reach the device before checking routing.");
        Assert.That(pitch.ReadValue<float>(), Is.EqualTo(1f), "devices=" + actions.devices + "; controls=" + string.Join(",", pitch.controls));
        Press();
        Assert.That(actions.FindAction("Pitch Input").ReadValue<float>(), Is.Zero);
    }

    [TestCase("Pitch Input", Key.S, Key.W, 1f, -1f)]
    [TestCase("Roll Input", Key.A, Key.D, -1f, 1f)]
    [TestCase("Yaw Input", Key.Q, Key.E, -1f, 1f)]
    public void KeyboardDirectionSwitch_UpdatesWithoutAnInterveningNeutralEvent(
        string actionName, Key first, Key second, float initial, float switched)
    {
        float delivered = 0;
        actions.FindAction(actionName).performed += context => delivered = context.ReadValue<float>();
        Press(first);
        Assert.That(Delivered(actionName), Is.EqualTo(initial));
        Assert.That(delivered, Is.EqualTo(initial));
        Press(second);
        Assert.That(Delivered(actionName), Is.EqualTo(switched));
        Assert.That(delivered, Is.EqualTo(switched), "The action callback must deliver direction changes, not just ReadValue.");
    }

    [Test]
    public void ReboundPedalAxis_AndKeyboardYawDeliverInAxisMode()
    {
        AddHotas();
        service.SetYawBindingMode(YawBindingMode.Axis);
        int slot = -1;
        InputAction yaw = actions.FindAction("Yaw Input");
        for (int i = 0; i < yaw.bindings.Count; i++)
            if (yaw.bindings[i].groups == "YawAxis") slot = i;
        Assert.That(slot, Is.GreaterThanOrEqualTo(0));
        Assert.That(service.TryApplyJoystickBindingPath("Yaw Input", slot,
            "<" + StickLayout + ">/stick/x", out string error), Is.True, error);
        InputSystem.QueueDeltaStateEvent((AxisControl)stick["stick/x"], -1f);
        InputSystem.Update();
        Assert.That(actions.FindAction("Yaw Input").ReadValue<float>(), Is.EqualTo(-1f));
        InputSystem.QueueDeltaStateEvent((AxisControl)stick["stick/x"], 0f);
        InputSystem.Update();
        Press(Key.E);
        Assert.That(actions.FindAction("Yaw Input").ReadValue<float>(), Is.EqualTo(1f));
        Press();
        Assert.That(actions.FindAction("Yaw Input").ReadValue<float>(), Is.Zero);
    }

    [Test]
    public void FactoryEngineStart_KeyboardAndHotasPerformOncePerPress()
    {
        int starts = 0;
        actions.FindAction("Start Engine Global").performed += _ => starts++;
        Press(Key.F1);
        Assert.That(starts, Is.EqualTo(1));
        Press();
        Assert.That(starts, Is.EqualTo(1), "Release must not restart an engine.");
        AddHotas();
        InputSystem.QueueDeltaStateEvent((ButtonControl)throttle["button16"], 1f);
        InputSystem.Update();
        Assert.That(starts, Is.EqualTo(2));
        InputSystem.QueueDeltaStateEvent((ButtonControl)throttle["button16"], 0f);
        InputSystem.Update();
        Assert.That(starts, Is.EqualTo(2));
    }

    private void AddHotas()
    {
        // Exact layout names exercise the shipped path selectors. Synthetic logical
        // controls do not claim to validate USB HID descriptors or physical calibration.
        bool hasStick = false;
        bool hasThrottle = false;
        foreach (string layout in InputSystem.ListLayouts())
        {
            hasStick |= layout == StickLayout;
            hasThrottle |= layout == ThrottleLayout;
        }
        if (!hasStick)
        {
            InputSystem.RegisterLayout("{\"name\":\"" + StickLayout + "\",\"extend\":\"Joystick\"}");
            registeredStick = true;
        }
        if (!hasThrottle)
        {
            InputSystem.RegisterLayout("{\"name\":\"" + ThrottleLayout + "\",\"extend\":\"Joystick\",\"controls\":[" +
            "{\"name\":\"z\",\"layout\":\"Axis\",\"format\":\"FLT\"}," +
            "{\"name\":\"slider\",\"layout\":\"Axis\",\"format\":\"FLT\"}," +
            "{\"name\":\"button16\",\"layout\":\"Button\",\"format\":\"FLT\"}]}");
            registeredThrottle = true;
        }
        stick = InputSystem.AddDevice(StickLayout);
        throttle = InputSystem.AddDevice(ThrottleLayout);
        service.PairConnectedDevices(stick);
        service.PairConnectedDevices(throttle);
        RestrictToOwnedDevices();
    }

    private void Press(params Key[] keys)
    {
        RestrictToOwnedDevices();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        InputSystem.Update();
    }

    private void RestrictToOwnedDevices()
    {
        var devices = new System.Collections.Generic.List<InputDevice> { keyboard };
        if (stick != null) devices.Add(stick);
        if (throttle != null) devices.Add(throttle);
        actions.devices = devices.ToArray();
    }

    private float Delivered(string actionName) => actions.FindAction(actionName).ReadValue<float>();

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
