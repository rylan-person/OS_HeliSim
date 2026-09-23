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
        ControlsBindingRow rowPrefab = CreateRowPrefab();
        page = NewObject("Controls Page").AddComponent<ControlsRebindingPage>();
        page.SetControlsForTest(defaultActions, store, rowPrefab, rowsRoot, resetProfile, diagnostic, hardware);
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

        ControlsBindingRow hardwareBinding = FindRow("Engine start — Primary 2");
        hardwareBinding.transform.Find("Clear").GetComponent<Button>().onClick.Invoke();

        Assert.That(page.Service.Actions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.Empty);
        Assert.That(defaultActions.FindAction("Start Engine Global").bindings[1].effectivePath, Is.EqualTo("<Gamepad>/buttonSouth"));
        Assert.That(store.Json, Does.Contain("bindings"));
    }

    [Test]
    public void ReopeningPage_DoesNotDuplicateListeners_AndProfileResetRestoresBindings()
    {
        page.OnPageSelected();
        page.OnPageDeselected();
        page.OnPageSelected();

        ControlsBindingRow hardwareBinding = FindRow("Engine start — Primary 2");
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
        TMP_Text command = NewText("Command", root.transform);
        TMP_Text binding = NewText("Binding", root.transform);
        TMP_Text primary = NewText("Primary", root.transform);
        TMP_Text error = NewText("Error", root.transform);
        Button rebind = NewButton("Rebind", root.transform);
        Button clear = NewButton("Clear", root.transform);
        Button reset = NewButton("Reset", root.transform);
        row.SetControlsForTest(command, binding, primary, error, rebind, clear, reset);
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
