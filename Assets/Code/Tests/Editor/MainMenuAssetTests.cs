using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class MainMenuAssetTests
{
    private const string PrefabPath = "Assets/Level/Prefabs/UI/MainMenu.prefab";
    private const string ScenePath = "Assets/Level/Scenes/MainMenu.unity";

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
