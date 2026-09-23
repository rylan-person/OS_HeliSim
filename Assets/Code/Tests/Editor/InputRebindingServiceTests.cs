using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputRebindingServiceTests
{
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

    [SetUp]
    public void SetUp()
    {
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
        Assert.That(service.GetJoystickBindingDisplay("Start Engine Global"), Is.EqualTo("Factory controls"));

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
}
