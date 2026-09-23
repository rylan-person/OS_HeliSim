using System.Collections.Generic;
using System.Reflection;
using HeliSim.InFlightMenu;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SettingsToggleVisualTests
{
    private readonly List<Object> createdObjects = new List<Object>();

    [TearDown]
    public void TearDown()
    {
        foreach (Object createdObject in createdObjects)
        {
            Object.DestroyImmediate(createdObject);
        }

        createdObjects.Clear();
    }

    [Test]
    public void Refresh_HighlightsOnlyTheSelectedSideAndFollowsToggleChanges()
    {
        GameObject root = CreateGameObject("SettingToggle");
        root.SetActive(false);
        Toggle toggle = root.AddComponent<Toggle>();
        Image offBackground = CreateComponent<Image>("OffBackground");
        Image onBackground = CreateComponent<Image>("OnBackground");
        TMP_Text offLabel = CreateComponent<TextMeshProUGUI>("OffLabel");
        TMP_Text onLabel = CreateComponent<TextMeshProUGUI>("OnLabel");

        SettingsToggleVisual visual = root.AddComponent<SettingsToggleVisual>();
        SetField(visual, "settingToggle", toggle);
        SetField(visual, "offLabel", offLabel);
        SetField(visual, "onLabel", onLabel);
        SetField(visual, "offBackground", offBackground);
        SetField(visual, "onBackground", onBackground);

        toggle.isOn = false;
        root.SetActive(true);
        Color selectedOffBackground = offBackground.color;
        Color unselectedOnBackground = onBackground.color;
        Color selectedOffText = offLabel.color;
        Color unselectedOnText = onLabel.color;

        Assert.That(selectedOffBackground, Is.Not.EqualTo(unselectedOnBackground));
        Assert.That(selectedOffText, Is.Not.EqualTo(unselectedOnText));

        toggle.isOn = true;

        Assert.That(onBackground.color, Is.EqualTo(selectedOffBackground));
        Assert.That(offBackground.color, Is.EqualTo(unselectedOnBackground));
        Assert.That(onLabel.color, Is.EqualTo(selectedOffText));
        Assert.That(offLabel.color, Is.EqualTo(unselectedOnText));

        toggle.SetIsOnWithoutNotify(false);
        InvokeLateUpdate(visual);

        Assert.That(offBackground.color, Is.EqualTo(selectedOffBackground));
        Assert.That(onBackground.color, Is.EqualTo(unselectedOnBackground));
        Assert.That(offLabel.color, Is.EqualTo(selectedOffText));
        Assert.That(onLabel.color, Is.EqualTo(unselectedOnText));
    }

    private GameObject CreateGameObject(string name)
    {
        GameObject gameObject = new GameObject(name);
        createdObjects.Add(gameObject);
        return gameObject;
    }

    private T CreateComponent<T>(string name) where T : Component
    {
        GameObject gameObject = CreateGameObject(name);
        return gameObject.AddComponent<T>();
    }

    private static void SetField(object instance, string fieldName, object value)
    {
        FieldInfo field = instance.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"Expected serialized field '{fieldName}'.");
        field.SetValue(instance, value);
    }

    private static void InvokeLateUpdate(SettingsToggleVisual visual)
    {
        MethodInfo lateUpdate = typeof(SettingsToggleVisual).GetMethod(
            "LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(lateUpdate, Is.Not.Null, "Expected the visual to detect silent Toggle changes during LateUpdate.");
        lateUpdate.Invoke(visual, null);
    }
}
