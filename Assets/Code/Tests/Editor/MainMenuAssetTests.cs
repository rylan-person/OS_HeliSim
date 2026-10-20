using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

public sealed class MainMenuAssetTests
{
    private const string PrefabPath = "Assets/Level/Prefabs/UI/MainMenu.prefab";
    private const string ScenePath = "Assets/Level/Scenes/MainMenu.unity";

    [TestCase(PrefabPath)]
    [TestCase("Assets/Level/Prefabs/UI/Menu.prefab")]
    public void Dropdowns_HaveAFullFieldHitTarget(string path)
    {
        var menu = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var dropdowns = menu.GetComponentsInChildren<TMP_Dropdown>(true);
        Assert.That(dropdowns, Is.Not.Empty);
        foreach (var dropdown in dropdowns)
        {
            var graphic = dropdown.GetComponent<Graphic>();
            Assert.That(graphic, Is.Not.Null, dropdown.name);
            Assert.That(graphic.enabled, Is.True, dropdown.name + " background must receive clicks across the field.");
            Assert.That(graphic.raycastTarget, Is.True, dropdown.name);
            Assert.That(dropdown.targetGraphic, Is.SameAs(graphic), dropdown.name);
            Assert.That(dropdown.GetComponent<UnityEngine.UI.Image>().sprite, Is.Null,
                dropdown.name + " uses a solid field background rather than an inherited transparent sprite.");
        }
    }

    [TestCase(PrefabPath)]
    [TestCase("Assets/Level/Prefabs/UI/Menu.prefab")]
    public void Dropdowns_OpenAFieldWidthListUnderTheField(string path)
    {
        var menu = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        foreach (var dropdown in menu.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            var template = dropdown.template;
            Assert.That(template, Is.Not.Null, dropdown.name);
            Assert.That(template.anchorMin, Is.EqualTo(Vector2.zero), dropdown.name);
            Assert.That(template.anchorMax, Is.EqualTo(Vector2.right), dropdown.name);
            Assert.That(template.pivot, Is.EqualTo(new Vector2(0.5f, 1f)), dropdown.name);
            Assert.That(template.anchoredPosition, Is.EqualTo(Vector2.zero), dropdown.name);
            Assert.That(template.sizeDelta.x, Is.Zero, dropdown.name);
            Assert.That(template.sizeDelta.y, Is.GreaterThanOrEqualTo(150f), dropdown.name);
            var background = template.Find("Viewport").GetComponent<UnityEngine.UI.Image>();
            Assert.That(background.enabled, Is.True, dropdown.name + " options must obscure the controls underneath.");
            Assert.That(background.color.a, Is.EqualTo(1f), dropdown.name);
            Assert.That(background.sprite, Is.Null, dropdown.name + " options require a solid background.");
            Assert.That(background.GetComponent<Mask>().showMaskGraphic, Is.True, dropdown.name);
        }
    }

    [Test]
    public void StartupScene_IsFirstEnabledScene_AndPreservesGameplay()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        Assert.That(scenes.Length, Is.GreaterThanOrEqualTo(2));
        Assert.That(scenes[0].path, Is.EqualTo(ScenePath));
        Assert.That(scenes.Any(s => s.path == "Assets/Level/Scenes/Multiplayer.unity"), Is.True);
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath), Is.Not.Null);
    }

    [Test]
    public void MenuPrefab_HasPrimaryPagesAndKeyboardInput()
    {
        var menu = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(menu, Is.Not.Null, "Dedicated menu prefab is missing.");
        foreach (string name in new[] { "FlyPage", "MultiplayerPage", "HardwarePage", "RecordingsPage", "HelpPage", "ConfirmChanges", "ConfirmQuit", "LoadingOverlay" })
            Assert.That(menu.GetComponentsInChildren<Transform>(true).Any(t => t.name == name), Is.True, name);
        Assert.That(menu.GetComponentsInChildren<EventSystem>(true).Length, Is.EqualTo(1));
        Assert.That(menu.GetComponent<CanvasScaler>(), Is.Not.Null);
        Assert.That(menu.GetComponent<GraphicRaycaster>(), Is.Not.Null);
    }

    [Test]
    public void MenuPrefab_ContainsSharedSettingsWithoutSimulationOrMotionOutput()
    {
        var menu = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(menu, Is.Not.Null);
        Assert.That(menu.GetComponentsInChildren<ControlsRebindingPage>(true).Length, Is.EqualTo(1));
        Assert.That(menu.GetComponentsInChildren<GeneralSettingsPage>(true).Length, Is.EqualTo(1));
        Assert.That(menu.GetComponentsInChildren<HeliSim.InFlightMenu.DisplaySettingsPage>(true).Length, Is.EqualTo(1));
        Assert.That(menu.GetComponentsInChildren<CesiumSettingsPage>(true).Length, Is.EqualTo(1));
        Assert.That(menu.GetComponentsInChildren<MainMenu>(true), Is.Empty);
        Assert.That(menu.GetComponentsInChildren<NovaController>(true), Is.Empty);
        Assert.That(menu.GetComponentsInChildren<Unity.Netcode.NetworkManager>(true), Is.Empty);
        Assert.That(menu.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
    }

    [Test]
    public void SharedSettings_HaveMenuOwnedPreferencesAndBindingDialog()
    {
        var menu = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Assert.That(menu, Is.Not.Null);
        foreach (var page in menu.GetComponentsInChildren<InFlightMenuPage>(true))
        {
            var serialized = new SerializedObject(page);
            var preferences = serialized.FindProperty("prefSettings");
            if (preferences != null)
            {
                var target = preferences.objectReferenceValue as Component;
                Assert.That(target, Is.Not.Null, page.name);
                Assert.That(target.transform.IsChildOf(menu.transform), Is.True, page.name);
            }
        }
        var controls = menu.GetComponentInChildren<ControlsRebindingPage>(true);
        var controlsData = new SerializedObject(controls);
        var dialog = controlsData.FindProperty("bindingDialogRoot").objectReferenceValue as GameObject;
        Assert.That(dialog, Is.Not.Null);
        Assert.That(dialog.transform.IsChildOf(menu.transform), Is.True);
        Assert.That(controlsData.FindProperty("defaultActions").objectReferenceValue, Is.Not.Null);
    }

    [Test]
    public void AssistanceChoicesMatchTheAircraftPresetOrderExactly()
    {
        var menu = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var screen = menu.GetComponent<HeliSim.FrontEnd.MainMenuScreen>();
        var dropdown = new SerializedObject(screen).FindProperty("assistance").objectReferenceValue as TMPro.TMP_Dropdown;
        var inFlightMenu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/UI/Menu.prefab").GetComponentInChildren<MainMenu>(true);
        Assert.That(dropdown.options.Skip(1).Select(option => option.text), Is.EqualTo(inFlightMenu.AssistValuesList.Select(preset => preset.name)));
    }
}
