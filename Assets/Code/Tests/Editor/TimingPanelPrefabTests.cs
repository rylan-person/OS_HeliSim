using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class TimingPanelPrefabTests
{
    private const string PanelPath = "Assets/Level/Prefabs/Dashboard/Panels/TimingPanel.prefab";

    [Test]
    public void TimingPanel_UsesTheDashboardTimingPanel()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);

        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<DashboardPanel>().PanelName, Is.EqualTo("Timing"));
    }

    [Test]
    public void TimingPanel_ContainsTheExistingSectorAndLapLayout()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PanelPath);

        Assert.That(prefab, Is.Not.Null);
        Timing timing = prefab.GetComponentInChildren<Timing>(true);
        Assert.That(timing, Is.Not.Null);
        Assert.That(timing.sectorTimesText, Has.Length.EqualTo(5));
        Assert.That(timing.sectorTimesDifferenceText, Has.Length.EqualTo(5));
    }
}
