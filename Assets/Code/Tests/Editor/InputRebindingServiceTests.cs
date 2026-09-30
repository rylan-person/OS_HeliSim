using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Users;

public class InputRebindingServiceTests
{
    [TestCase("Assets/Level/Prefabs/Helicopters/R66_Default.prefab")]
    public void FlightPrefab_HasUsableFlightActions(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        PlayerInput player = prefab.GetComponent<PlayerInput>();
        InputActionAsset shipped = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/Code/InputActions/DefaultHeliAction.inputactions");

        Assert.That(player.actions, Is.SameAs(shipped), prefabPath);
        Assert.That(player.actions.FindActionMap(player.defaultActionMap, false), Is.Not.Null);
        Assert.That(player.notificationBehavior, Is.EqualTo(PlayerNotifications.BroadcastMessages));
        Assert.That(prefab.GetComponent<PlayerInputBindingOverrides>(), Is.Not.Null);
    }

    [Test]
    public void MouseButtonAssignment_PersistsAndDrivesCompositeDirection()
    {
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        try
        {
            actions.devices = new InputDevice[] { mouse };
            Assert.That(service.TryApplyBindingPath("Pitch Input", 3, "<Mouse>/leftButton", out string error), Is.True, error);
            service.Load();
            InputAction pitch = actions.FindAction("Pitch Input");
            Assert.That(pitch.bindings[3].effectivePath, Is.EqualTo("<Mouse>/leftButton"));
            Assert.That(service.GetBindingRows("Pitch Input", BindingDeviceTab.KeyboardMouse).Count, Is.EqualTo(2));
            actions.Enable();
            InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState().WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left));
            InputSystem.Update();
            Assert.That(pitch.ReadValue<float>(), Is.EqualTo(1f));
            InputSystem.QueueStateEvent(mouse, new UnityEngine.InputSystem.LowLevel.MouseState());
            InputSystem.Update();
            Assert.That(pitch.ReadValue<float>(), Is.Zero);
        }
        finally
        {
            actions.Disable();
            InputSystem.RemoveDevice(mouse);
        }
    }

    [TestCase("Pitch Input", Key.W, -1f)]
    [TestCase("Pitch Input", Key.S, 1f)]
    [TestCase("Roll Input", Key.A, -1f)]
    [TestCase("Roll Input", Key.D, 1f)]
    [TestCase("Yaw Input", Key.Q, -1f)]
    [TestCase("Yaw Input", Key.E, 1f)]
    [TestCase("Collective Lever", Key.PageUp, -1f)]
    [TestCase("Collective Lever", Key.PageDown, 1f)]
    [TestCase("Throttle Lever", Key.Digit1, -1f)]
    [TestCase("Throttle Lever", Key.Digit2, 1f)]
    public void ShippedFlightAxis_RespondsToBothKeyboardDirections(string actionName, Key key, float expected)
    {
        InputActionAsset shipped = Object.Instantiate(AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/Code/InputActions/DefaultHeliAction.inputactions"));
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            shipped.devices = new InputDevice[] { keyboard };
            shipped.Enable();
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(key));
            InputSystem.Update();
            Assert.That(shipped.FindAction(actionName).ReadValue<float>(), Is.EqualTo(expected));
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            InputSystem.Update();
            Assert.That(shipped.FindAction(actionName).ReadValue<float>(), Is.Zero);
        }
        finally
        {
            shipped.Disable();
            Object.DestroyImmediate(shipped);
            InputSystem.RemoveDevice(keyboard);
        }
    }

    [Test]
    public void MouseButtonAssignment_CanReplaceDesktopCommandAndSurviveLoad()
    {
        Assert.That(service.TryApplyBindingPath("Start Engine Global", 0, "<Mouse>/rightButton", out string error), Is.True, error);
        service.Load();
        Assert.That(actions.FindAction("Start Engine Global").bindings[0].effectivePath, Is.EqualTo("<Mouse>/rightButton"));
        Assert.That(service.TryClear("Start Engine Global", 0, out _), Is.False);
        Assert.That(service.TryApplyBindingPath("Pause Menu", 0, "<Mouse>/middleButton", out _), Is.False);
    }

    [Test]
    public void InteractiveKeyboardRebind_ChangesLiveAxisAndRestoresEnabledState()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            actions.devices = new InputDevice[] { keyboard };
            actions.Enable();
            bool changed = false;
            string error = null;
            Assert.That(service.TryStartRebind("Pitch Input", 3, message => error = message,
                () => changed = true, acceptControl: control => control.device == keyboard), Is.True);
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.UpArrow));
            InputSystem.Update();
            Assert.That(error, Is.Null);
            Assert.That(changed, Is.True);
            InputAction pitch = actions.FindAction("Pitch Input");
            Assert.That(pitch.enabled, Is.True);
            Assert.That(pitch.bindings[3].effectivePath, Is.EqualTo("<Keyboard>/upArrow"));
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState());
            InputSystem.Update();
            InputSystem.QueueStateEvent(keyboard, new UnityEngine.InputSystem.LowLevel.KeyboardState(Key.UpArrow));
            InputSystem.Update();
            Assert.That(pitch.ReadValue<float>(), Is.EqualTo(1f));
        }
        finally
        {
            actions.Disable();
            InputSystem.RemoveDevice(keyboard);
        }
    }

    [Test]
    public void ShippedActionAsset_ContainsEverySupportedMenuCommandWithKeyboardFallback()
    {
        InputActionAsset shipped = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/Code/InputActions/DefaultHeliAction.inputactions");
        Assert.That(shipped, Is.Not.Null);
        foreach (InputRebindingCommand command in InputRebindingCommands.All)
        {
            if (command.ActionName == "Camera Orbit") continue; // Legacy camera input has no action owner.
            InputAction action = shipped.FindAction(command.ActionName, false);
            Assert.That(action, Is.Not.Null, command.Label);
            bool hasKeyboard = false;
            foreach (InputBinding binding in action.bindings)
            {
                hasKeyboard |= binding.path.StartsWith("<Keyboard>/", System.StringComparison.OrdinalIgnoreCase);
            }
            Assert.That(hasKeyboard, Is.True, command.Label + " needs a keyboard fallback.");
        }
    }

    private InputActionAsset actions;
    private MemoryStore store;
    private InputRebindingService service;
    private InputSettings originalInputSettings;
    private InputSettings testInputSettings;

    [SetUp]
    public void SetUp()
    {
        // EditMode normally processes only editor input, which does not trigger gameplay actions.
        originalInputSettings = InputSystem.settings;
        testInputSettings = Object.Instantiate(originalInputSettings);
        testInputSettings.SetInternalFeatureFlag("RUN_PLAYER_UPDATES_IN_EDIT_MODE", true);
        InputSystem.settings = testInputSettings;
        actions = ScriptableObject.CreateInstance<InputActionAsset>();
        InputActionMap map = new InputActionMap("Default");

        InputAction pitch = map.AddAction("Pitch Input", InputActionType.Value);
        pitch.expectedControlType = "Axis";
        pitch.AddBinding("<Gamepad>/leftStick/y");
        pitch.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/s")
            .With("positive", "<Keyboard>/w");

        InputAction engine = map.AddAction("Start Engine Global", InputActionType.Button);
        engine.expectedControlType = "Button";
        engine.AddBinding("<Keyboard>/f1");
        engine.AddBinding("<Gamepad>/buttonSouth");

        InputAction menu = map.AddAction("Pause Menu", InputActionType.Button);
        menu.expectedControlType = "Button";
        menu.AddBinding("<Keyboard>/escape");
        menu.AddBinding("<Gamepad>/start");

        actions.AddActionMap(map);
        store = new MemoryStore();
        service = new InputRebindingService(actions, store);
    }

    [TearDown]
    public void TearDown()
    {
        service.Dispose();
        Object.DestroyImmediate(actions);
        InputSystem.settings = originalInputSettings;
        Object.DestroyImmediate(testInputSettings);
    }

    [Test]
    public void SaveAndLoad_UsesInputSystemOverrideJson()
    {
        Assert.That(service.TryApplyBindingPath("Start Engine Global", 1, "<Gamepad>/buttonNorth", out string error), Is.True, error);
        Assert.That(store.Json, Does.Contain("buttonNorth"));

        actions.RemoveAllBindingOverrides();
        service.Load();

        Assert.That(actions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonNorth"));
    }

    [Test]
    public void ClearAndReset_HandleDirectAndCompositeChildBindings()
    {
        Assert.That(service.TryClear("Start Engine Global", 1, out string clearError), Is.True, clearError);
        Assert.That(actions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.Empty);
        Assert.That(service.TryReset("Start Engine Global", 1, out string resetError), Is.True, resetError);
        Assert.That(actions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));

        Assert.That(service.TryApplyBindingPath("Pitch Input", 2, "<Keyboard>/downArrow", out string compositeError), Is.True, compositeError);
        Assert.That(service.GetBindingRows("Pitch Input")[1].BindingIndex, Is.EqualTo(2));
        Assert.That(service.TryReset("Pitch Input", 1, out string parentError), Is.True, parentError);
        Assert.That(actions.FindAction("Pitch Input").bindings[2].effectivePath, Is.EqualTo("<Keyboard>/s"));
    }

    [Test]
    public void IncompatibleButtonOrAxis_IsRejectedWithoutMutation()
    {
        Assert.That(service.TryApplyBindingPath("Start Engine Global", 1, "<Gamepad>/leftStick/x", out string buttonError), Is.False);
        Assert.That(buttonError, Does.Contain("button"));
        Assert.That(service.TryApplyBindingPath("Pitch Input", 0, "<Keyboard>/q", out string axisError), Is.False);
        Assert.That(axisError, Does.Contain("axis"));
        Assert.That(actions.FindAction("Pitch Input").bindings[0].effectivePath, Is.EqualTo("<Gamepad>/leftStick/y"));
        Assert.That(store.Json, Is.Null);
    }

    [Test]
    public void Rebinding_UsesLayoutsWithoutConnectedFlightHardware()
    {
        Assert.That(service.TryApplyBindingPath("Pitch Input", 0, "<Gamepad>/rightStick/x", out string error), Is.True, error);
        Assert.That(actions.FindAction("Pitch Input").bindings[0].effectivePath, Is.EqualTo("<Gamepad>/rightStick/x"));
        Assert.That(service.GetBindingRows("Missing Action")[0].Error, Does.Contain("no Input System action"));
    }

    [Test]
    public void ShippedActionAsset_HasAssignableYawAxisAndTwoButtonDirections()
    {
        InputActionAsset shipped = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/Code/InputActions/DefaultHeliAction.inputactions");
        InputAction yaw = shipped.FindAction("Yaw Input");
        int axisSlots = 0;
        int buttonDirections = 0;
        foreach (InputBinding binding in yaw.bindings)
        {
            if (binding.groups == "YawAxis" && !binding.isPartOfComposite && !binding.isComposite)
            {
                axisSlots++;
                Assert.That(binding.path, Is.Empty);
            }
            if (binding.groups == "YawButtons" && binding.isPartOfComposite) buttonDirections++;
        }
        Assert.That(axisSlots, Is.EqualTo(1));
        Assert.That(buttonDirections, Is.EqualTo(2));
    }

    [Test]
    public void DeviceTabs_UseOriginalPathsAfterOverridesAreCleared()
    {
        InputAction engine = actions.FindAction("Start Engine Global");
        engine.AddBinding("<Mouse>/leftButton");
        engine.AddBinding("<HID::Flight Stick>/button1");
        Assert.That(service.TryClear("Start Engine Global", 1, out string error), Is.True, error);

        var keyboardRows = service.GetBindingRows("Start Engine Global", BindingDeviceTab.KeyboardMouse);
        var joystickRows = service.GetBindingRows("Start Engine Global", BindingDeviceTab.Joystick);

        Assert.That(keyboardRows.Count, Is.EqualTo(2));
        Assert.That(keyboardRows[0].BindingIndex, Is.EqualTo(0));
        Assert.That(keyboardRows[1].BindingIndex, Is.EqualTo(2));
        Assert.That(joystickRows.Count, Is.EqualTo(2));
        Assert.That(joystickRows[0].BindingIndex, Is.EqualTo(1));
        Assert.That(joystickRows[0].Display, Is.EqualTo("Unbound"));
        Assert.That(joystickRows[1].BindingIndex, Is.EqualTo(3));
        Assert.That(service.GetBindingRows("Missing Action", BindingDeviceTab.KeyboardMouse).Count, Is.EqualTo(1));
        Assert.That(service.GetBindingRows("Missing Action", BindingDeviceTab.Joystick), Is.Empty);
    }

    [Test]
    public void JoystickSetting_ReplacesAllHardwarePathsAndRestoresFactoryBindings()
    {
        InputAction engine = actions.FindAction("Start Engine Global");
        engine.AddBinding("<Joystick>/button1");
        Assert.That(service.GetJoystickBindingDisplay("Start Engine Global"), Does.StartWith("Factory controls"));
        Assert.That(service.GetJoystickBindingDisplay("Start Engine Global"), Does.Contain("Gamepad"));

        Assert.That(service.TryApplyJoystickBindingPath("Start Engine Global", 1, "<Gamepad>/buttonNorth", out string applyError), Is.True, applyError);
        Assert.That(engine.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f1"));
        Assert.That(engine.bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonNorth"));
        Assert.That(engine.bindings[2].effectivePath, Is.Empty);
        Assert.That(service.GetJoystickBindingDisplay("Start Engine Global"), Is.Not.EqualTo("Factory controls"));

        actions.RemoveAllBindingOverrides();
        service.Load();
        Assert.That(engine.bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonNorth"));
        Assert.That(engine.bindings[2].effectivePath, Is.Empty);

        Assert.That(service.TryClearJoystickBindings("Start Engine Global", out string clearError), Is.True, clearError);
        Assert.That(engine.bindings[1].effectivePath, Is.Empty);
        Assert.That(engine.bindings[2].effectivePath, Is.Empty);
        Assert.That(service.GetJoystickBindingDisplay("Start Engine Global"), Is.EqualTo("Unbound"));

        Assert.That(service.TryResetJoystickBindings("Start Engine Global", out string resetError), Is.True, resetError);
        Assert.That(engine.bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
        Assert.That(engine.bindings[2].effectivePath, Is.EqualTo("<Joystick>/button1"));
        Assert.That(engine.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f1"));
    }

    [Test]
    public void JoystickAssignment_PairsNewDeviceWithTheActionsUser()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Joystick joystick = InputSystem.AddDevice<Joystick>();
        InputUser user = default;
        try
        {
            user = InputUser.PerformPairingWithDevice(keyboard);
            user.AssociateActionsWithUser(actions);
            actions.Enable();

            Assert.That(service.TryApplyJoystickBindingPath("Start Engine Global", 1,
                "<Joystick>/trigger", out string error), Is.True, error);

            InputAction engine = actions.FindAction("Start Engine Global");
            Assert.That(engine.controls, Does.Contain(joystick.trigger));
        }
        finally
        {
            actions.Disable();
            if (user.valid) user.UnpairDevicesAndRemoveUser();
            InputSystem.RemoveDevice(joystick);
            InputSystem.RemoveDevice(keyboard);
        }
    }

    [Test]
    public void SavedJoystickAssignment_ResolvesAfterLocalPlayerLoadsIt()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Joystick joystick = InputSystem.AddDevice<Joystick>();
        InputUser user = default;
        try
        {
            user = InputUser.PerformPairingWithDevice(keyboard);
            user.AssociateActionsWithUser(actions);
            actions.Enable();
            Assert.That(service.TryApplyJoystickBindingPath("Start Engine Global", 1,
                "<Joystick>/trigger", out string error), Is.True, error);

            user.UnpairDevice(joystick);
            actions.RemoveAllBindingOverrides();
            service.Load();
            service.PairConnectedDevices();

            InputAction engine = actions.FindAction("Start Engine Global");
            Assert.That(engine.bindings[1].effectivePath, Is.EqualTo("<Joystick>/trigger"));
            Assert.That(engine.controls, Does.Contain(joystick.trigger));
        }
        finally
        {
            actions.Disable();
            if (user.valid) user.UnpairDevicesAndRemoveUser();
            InputSystem.RemoveDevice(joystick);
            InputSystem.RemoveDevice(keyboard);
        }
    }

    [Test]
    public void YawMode_SwitchesLiveControlsAndKeepsKeyboardYaw()
    {
        InputAction yaw = actions.FindAction("Yaw Input", false) ?? actions.FindActionMap("Default").AddAction("Yaw Input", InputActionType.PassThrough);
        yaw.expectedControlType = "Axis";
        yaw.AddCompositeBinding("1DAxis")
            .With("negative", "<Joystick>/trigger", groups: "YawButtons")
            .With("positive", "<Joystick>/trigger", groups: "YawButtons");
        yaw.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/q", groups: "YawButtons;YawAxis")
            .With("positive", "<Keyboard>/e", groups: "YawButtons;YawAxis");
        yaw.AddBinding(string.Empty, groups: "YawAxis");
        var modeStore = new MemoryYawStore();
        using (var yawService = new InputRebindingService(actions, store, modeStore))
        {
            Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
            Joystick joystick = InputSystem.AddDevice<Joystick>();
            InputUser user = default;
            try
            {
                user = InputUser.PerformPairingWithDevice(keyboard);
                user.AssociateActionsWithUser(actions);
                actions.Enable();
                yawService.SetYawBindingMode(YawBindingMode.Axis);
                Assert.That(yawService.GetBindingRows("Yaw Input", BindingDeviceTab.Joystick).Count, Is.EqualTo(1));
                Assert.That(yawService.TryApplyJoystickBindingPath("Yaw Input", 6, "<Joystick>/stick/x", out string error), Is.True, error);
                Assert.That(IsPaired(user, joystick), Is.True);
                Assert.That(ContainsControl(yaw, joystick.stick.x), Is.True);
                Assert.That(ContainsControl(yaw, keyboard.qKey), Is.True);
                Assert.That(ContainsControl(yaw, joystick.trigger), Is.False);

                yawService.SetYawBindingMode(YawBindingMode.Buttons);
                Assert.That(yawService.GetBindingRows("Yaw Input", BindingDeviceTab.Joystick).Count, Is.EqualTo(2));
                Assert.That(ContainsControl(yaw, joystick.trigger), Is.True);
                Assert.That(ContainsControl(yaw, keyboard.qKey), Is.True);
                Assert.That(ContainsControl(yaw, joystick.stick.x), Is.False);
            }
            finally
            {
                actions.Disable();
                if (user.valid) user.UnpairDevicesAndRemoveUser();
                InputSystem.RemoveDevice(joystick);
                InputSystem.RemoveDevice(keyboard);
            }
        }
    }

    [Test]
    public void ResetProfile_ClearsPedalAxisAndRestoresButtonMode()
    {
        InputAction yaw = actions.FindActionMap("Default").AddAction("Yaw Input", InputActionType.PassThrough);
        yaw.expectedControlType = "Axis";
        yaw.AddCompositeBinding("1DAxis")
            .With("negative", "<Joystick>/trigger", groups: "YawButtons")
            .With("positive", "<Joystick>/trigger", groups: "YawButtons");
        yaw.AddBinding(string.Empty, groups: "YawAxis");
        yaw.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/q", groups: "YawButtons;YawAxis")
            .With("positive", "<Keyboard>/e", groups: "YawButtons;YawAxis");
        var modeStore = new MemoryYawStore();
        using (var yawService = new InputRebindingService(actions, store, modeStore))
        {
            yawService.SetYawBindingMode(YawBindingMode.Axis);
            Assert.That(yawService.TryApplyJoystickBindingPath("Yaw Input", 3, "<Joystick>/stick/x", out string error), Is.True, error);
            Assert.That(yaw.bindings[3].effectivePath, Is.EqualTo("<Joystick>/stick/x"));
            yawService.Load();
            Assert.That(yawService.YawMode, Is.EqualTo(YawBindingMode.Axis));
            Assert.That(yaw.bindings[3].effectivePath, Is.EqualTo("<Joystick>/stick/x"));
            yawService.ResetProfile();
            Assert.That(yawService.YawMode, Is.EqualTo(YawBindingMode.Buttons));
            Assert.That(modeStore.Load(), Is.EqualTo(YawBindingMode.Buttons));
            Assert.That(yaw.bindings[3].effectivePath, Is.Empty);
            Assert.That(yaw.bindings[1].effectivePath, Is.EqualTo("<Joystick>/trigger"));
            Assert.That(yaw.bindings[2].effectivePath, Is.EqualTo("<Joystick>/trigger"));
            Assert.That(store.Json, Is.Null);
        }
    }

    [Test]
    public void DeviceTabs_RejectControlsFromTheOtherTab()
    {
        Keyboard keyboard = InputSystem.AddDevice<Keyboard>();
        Mouse mouse = InputSystem.AddDevice<Mouse>();
        Gamepad gamepad = InputSystem.AddDevice<Gamepad>();
        try
        {
            Assert.That(InputRebindingService.IsDeviceAllowedForTab(keyboard, BindingDeviceTab.KeyboardMouse), Is.True);
            Assert.That(InputRebindingService.IsDeviceAllowedForTab(mouse, BindingDeviceTab.KeyboardMouse), Is.True);
            Assert.That(InputRebindingService.IsDeviceAllowedForTab(gamepad, BindingDeviceTab.KeyboardMouse), Is.False);
            Assert.That(InputRebindingService.IsDeviceAllowedForTab(gamepad, BindingDeviceTab.Joystick), Is.True);
            Assert.That(InputRebindingService.IsDeviceAllowedForTab(keyboard, BindingDeviceTab.Joystick), Is.False);
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard);
            InputSystem.RemoveDevice(mouse);
            InputSystem.RemoveDevice(gamepad);
        }
    }

    [Test]
    public void KeyboardFallback_AndEscapeRecoverySurviveClearAndLoadedOverrides()
    {
        Assert.That(service.TryClear("Start Engine Global", 0, out string fallbackError), Is.False);
        Assert.That(fallbackError, Does.Contain("keyboard"));
        Assert.That(service.TryClear("Pause Menu", 0, out string escapeError), Is.False);
        Assert.That(escapeError, Does.Contain("keyboard"));
        Assert.That(service.TryApplyBindingPath("Pause Menu", 0, "<Keyboard>/p", out _), Is.False);
        Assert.That(service.TryClear("Pitch Input", 2, out string compositeError), Is.False);
        Assert.That(compositeError, Does.Contain("keyboard"));
        Assert.That(service.TryApplyBindingPath("Pitch Input", 2, "<Gamepad>/buttonSouth", out _), Is.False);
        Assert.That(service.GetPrimaryBindingDisplay("Pitch Input"), Does.Contain("S").IgnoreCase);
        Assert.That(service.GetPrimaryBindingDisplay("Pitch Input"), Does.Contain("W").IgnoreCase);

        actions.FindAction("Pause Menu").ApplyBindingOverride(0, "<Gamepad>/buttonNorth");
        actions.FindAction("Pitch Input").ApplyBindingOverride(2, string.Empty);
        store.Save(actions.SaveBindingOverridesAsJson());
        service.Load();

        Assert.That(actions.FindAction("Pause Menu").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/escape"));
        Assert.That(actions.FindAction("Pitch Input").bindings[2].effectivePath, Is.EqualTo("<Keyboard>/s"));
        Assert.That(service.GetPrimaryBindingDisplay("Pause Menu"), Does.Contain("Esc").IgnoreCase);
        service.ResetProfile();
        Assert.That(store.Json, Is.Null);
    }

    [Test]
    public void Escape_IsReservedForMenuRecovery()
    {
        Assert.That(service.TryApplyBindingPath("Start Engine Global", 0, "<Keyboard>/escape", out string error), Is.False);
        Assert.That(error, Does.Contain("reserved"));
        Assert.That(actions.FindAction("Start Engine Global").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f1"));
        Assert.That(store.Json, Is.Null);
    }

    private sealed class MemoryStore : IBindingOverrideStore
    {
        public string Json { get; private set; }
        public string Load() => Json;
        public void Save(string json) => Json = json;
        public void Clear() => Json = null;
    }

    private static bool ContainsControl(InputAction action, InputControl control)
    {
        foreach (InputControl candidate in action.controls)
            if (candidate == control) return true;
        return false;
    }

    private static bool IsPaired(InputUser user, InputDevice device)
    {
        foreach (InputDevice paired in user.pairedDevices)
            if (paired == device) return true;
        return false;
    }

    private sealed class MemoryYawStore : IYawBindingModeStore
    {
        private YawBindingMode mode;
        public YawBindingMode Load() => mode;
        public void Save(YawBindingMode value) => mode = value;
        public void Clear() => mode = YawBindingMode.Buttons;
    }
}
