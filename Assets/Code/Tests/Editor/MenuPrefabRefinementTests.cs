using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public sealed class MenuPrefabRefinementTests
{
    private const string MenuPath = "Assets/Level/Prefabs/UI/Menu.prefab";

    [Test]
    public void FlightPage_ContainsSessionGroupsAndNoRemovedSelectors()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
        Assert.That(menu, Is.Not.Null);
        Transform flight = Find(menu.transform, "FlightSessionPage");
        Assert.That(flight, Is.Not.Null);
        foreach (string section in new[] { "Aircraft", "Assists Section", "Time Trial", "World & Map", "Journey", "VR" })
        {
            Assert.That(Find(flight, section), Is.Not.Null, section);
        }
        foreach (string removed in new[] { "ControlScheme", "ActiveCamera", "Location" })
        {
            Assert.That(Find(flight, removed), Is.Null, removed);
        }
    }

    [Test]
    public void AdvancedAssists_StayCollapsedWithOriginalControlsPresent()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
        Transform advanced = Find(Find(menu.transform, "FlightSessionPage"), "Advanced Control Settings");
        Assert.That(advanced, Is.Not.Null);
        Assert.That(advanced.gameObject.activeSelf, Is.False);
        foreach (string control in new[] { "ToggleAutoTrim", "TailRotorTorque", "MainRotorTorque", "RollAssists", "StabiliztionAssists" })
        {
            Assert.That(Find(advanced, control), Is.Not.Null, control);
        }
    }

    [Test]
    public void CesiumTileSliders_ExposeInclusiveZeroToOneThousandRange()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
        Transform cesium = Find(menu.transform, "CesiumPage");
        foreach (string row in new[] { "MaxSimultaneousTileLoads", "LoadingDescendantLimit" })
        {
            Slider slider = Find(Find(cesium, row), "Slider").GetComponent<Slider>();
            Assert.That(slider.minValue, Is.EqualTo(0f), row);
            Assert.That(slider.maxValue, Is.EqualTo(1000f), row);
            Assert.That(slider.wholeNumbers, Is.True, row);
        }
    }

    [Test]
    public void ControlsPage_HasDeviceTabsCompactRowsAndDialogOutsideScrollViewport()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
        Transform controls = Find(menu.transform, "ControlsPage");
        Transform tabs = Find(controls, "DeviceTabs");
        Transform row = Find(controls, "BindingRowTemplate");
        Transform dialog = Find(menu.transform, "BindingDialog");

        Assert.That(Find(tabs, "KeyboardTab").GetComponent<Button>(), Is.Not.Null);
        Assert.That(Find(tabs, "JoystickTab").GetComponent<Button>(), Is.Not.Null);
        Assert.That(row.GetComponent<Button>(), Is.Not.Null);
        Assert.That(row.GetComponent<LayoutElement>().preferredHeight, Is.LessThanOrEqualTo(56));
        Assert.That(Find(controls, "CategoryHeadingTemplate"), Is.Not.Null);
        Assert.That(dialog.gameObject.activeSelf, Is.False);
        Assert.That(dialog.IsChildOf(Find(menu.transform, "PageViewport")), Is.False);
    }

    [Test]
    public void BooleanSettings_UseSegmentedOffOnVisuals()
    {
        GameObject menu = AssetDatabase.LoadAssetAtPath<GameObject>(MenuPath);
        foreach (string name in new[] { "EnableNovaPopup", "VSync", "PreloadAncestors", "EnableFogCulling" })
        {
            Transform row = Find(menu.transform, name);
            Assert.That(row.GetComponent<HeliSim.InFlightMenu.SettingsToggleVisual>(), Is.Not.Null, name);
            Assert.That(Find(row, "OffSegment"), Is.Not.Null, name);
            Assert.That(Find(row, "OnSegment"), Is.Not.Null, name);
        }
    }

    private static Transform Find(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            Transform found = Find(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
