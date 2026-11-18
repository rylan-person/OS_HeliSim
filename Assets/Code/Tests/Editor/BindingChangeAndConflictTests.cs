using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BindingChangeAndConflictTests
{
    private readonly List<Object> created = new List<Object>();
    private InputActionAsset actions;
    private MemoryStore store;
    private InputRebindingService service;

    [SetUp]
    public void SetUp()
    {
        actions = ScriptableObject.CreateInstance<InputActionAsset>();
        created.Add(actions);
        InputActionMap map = new InputActionMap("Default");
        InputAction pitch = map.AddAction("Pitch Input", InputActionType.Value);
        pitch.expectedControlType = "Axis";
        pitch.AddBinding("<Gamepad>/leftStick/y");
        pitch.AddCompositeBinding("1DAxis")
            .With("negative", "<Keyboard>/s")
            .With("positive", "<Keyboard>/w");
        InputAction engine = map.AddAction("Start Engine Global", InputActionType.Button);
        engine.AddBinding("<Keyboard>/f1");
        engine.AddBinding("<Gamepad>/buttonSouth");
        InputAction reset = map.AddAction("Reset Helicopter", InputActionType.Button);
        reset.AddBinding("<Keyboard>/r");
        reset.AddBinding("<Gamepad>/buttonEast");
        actions.AddActionMap(map);

        store = new MemoryStore();
        service = new InputRebindingService(actions, store, new MemoryYawStore());
    }

    [TearDown]
    public void TearDown()
    {
        service?.Dispose();
        service = null;
        foreach (Object item in created)
        {
            if (item != null) Object.DestroyImmediate(item);
        }

        created.Clear();
    }

    [Test]
    public void IsBindingChanged_TracksApplyAndReset()
    {
        Assert.That(service.IsBindingChanged("Start Engine Global", 0), Is.False);
        Assert.That(service.IsBindingChanged("Missing Action", 0), Is.False);
        Assert.That(service.IsBindingChanged("Start Engine Global", 7), Is.False);

        Assert.That(service.TryApplyBindingPath("Start Engine Global", 0, "<Keyboard>/f2", out string error), Is.True, error);
        Assert.That(service.IsBindingChanged("Start Engine Global", 0), Is.True);
        Assert.That(service.IsBindingChanged("Start Engine Global", 1), Is.False);

        Assert.That(service.TryReset("Start Engine Global", 0, out string resetError), Is.True, resetError);
        Assert.That(service.IsBindingChanged("Start Engine Global", 0), Is.False);
    }

    [Test]
    public void AreJoystickBindingsChanged_DetectsClearedJoystickSetting()
    {
        Assert.That(service.AreJoystickBindingsChanged("Start Engine Global"), Is.False);
        Assert.That(service.TryClearJoystickBindings("Start Engine Global", out string error), Is.True, error);
        Assert.That(service.AreJoystickBindingsChanged("Start Engine Global"), Is.True);
        Assert.That(service.IsBindingChanged("Start Engine Global", 0), Is.False);
    }

    [Test]
    public void FindConflict_ReturnsOtherCommandLabelForSharedKey()
    {
        Assert.That(service.FindConflict("Start Engine Global", 0, BindingDeviceTab.KeyboardMouse), Is.Null);
        Assert.That(service.FindConflict("Reset Helicopter", 0, BindingDeviceTab.KeyboardMouse), Is.Null);

        Assert.That(service.TryApplyBindingPath("Reset Helicopter", 0, "<Keyboard>/f1", out string error), Is.True, error);

        Assert.That(service.FindConflict("Start Engine Global", 0, BindingDeviceTab.KeyboardMouse), Is.EqualTo("Reset aircraft"));
        Assert.That(service.FindConflict("Reset Helicopter", 0, BindingDeviceTab.KeyboardMouse), Is.EqualTo("Engine start"));
    }

    [Test]
    public void FindConflict_NamesPartWhenConflictIsWithinSameCommand()
    {
        Assert.That(service.TryApplyBindingPath("Pitch Input", 2, "<Keyboard>/w", out string error), Is.True, error);

        Assert.That(service.FindConflict("Pitch Input", 2, BindingDeviceTab.KeyboardMouse), Is.EqualTo("Cyclic pitch — positive"));
        Assert.That(service.FindConflict("Pitch Input", 3, BindingDeviceTab.KeyboardMouse), Is.EqualTo("Cyclic pitch — negative"));
    }

    [Test]
    public void FindConflict_IgnoresBindingsOnOtherDeviceTab()
    {
        // The reset joystick binding stays on the joystick tab even when pointed at a keyboard key.
        Assert.That(service.TryApplyBindingPath("Reset Helicopter", 1, "<Keyboard>/f1", out string error), Is.True, error);

        Assert.That(service.FindConflict("Start Engine Global", 0, BindingDeviceTab.KeyboardMouse), Is.Null);
        Assert.That(service.FindConflict("Reset Helicopter", 1, BindingDeviceTab.Joystick), Is.Null);
        Assert.That(service.FindJoystickConflict("Reset Helicopter"), Is.Null);
    }

    [Test]
    public void FindJoystickConflict_ReturnsOtherCommandSharingJoystickControl()
    {
        Assert.That(service.FindJoystickConflict("Start Engine Global"), Is.Null);
        Assert.That(service.TryApplyBindingPath("Reset Helicopter", 1, "<Gamepad>/buttonSouth", out string error), Is.True, error);

        Assert.That(service.FindJoystickConflict("Start Engine Global"), Is.EqualTo("Reset aircraft"));
        Assert.That(service.FindJoystickConflict("Reset Helicopter"), Is.EqualTo("Engine start"));
    }

    [Test]
    public void Row_ShowsChangedBadgeAndConflictText()
    {
        Assert.That(service.TryApplyBindingPath("Start Engine Global", 0, "<Keyboard>/s", out string error), Is.True, error);
        TestRow engineRow = CreateRow();
        TestRow pitchRow = CreateRow();

        engineRow.Row.Configure(service, Command("Start Engine Global"),
            FirstRow("Start Engine Global", BindingDeviceTab.KeyboardMouse), false, null, null, false,
            BindingDeviceTab.KeyboardMouse);
        pitchRow.Row.Configure(service, Command("Pitch Input"),
            FirstRow("Pitch Input", BindingDeviceTab.KeyboardMouse), false, null, null, false,
            BindingDeviceTab.KeyboardMouse);

        Assert.That(engineRow.Row.ShowsChanged, Is.True);
        Assert.That(engineRow.Badge.activeSelf, Is.True);
        Assert.That(engineRow.Error.text, Is.EqualTo("Also bound to Cyclic pitch."));
        Assert.That(pitchRow.Row.ShowsChanged, Is.False);
        Assert.That(pitchRow.Error.text, Is.EqualTo("Also bound to Engine start."));

        Assert.That(service.TryReset("Start Engine Global", 0, out string resetError), Is.True, resetError);
        engineRow.Row.Refresh();
        pitchRow.Row.Refresh();
        Assert.That(engineRow.Row.ShowsChanged, Is.False);
        Assert.That(engineRow.Error.text, Is.Empty);
        Assert.That(pitchRow.Error.text, Is.Empty);
    }

    [Test]
    public void Row_TransientErrorTakesPrecedenceOverConflict()
    {
        Assert.That(service.TryApplyBindingPath("Start Engine Global", 0, "<Keyboard>/s", out string error), Is.True, error);
        TestRow pitchRow = CreateRow();
        pitchRow.Row.Configure(service, Command("Pitch Input"),
            FirstRow("Pitch Input", BindingDeviceTab.KeyboardMouse), false, null, null, false,
            BindingDeviceTab.KeyboardMouse);
        Assert.That(pitchRow.Error.text, Is.EqualTo("Also bound to Engine start."));

        // Clearing the only keyboard direction of a composite is refused with a transient error.
        pitchRow.Clear.onClick.Invoke();
        Assert.That(pitchRow.Error.text, Does.Contain("Keep a keyboard"));

        pitchRow.Row.Refresh();
        Assert.That(pitchRow.Error.text, Does.Contain("Keep a keyboard"));
    }

    [Test]
    public void GroupedJoystickRow_UsesJoystickChangeAndConflict()
    {
        Assert.That(service.TryApplyBindingPath("Reset Helicopter", 1, "<Gamepad>/buttonSouth", out string error), Is.True, error);
        TestRow resetRow = CreateRow();
        resetRow.Row.Configure(service, Command("Reset Helicopter"),
            FirstRow("Reset Helicopter", BindingDeviceTab.Joystick), false, null, null, true, BindingDeviceTab.Joystick);

        Assert.That(resetRow.Row.ShowsChanged, Is.True);
        Assert.That(resetRow.Error.text, Is.EqualTo("Also bound to Engine start."));
    }

    [TestCase(0, "All commands use their default bindings. Changes save immediately.")]
    [TestCase(1, "1 command differs from the defaults on this device. Changes save immediately.")]
    [TestCase(2, "2 commands differ from the defaults on this device. Changes save immediately.")]
    public void Page_ChangeSummaryCountsChangedCommands(int changes, string expected)
    {
        if (changes >= 1)
            Assert.That(service.TryApplyBindingPath("Start Engine Global", 0, "<Keyboard>/f2", out string first), Is.True, first);
        if (changes >= 2)
        {
            Assert.That(service.TryApplyBindingPath("Pitch Input", 2, "<Keyboard>/downArrow", out string second), Is.True, second);
            Assert.That(service.TryApplyBindingPath("Pitch Input", 3, "<Keyboard>/upArrow", out string third), Is.True, third);
        }

        // A joystick-only change is not counted on the keyboard tab.
        Assert.That(service.TryApplyBindingPath("Reset Helicopter", 1, "<Gamepad>/buttonNorth", out string joystick), Is.True, joystick);

        TMP_Text summary = NewText("Summary");
        ControlsRebindingPage page = NewObject("Controls Page").AddComponent<ControlsRebindingPage>();
        page.SetControlsForTest(actions, store, CreateRow().Row, NewObject("Rows").transform, null, null, null);
        page.SetPresentationForTest(null, null, null, null, null, null, null, null, null);
        page.SetChangeSummaryForTest(summary);
        page.SetServiceForTest(service);

        page.OnPageSelected();

        Assert.That(summary.text, Is.EqualTo(expected));
    }

    [Test]
    public void Page_ChangeSummaryIsNeutralWithoutService()
    {
        TMP_Text summary = NewText("Summary");
        ControlsRebindingPage page = NewObject("Controls Page").AddComponent<ControlsRebindingPage>();
        page.SetControlsForTest(null, store, CreateRow().Row, NewObject("Rows").transform, null, null, null);
        page.SetChangeSummaryForTest(summary);

        page.OnPageSelected();

        Assert.That(summary.text, Is.EqualTo("Changes save immediately."));
    }

    private static InputRebindingCommand Command(string actionName)
    {
        foreach (InputRebindingCommand command in InputRebindingCommands.All)
        {
            if (command.ActionName == actionName) return command;
        }

        throw new System.ArgumentException(actionName);
    }

    private InputBindingRow FirstRow(string actionName, BindingDeviceTab tab)
    {
        IReadOnlyList<InputBindingRow> rows = service.GetBindingRows(actionName, tab);
        Assert.That(rows.Count, Is.GreaterThan(0));
        return rows[0];
    }

    private TestRow CreateRow()
    {
        GameObject root = NewObject("Binding Row");
        ControlsBindingRow row = root.AddComponent<ControlsBindingRow>();
        TMP_Text command = NewText("Command", root.transform);
        TMP_Text binding = NewText("Binding", root.transform);
        TMP_Text primary = NewText("Primary", root.transform);
        TMP_Text errorText = NewText("Error", root.transform);
        Button rebind = NewButton("Rebind", root.transform);
        Button clear = NewButton("Clear", root.transform);
        Button reset = NewButton("Reset", root.transform);
        GameObject badge = NewObject("Changed");
        badge.transform.SetParent(root.transform);
        badge.SetActive(false);
        row.SetControlsForTest(command, binding, primary, errorText, rebind, clear, reset);
        row.SetChangedBadgeForTest(badge);
        return new TestRow(row, errorText, badge, clear);
    }

    private GameObject NewObject(string name)
    {
        GameObject result = new GameObject(name);
        created.Add(result);
        return result;
    }

    private TMP_Text NewText(string name, Transform parent = null)
    {
        GameObject result = NewObject(name);
        if (parent != null) result.transform.SetParent(parent);
        return result.AddComponent<TextMeshProUGUI>();
    }

    private Button NewButton(string name, Transform parent)
    {
        GameObject result = NewObject(name);
        if (parent != null) result.transform.SetParent(parent);
        return result.AddComponent<Button>();
    }

    private readonly struct TestRow
    {
        public readonly ControlsBindingRow Row;
        public readonly TMP_Text Error;
        public readonly GameObject Badge;
        public readonly Button Clear;

        public TestRow(ControlsBindingRow row, TMP_Text error, GameObject badge, Button clear)
        {
            Row = row;
            Error = error;
            Badge = badge;
            Clear = clear;
        }
    }

    private sealed class MemoryYawStore : IYawBindingModeStore
    {
        private YawBindingMode mode;
        public YawBindingMode Load() => mode;
        public void Save(YawBindingMode value) => mode = value;
        public void Clear() => mode = YawBindingMode.Buttons;
    }

    private sealed class MemoryStore : IBindingOverrideStore
    {
        public string Json { get; private set; }
        public string Load() => Json;
        public void Save(string json) => Json = json;
        public void Clear() => Json = null;
    }
}
