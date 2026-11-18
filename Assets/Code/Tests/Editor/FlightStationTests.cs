using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Oyedoyin.RotaryWing;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class FlightStationAssetTests
{
    [TestCase(0f, 0f, 0f, 0f)]
    [TestCase(30f, 0f, 45f, 0f)]
    [TestCase(0f, -30f, 0f, 45f)]
    public void BasicMotionCueingRespectsLocalAxesAndOneGLimit(float localX, float localZ, float roll, float pitch)
    {
        var root = new GameObject("Offline cue calculation");
        root.SetActive(false);
        try
        {
            var nova = root.AddComponent<NovaController>();
            nova.motionCueingFactor = 1f;
            nova.aAccelerationFilter = 1f;
            var heading = Quaternion.Euler(0f, 67f, 0f);
            var worldAcceleration = heading * new Vector3(localX, 0f, localZ);
            var calculate = typeof(NovaController).GetMethod("doBasicMotionCueing",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Quaternion actual = (Quaternion)calculate.Invoke(nova, new object[] { heading, worldAcceleration });
            Assert.That(Quaternion.Angle(actual, heading * Quaternion.Euler(pitch, 0f, roll)), Is.LessThan(0.02f));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void R66WiresSeatedOriginCameraAndIndependentSeatTarget()
    {
        var aircraft = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Helicopters/R66_Default.prefab");
        var recenter = aircraft.GetComponentInChildren<HelicopterComponents>(true).recenter;
        Assert.That(recenter != null && recenter.XROrigin != null && recenter.TargetTransform != null);
        Assert.That(recenter.XROrigin.Camera.transform.IsChildOf(recenter.XROrigin.transform));
        Assert.That(recenter.TargetTransform.IsChildOf(aircraft.transform));
        Assert.That(recenter.TargetTransform.IsChildOf(recenter.XROrigin.transform), Is.False,
            "Recentering must not move its own seat reference.");
    }

    [Test]
    public void MenusAndSpectatorPrefabHaveNoMotionOutputComponents()
    {
        foreach (string path in new[] { "Assets/Level/Prefabs/UI/MainMenu.prefab", "Assets/Level/Prefabs/UI/Menu.prefab",
            "Assets/Level/Prefabs/Multiplayer/SpectatorPrefab.prefab" })
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(asset, Is.Not.Null, path);
            Assert.That(asset.GetComponentsInChildren<NovaController>(true), Is.Empty, path);
        }
    }
}

// Uses the existing saved-scene, preference and Play-mode lifecycle from MainMenuFlowTests.
public sealed partial class MainMenuFlowTests
{
    [UnityTest]
    public IEnumerator SeatedRecenterMovesTheVirtualEyeWithoutMovingTheSeatOrTurningTheAircraft()
    {
        var aircraft = new GameObject("Station");
        aircraft.transform.SetPositionAndRotation(new Vector3(40, 10, -12), Quaternion.Euler(0, 65, 0));
        var seat = Child(aircraft, "Seat");
        seat.transform.localPosition = new Vector3(1, 2, 3);
        var rig = Child(aircraft, "Origin");
        rig.SetActive(false);
        var floor = Child(rig, "Tracking offset");
        var eye = Child(floor, "Virtual eye").AddComponent<Camera>();
        eye.enabled = false;
        eye.transform.localPosition = new Vector3(-0.25f, 1.15f, 0.45f);
        var origin = rig.AddComponent<XROrigin>();
        origin.Camera = eye;
        origin.Origin = rig;
        origin.CameraFloorOffsetObject = floor;
        // Synthetic tracking pose. Only Recenter controls the origin in this test.
        origin.enabled = false;
        var recenter = rig.AddComponent<Recenter>();
        recenter.XROrigin = origin;
        recenter.TargetTransform = seat.transform;
        rig.SetActive(true);
        yield return null;
        Vector3 target = seat.transform.position;
        Quaternion heading = aircraft.transform.rotation;
        Assert.That(Vector3.Distance(eye.transform.position, target), Is.GreaterThan(0.1f));
        recenter.RecenterHeadset();
        yield return null;
        Assert.That(Vector3.Distance(eye.transform.position, target), Is.LessThan(0.001f));
        Assert.That(seat.transform.position, Is.EqualTo(target));
        Assert.That(Quaternion.Angle(heading, aircraft.transform.rotation), Is.LessThan(0.001f));
        Object.Destroy(aircraft);
    }

    [UnityTest]
    public IEnumerator MissingAircraftBodyLeavesNovaInactiveAcrossPhysicsTicksAndDisable()
    {
        var root = new GameObject("Unconnected motion interface");
        root.SetActive(false);
        var nova = root.AddComponent<NovaController>();
        using (var cued = new InputAction("Cued"))
        using (var rotation = new InputAction("Rotation"))
        {
            StationField(nova, "controlTypeCued").SetValue(nova, cued);
            StationField(nova, "controlTypeRotation").SetValue(nova, rotation);
            Assert.That(root.GetComponent<Rigidbody>() == null);
            root.SetActive(true);
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(nova.active, Is.False);
            Assert.That(StationField(nova, "client").GetValue(nova), Is.Null, "No UDP socket should be opened.");
            Assert.That(cued.enabled && rotation.enabled, Is.True);
            root.SetActive(false);
            Assert.That(cued.enabled || rotation.enabled, Is.False);
            Object.Destroy(root);
        }
    }

    [UnityTest]
    public IEnumerator NewFlightResetsBodyCountdownAndCollectiveThenUnlocksForPilot()
    {
        var root = new GameObject("Session controls");
        root.SetActive(false);
        var aircraft = Child(root, "Aircraft");
        var controller = aircraft.AddComponent<RotaryController>();
        controller.enabled = false;
        var computer = aircraft.AddComponent<RotaryComputer>();
        computer.enabled = false;
        var body = aircraft.GetComponent<Rigidbody>() ?? aircraft.AddComponent<Rigidbody>();
        body.useGravity = false;
        var links = aircraft.AddComponent<HelicopterComponents>();
        links.helicopterRb = body;
        links.rotaryController = controller;
        links.rotaryComputer = computer;
        links.timeRemainingTextInternal = Child(root, "Aircraft time").AddComponent<TextMeshProUGUI>();
        var menu = Child(root, "In-flight controls").AddComponent<MainMenu>();
        menu.timeRemainingTextMenu = Child(root, "Menu time").AddComponent<TextMeshProUGUI>();
        var popup = Child(root, "Unlock collective");
        var offline = Child(root, "Offline world");
        var online = Child(root, "Online world");
        StationField(menu, "OfflineMap").SetValue(menu, offline);
        StationField(menu, "OnlineMap").SetValue(menu, online);
        StationField(menu, "OfflineMapToggle").SetValue(menu, Child(root, "Map toggle").AddComponent<Toggle>());
        StationField(menu, "unlockCollectivePopup").SetValue(menu, popup);
        computer.collectiveUpper = 14;
        computer.collectiveLower = 2;
        controller.baseRotation = Quaternion.Euler(0, 71, 0);
        controller.basePosition = new Vector3(5, 2, 3);
        root.SetActive(true);
        yield return null;
        Assert.That(menu.isTimePaused && popup.activeSelf);
        Assert.That(computer.collectiveUpper, Is.EqualTo(2));
        menu.UnlockCollective();
        Assert.That(menu.isTimePaused || popup.activeSelf, Is.False);
        Assert.That(computer.collectiveUpper, Is.EqualTo(14));
        menu.ToggleOfflineMapFromInput();
        Assert.That(offline.activeSelf && !online.activeSelf);
        menu.ToggleOfflineMapFromInput();
        Assert.That(online.activeSelf && !offline.activeSelf);
        menu.timeLimitSeconds = 12;
        menu.NewSession();
        Assert.That(body.isKinematic && menu.isTimePaused);
        float deadline = Time.realtimeSinceStartup + 12;
        while (body.isKinematic && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(body.isKinematic, Is.False);
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(Vector3.Distance(body.position, controller.basePosition), Is.LessThan(0.01f));
        Assert.That(Quaternion.Angle(body.rotation, controller.baseRotation), Is.LessThan(0.01f));
        Assert.That(popup.activeSelf && menu.isTimePaused);
        Assert.That(menu.timeRemainingTextMenu.text, Is.EqualTo("12"));
        menu.UnlockCollective();
        yield return Until(() => menu.timeRemainingTextMenu.text != "12", "flight countdown resumes after unlocking");
        Assert.That(int.Parse(menu.timeRemainingTextMenu.text), Is.LessThan(12));
        Assert.That(links.timeRemainingTextInternal.color, Is.EqualTo(Color.red));
        Object.Destroy(root);
    }

    private static GameObject Child(GameObject parent, string name)
    {
        var child = new GameObject(name);
        child.transform.SetParent(parent.transform, false);
        return child;
    }

    private static FieldInfo StationField(object target, string name)
    {
        return target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    }
}
