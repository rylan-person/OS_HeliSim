#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class WaypointManagerBindingTests
{
    private GameObject managerObject;
    private WaypointManager manager;
    private GameObject firstHelicopter;
    private GameObject secondHelicopter;

    [SetUp]
    public void SetUp()
    {
        managerObject = new GameObject("Waypoint manager test");
        managerObject.SetActive(false);
        manager = managerObject.AddComponent<WaypointManager>();
        SetField("waypoints", Array.Empty<GameObject>());
        SetField("timingManagers", Array.Empty<Timing>());
        SetField("bestSectors", new List<float>());
        SetField("bestLapSectors", new List<float>());
        SetField("averageSectors", new List<float>());
        firstHelicopter = new GameObject("First helicopter");
        secondHelicopter = new GameObject("Second helicopter");
    }

    [TearDown]
    public void TearDown()
    {
        UnityEngine.Object.DestroyImmediate(managerObject);
        UnityEngine.Object.DestroyImmediate(firstHelicopter);
        UnityEngine.Object.DestroyImmediate(secondHelicopter);
    }

    [Test]
    public void BindingNewLocalHelicopter_TracksOnlyItsHierarchyAndResetsProgress()
    {
        GameObject child = new GameObject("Collider child");
        child.transform.SetParent(firstHelicopter.transform);
        manager.currentLapSectors.Add(12f);
        manager.lapActive = true;
        SetField("lastLapTime", 42f);
        SetField("lastLapDiffTime", 3f);

        manager.BindLocalHelicopter(firstHelicopter.transform, null);

        Assert.That(manager.IsTrackedHelicopter(firstHelicopter.transform), Is.True);
        Assert.That(manager.IsTrackedHelicopter(child.transform), Is.True);
        Assert.That(manager.IsTrackedHelicopter(secondHelicopter.transform), Is.False);
        Assert.That(manager.lapActive, Is.False);
        Assert.That(manager.currentLapSectors, Has.All.EqualTo(0f));
        Assert.That(GetField<float>("lastLapTime"), Is.Zero);
        Assert.That(GetField<float>("lastLapDiffTime"), Is.Zero);
    }

    [Test]
    public void UnbindingStaleHelicopter_DoesNotClearCurrentBinding()
    {
        manager.BindLocalHelicopter(firstHelicopter.transform, null);
        manager.BindLocalHelicopter(secondHelicopter.transform, null);

        manager.UnbindLocalHelicopter(firstHelicopter.transform);
        Assert.That(manager.IsTrackedHelicopter(secondHelicopter.transform), Is.True);

        manager.UnbindLocalHelicopter(secondHelicopter.transform);
        Assert.That(manager.helicopterTransform, Is.Null);
        Assert.That(manager.IsTrackedHelicopter(secondHelicopter.transform), Is.False);
    }

    [Test]
    public void DespawningViewedPlayer_ClearsTheTimingSource()
    {
        PlayerTimeTrialState state = firstHelicopter.AddComponent<PlayerTimeTrialState>();
        PlayerTimeTrialState.SetActiveState(state);

        state.OnNetworkDespawn();

        Assert.That(PlayerTimeTrialState.ActiveState, Is.Null);
    }

    [Test]
    public void CompletingLaps_UsesActualLapCountForAverageAndNoFirstLapDelta()
    {
        string path = Path.Combine(Application.temporaryCachePath, $"waypoint-test-{Guid.NewGuid():N}.csv");
        SetField("filePath", path);
        SetField("lapTimes", new List<float>());
        SetField("currentLapSectors", new List<float> { 10f });
        SetField("averageSectors", new List<float> { 0f });
        SetField("bestSectors", new List<float> { float.MaxValue });
        SetField("bestLapSectors", new List<float> { float.MaxValue });

        try
        {
            SetField("currentLapTime", 10f);
            InvokeFinishLap();
            Assert.That(GetField<float>("lastLapDiffTime"), Is.Zero);
            Assert.That(((List<float>)GetField<object>("averageSectors"))[0], Is.EqualTo(10f));

            SetField("currentLapTime", 20f);
            SetField("currentLapSectors", new List<float> { 20f });
            InvokeFinishLap();
            Assert.That(GetField<float>("lastLapDiffTime"), Is.EqualTo(10f));
            Assert.That(((List<float>)GetField<object>("averageSectors"))[0], Is.EqualTo(15f));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private void InvokeFinishLap()
    {
        typeof(WaypointManager).GetMethod("finishLap", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
    }

    private T GetField<T>(string name)
    {
        return (T)typeof(WaypointManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(manager);
    }

    private void SetField(string name, object value)
    {
        typeof(WaypointManager).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, value);
    }
}
#endif
