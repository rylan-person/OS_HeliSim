using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ControlsRebindingPageTests
{
    private readonly List<Object> created = new List<Object>();
    private InputActionAsset defaultActions;
    private MemoryStore store;
    private ControlsRebindingPage page;
    private Transform rowsRoot;
    private TMP_Text diagnostic;
    private TMP_Text hardware;
    private Button resetProfile;
    private Button keyboardTab;
    private Button joystickTab;

    [SetUp]
    public void SetUp()
    {
        defaultActions = ScriptableObject.CreateInstance<InputActionAsset>();
        created.Add(defaultActions);
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
        defaultActions.AddActionMap(map);

        store = new MemoryStore();
        rowsRoot = NewObject("Rows").transform;
        diagnostic = NewText("Diagnostic", null);
        hardware = NewText("Hardware", null);
        resetProfile = NewButton("Reset Profile", null);
        keyboardTab = NewButton("KeyboardTab", null);
        joystickTab = NewButton("JoystickTab", null);
        ControlsBindingRow rowPrefab = CreateRowPrefab();
        page = NewObject("Controls Page").AddComponent<ControlsRebindingPage>();
        page.SetControlsForTest(defaultActions, store, rowPrefab, rowsRoot, resetProfile, diagnostic, hardware);
        page.SetPresentationForTest(keyboardTab, joystickTab, null, null, null, null, null, null, null);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (Object item in created)
        {
            if (item != null) Object.DestroyImmediate(item);
        }

        created.Clear();
    }

    [Test]
    public void MissingCommands_RenderUnavailableDiagnosticRows()
    {
        page.OnPageSelected();

        ControlsBindingRow missing = FindRow("Trim");
        Assert.That(missing, Is.Not.Null);
        Assert.That(missing.transform.Find("Error").GetComponent<TMP_Text>().text, Does.Contain("no Input System action"));
        Assert.That(missing.transform.Find("Rebind").GetComponent<Button>().interactable, Is.False);
        Assert.That(hardware.text, Does.Contain("joysticks"));
        Assert.That(hardware.text, Does.Not.Contain("device ID"));
    }

    [Test]
    public void OfflinePreview_UsesCloneAndPersistsForLaterPlayerLoad()
    {
        page.OnPageSelected();
        Assert.That(page.Service.Actions, Is.Not.SameAs(defaultActions));
        Assert.That(diagnostic.text, Does.Contain("disconnected"));
        joystickTab.onClick.Invoke();

        ControlsBindingRow hardwareBinding = FindRow("Engine start");
        hardwareBinding.transform.Find("Clear").GetComponent<Button>().onClick.Invoke();

        Assert.That(page.Service.Actions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.Empty);
        Assert.That(defaultActions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
        Assert.That(store.Json, Does.Contain("bindings"));
    }

    [Test]
    public void ReopeningPage_DoesNotDuplicateListeners_AndProfileResetRestoresBindings()
    {
        page.OnPageSelected();
        joystickTab.onClick.Invoke();
        page.OnPageDeselected();
        page.OnPageSelected();
        joystickTab.onClick.Invoke();

        ControlsBindingRow hardwareBinding = FindRow("Engine start");
        hardwareBinding.transform.Find("Clear").GetComponent<Button>().onClick.Invoke();
        Assert.That(store.SaveCount, Is.EqualTo(1));

        resetProfile.onClick.Invoke();
        Assert.That(store.ClearCount, Is.EqualTo(1));
        Assert.That(page.Service.Actions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
    }

    [Test]
    public void CompositeRows_ExposeBothDirectionsAndInlineFallbackError()
    {
        page.OnPageSelected();
        ControlsBindingRow negative = FindRow("Cyclic pitch — negative");
        ControlsBindingRow positive = FindRow("Cyclic pitch — positive");
        Assert.That(negative, Is.Not.Null);
        Assert.That(positive, Is.Not.Null);
        negative.transform.Find("Clear").GetComponent<Button>().onClick.Invoke();
        Assert.That(negative.transform.Find("Error").GetComponent<TMP_Text>().text, Does.Contain("keyboard"));
        Assert.That(page.Service.GetPrimaryBindingDisplay("Pitch Input"), Does.Contain("positive"));
    }

    [Test]
    public void Tabs_FilterRowsAndKeepBothSetsOfBindingsActive()
    {
        page.OnPageSelected();

        Assert.That(FindActiveRow("Engine start — Primary 1"), Is.Not.Null);
        Assert.That(FindActiveRow("Engine start"), Is.Null);
        joystickTab.onClick.Invoke();
        Assert.That(FindActiveRow("Engine start — Primary 1"), Is.Null);
        Assert.That(FindActiveRow("Engine start"), Is.Not.Null);
        Assert.That(page.Service.Actions.FindAction("Start Engine Global").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f1"));
        Assert.That(page.Service.Actions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
    }

    [Test]
    public void JoystickTab_ShowsOneRowForMultipleFactoryBindings()
    {
        InputAction engine = defaultActions.FindAction("Start Engine Global");
        engine.AddBinding("<Joystick>/button1");
        InputRebindingService injected = new InputRebindingService(defaultActions, store);
        try
        {
            page.SetServiceForTest(injected);
            page.OnPageSelected();
            joystickTab.onClick.Invoke();

            int visibleEngineRows = 0;
            foreach (ControlsBindingRow row in rowsRoot.GetComponentsInChildren<ControlsBindingRow>(true))
            {
                if (row.gameObject.activeSelf &&
                    row.transform.Find("Command").GetComponent<TMP_Text>().text.StartsWith("Engine start"))
                {
                    visibleEngineRows++;
                }
            }

            Assert.That(visibleEngineRows, Is.EqualTo(1));
            Assert.That(FindActiveRow("Engine start"), Is.Not.Null);
            Assert.That(engine.bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
            Assert.That(engine.bindings[2].effectivePath, Is.EqualTo("<Joystick>/button1"));
        }
        finally
        {
            injected.Dispose();
        }
    }

    [Test]
    public void JoystickDirectionalComposite_KeepsBothDirectionsAssignable()
    {
        InputActionMap map = defaultActions.FindActionMap("Default");
        InputAction yaw = map.AddAction("Yaw Input", InputActionType.Value);
        yaw.AddCompositeBinding("1DAxis")
            .With("negative", "<Joystick>/hat/left")
            .With("positive", "<Joystick>/hat/right");
        InputRebindingService injected = new InputRebindingService(defaultActions, store);
        try
        {
            page.SetServiceForTest(injected);
            page.OnPageSelected();
            joystickTab.onClick.Invoke();

            Assert.That(FindActiveRow("Pedals / yaw — negative"), Is.Not.Null);
            Assert.That(FindActiveRow("Pedals / yaw — positive"), Is.Not.Null);
            Assert.That(FindActiveRow("Pedals / yaw"), Is.Null);
        }
        finally
        {
            injected.Dispose();
        }
    }

    [Test]
    public void JoystickDialog_ClearAndRestoreAffectWholeSetting()
    {
        InputAction engine = defaultActions.FindAction("Start Engine Global");
        engine.AddBinding("<Joystick>/button1");
        GameObject dialog = NewObject("Dialog");
        dialog.SetActive(false);
        Button cancel = NewButton("Cancel", dialog.transform);
        Button clear = NewButton("Clear", dialog.transform);
        Button restore = NewButton("Restore", dialog.transform);
        page.SetPresentationForTest(keyboardTab, joystickTab, dialog, null, null, null, cancel, clear, restore);
        InputRebindingService injected = new InputRebindingService(defaultActions, store);
        try
        {
            page.SetServiceForTest(injected);
            page.OnPageSelected();
            joystickTab.onClick.Invoke();
            FindActiveRow("Engine start").GetComponent<Button>().onClick.Invoke();
            clear.onClick.Invoke();

            Assert.That(engine.bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f1"));
            Assert.That(engine.bindings[1].effectivePath, Is.Empty);
            Assert.That(engine.bindings[2].effectivePath, Is.Empty);

            FindActiveRow("Engine start").GetComponent<Button>().onClick.Invoke();
            restore.onClick.Invoke();
            Assert.That(engine.bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
            Assert.That(engine.bindings[2].effectivePath, Is.EqualTo("<Joystick>/button1"));
        }
        finally
        {
            injected.Dispose();
        }
    }

    [Test]
    public void Dialog_OpenAndCancel_LeavesBindingUntouched()
    {
        GameObject dialog = NewObject("Dialog");
        dialog.SetActive(false);
        TMP_Text title = NewText("Title", dialog.transform);
        TMP_Text binding = NewText("Binding", dialog.transform);
        TMP_Text status = NewText("Status", dialog.transform);
        Button cancel = NewButton("Cancel", dialog.transform);
        Button clear = NewButton("Clear", dialog.transform);
        Button restore = NewButton("Restore", dialog.transform);
        page.SetPresentationForTest(keyboardTab, joystickTab, dialog, title, binding, status, cancel, clear, restore);
        page.OnPageSelected();

        ControlsBindingRow row = FindActiveRow("Engine start — Primary 1");
        row.GetComponent<Button>().onClick.Invoke();
        Assert.That(dialog.activeSelf, Is.True);
        Assert.That(title.text, Does.Contain("Engine start"));
        Assert.That(page.IsBindingDialogOpen, Is.True);
        Assert.That(page.TryHandleEscape(), Is.True);
        Assert.That(dialog.activeSelf, Is.False);
        Assert.That(page.Service.Actions.FindAction("Start Engine Global").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f1"));
    }

    [Test]
    public void Dialog_ClearAndRestore_ChangesOnlySelectedBinding()
    {
        GameObject dialog = NewObject("Dialog");
        dialog.SetActive(false);
        Button cancel = NewButton("Cancel", dialog.transform);
        Button clear = NewButton("Clear", dialog.transform);
        Button restore = NewButton("Restore", dialog.transform);
        page.SetPresentationForTest(keyboardTab, joystickTab, dialog, null, null, null, cancel, clear, restore);
        page.OnPageSelected();
        joystickTab.onClick.Invoke();
        FindActiveRow("Engine start").GetComponent<Button>().onClick.Invoke();

        clear.onClick.Invoke();
        Assert.That(dialog.activeSelf, Is.False);
        Assert.That(page.Service.Actions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.Empty);
        Assert.That(page.Service.Actions.FindAction("Start Engine Global").bindings[0].effectivePath, Is.EqualTo("<Keyboard>/f1"));

        FindActiveRow("Engine start").GetComponent<Button>().onClick.Invoke();
        restore.onClick.Invoke();
        Assert.That(page.Service.Actions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
    }

    private ControlsBindingRow FindActiveRow(string label)
    {
        ControlsBindingRow row = FindRow(label);
        return row != null && row.gameObject.activeSelf ? row : null;
    }

    private ControlsBindingRow FindRow(string label)
    {
        foreach (ControlsBindingRow row in rowsRoot.GetComponentsInChildren<ControlsBindingRow>(true))
        {
            TMP_Text command = row.transform.Find("Command").GetComponent<TMP_Text>();
            if (command.text == label) return row;
        }

        return null;
    }

    private ControlsBindingRow CreateRowPrefab()
    {
        GameObject root = NewObject("Binding Row Prefab");
        root.SetActive(false);
        ControlsBindingRow row = root.AddComponent<ControlsBindingRow>();
        Button rowButton = root.AddComponent<Button>();
        TMP_Text command = NewText("Command", root.transform);
        TMP_Text binding = NewText("Binding", root.transform);
        TMP_Text primary = NewText("Primary", root.transform);
        TMP_Text error = NewText("Error", root.transform);
        Button rebind = NewButton("Rebind", root.transform);
        Button clear = NewButton("Clear", root.transform);
        Button reset = NewButton("Reset", root.transform);
        row.SetControlsForTest(command, binding, primary, error, rebind, clear, reset);
        row.SetRowButtonForTest(rowButton);
        return row;
    }

    private GameObject NewObject(string name)
    {
        GameObject result = new GameObject(name);
        created.Add(result);
        return result;
    }

    private TMP_Text NewText(string name, Transform parent)
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

    private sealed class MemoryStore : IBindingOverrideStore
    {
        public string Json { get; private set; }
        public int SaveCount { get; private set; }
        public int ClearCount { get; private set; }
        public string Load() => Json;
        public void Save(string json) { Json = json; SaveCount++; }
        public void Clear() { Json = null; ClearCount++; }
    }
}
