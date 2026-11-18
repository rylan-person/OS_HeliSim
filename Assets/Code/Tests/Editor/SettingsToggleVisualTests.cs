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

        InvokeLifecycle(visual, "Awake");
        try
        {
            InvokeLifecycle(visual, "OnEnable");
            toggle.isOn = false;
            Color selectedOffBackground = offBackground.color;
            Color unselectedOnBackground = onBackground.color;
            Color selectedOffText = offLabel.color;
            Color unselectedOnText = onLabel.color;

            Assert.That(selectedOffBackground, Is.Not.EqualTo(unselectedOnBackground));
            Assert.That(selectedOffText, Is.Not.EqualTo(unselectedOnText));

            toggle.isOn = true;

            // Deliberate change (menu redesign, 2026-11-17): selected On is teal and selected Off
            // is neutral, so the two states no longer share one highlight colour.
            Assert.That(onBackground.color, Is.Not.EqualTo(selectedOffBackground),
                "On and Off must be distinguishable when selected.");
            Assert.That(offBackground.color, Is.EqualTo(unselectedOnBackground));
            Assert.That(offLabel.color, Is.EqualTo(unselectedOnText));
            Color selectedOnBackground = onBackground.color;
            Color selectedOnText = onLabel.color;
            Assert.That(selectedOnBackground, Is.Not.EqualTo(offBackground.color));
            Assert.That(selectedOnText, Is.Not.EqualTo(offLabel.color));

            toggle.SetIsOnWithoutNotify(false);
            InvokeLateUpdate(visual);

            Assert.That(offBackground.color, Is.EqualTo(selectedOffBackground));
            Assert.That(onBackground.color, Is.EqualTo(unselectedOnBackground));
            Assert.That(offLabel.color, Is.EqualTo(selectedOffText));
            Assert.That(onLabel.color, Is.EqualTo(unselectedOnText));
        }
        finally
        {
            InvokeLifecycle(visual, "OnDisable");
        }
    }

    [Test]
    public void Refresh_WithTheme_UsesAccentForOnAndNeutralForOffAndDimsWhenDisabled()
    {
        GameObject root = CreateGameObject("ThemedToggle");
        root.SetActive(false);
        Toggle toggle = root.AddComponent<Toggle>();
        Image offBackground = CreateComponent<Image>("OffBackground");
        Image onBackground = CreateComponent<Image>("OnBackground");
        TMP_Text offLabel = CreateComponent<TextMeshProUGUI>("OffLabel");
        TMP_Text onLabel = CreateComponent<TextMeshProUGUI>("OnLabel");
        HeliSim.UI.Theme.MenuTheme theme = ScriptableObject.CreateInstance<HeliSim.UI.Theme.MenuTheme>();
        createdObjects.Add(theme);

        SettingsToggleVisual visual = root.AddComponent<SettingsToggleVisual>();
        SetField(visual, "settingToggle", toggle);
        SetField(visual, "offLabel", offLabel);
        SetField(visual, "onLabel", onLabel);
        SetField(visual, "offBackground", offBackground);
        SetField(visual, "onBackground", onBackground);
        visual.SetTheme(theme);

        toggle.SetIsOnWithoutNotify(true);
        visual.Refresh();
        Assert.That(onBackground.color, Is.EqualTo(theme.Palette.accent));
        Assert.That(onLabel.color, Is.EqualTo(theme.Palette.accentInk));

        toggle.SetIsOnWithoutNotify(false);
        visual.Refresh();
        Assert.That(offBackground.color, Is.EqualTo(theme.Palette.bg4));
        Color enabledOff = offBackground.color;

        toggle.interactable = false;
        visual.Refresh();
        Assert.That(offBackground.color.a, Is.LessThan(enabledOff.a));
        Assert.That(offBackground.color, Is.Not.EqualTo(enabledOff));
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
        InvokeLifecycle(visual, "LateUpdate");
    }

    private static void InvokeLifecycle(SettingsToggleVisual visual, string methodName)
    {
        MethodInfo method = typeof(SettingsToggleVisual).GetMethod(
            methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"Expected SettingsToggleVisual.{methodName} lifecycle method.");
        method.Invoke(visual, null);
    }
}
