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
