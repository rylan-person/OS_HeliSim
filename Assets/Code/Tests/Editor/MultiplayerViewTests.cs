using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public class MultiplayerViewTests
{
    [Test]
    public void RemoteSetupNeverReparentsSceneCameraTarget()
    {
        var world = new GameObject("World");
        var target = new GameObject("CameraTarget");
        var helicopter = new GameObject("Remote helicopter");
        try
        {
            target.transform.SetParent(world.transform);
            GameObjectTarget.Register("CameraTarget", target);
            var setup = helicopter.AddComponent<LocalPlayerSetup>();
            setup.localOnlyScripts = Array.Empty<Behaviour>();
            setup.localOnlyObjects = Array.Empty<GameObject>();
            setup.OnNetworkSpawn();
            Assert.That(target.transform.parent, Is.SameAs(world.transform));
        }
        finally
        {
            GameObjectTarget.Clear();
            UnityEngine.Object.DestroyImmediate(helicopter);
            UnityEngine.Object.DestroyImmediate(world);
        }
    }

    [Test]
    public void HelicopterPrefabRegistersSpawnLifecycleAndDisablesRemoteFlightController()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Helicopters/R66_Default.prefab");
        var registryType = typeof(LocalPlayerSetup).Assembly.GetType("NetworkHelicopterRegistry");
        Assert.That(registryType, Is.Not.Null, "Helicopters must publish local spawn/despawn events");
        Assert.That(prefab.GetComponent(registryType), Is.Not.Null);
        var setup = prefab.GetComponent<LocalPlayerSetup>();
        var controller = Array.Find(prefab.GetComponents<Behaviour>(), c => c != null && c.GetType().Name == "RotaryController");
        Assert.That(controller, Is.Not.Null);
        Assert.That(setup.localOnlyScripts, Does.Contain(controller));
    }

    [Test]
    public void TelemetryDestroyCallbackClearsReferenceAndNotifiesViews()
    {
        var source = new GameObject("Telemetry source");
        var collector = source.AddComponent<ClientTelemetryCollector>();
        ClientTelemetryCollector.SetActiveCollector(collector);
        bool notified = false;
        Action<ClientTelemetryCollector> changed = value => notified = ReferenceEquals(value, null);
        ClientTelemetryCollector.ActiveCollectorChanged += changed;
        try
        {
            // EditMode AddComponent does not run the runtime Awake/OnDestroy lifecycle.
            collector.OnDestroy();
            Assert.That(ReferenceEquals(ClientTelemetryCollector.ActiveCollector, null), Is.True);
            Assert.That(notified, Is.True);
        }
        finally
        {
            ClientTelemetryCollector.ActiveCollectorChanged -= changed;
            ClientTelemetryCollector.SetActiveCollector(null);
            if (source != null) UnityEngine.Object.DestroyImmediate(source);
        }
    }

    [Test]
    public void FollowerSwitchesPhysicsAndDataSourcesWithoutChangingParent()
    {
        var world = new GameObject("World");
        var anchor = new GameObject("CameraTarget");
        var first = new GameObject("First");
        var second = new GameObject("Second");
        var camera = new GameObject("Cinematic");
        try
        {
            anchor.transform.SetParent(world.transform);
            var follower = anchor.AddComponent<CameraTargetFollower>();
            var firstRb = first.AddComponent<Rigidbody>();
            var secondRb = second.AddComponent<Rigidbody>();
            var firstData = first.AddComponent<ClientTelemetryCollector>();
            var secondData = second.AddComponent<ClientTelemetryCollector>();
            var firstTrial = first.AddComponent<PlayerTimeTrialState>();
            var secondTrial = second.AddComponent<PlayerTimeTrialState>();
            first.transform.position = new Vector3(50, 20, 30);
            second.transform.position = new Vector3(-500, 200, 300);
            var auto = camera.AddComponent<AutoCam>();
            auto.target = anchor.transform;
            auto.maxDistance = float.MaxValue;
            follower.SetTarget(first.transform);
            InvokeCallback(auto, "LateUpdate");
            Assert.That(auto.targetRb, Is.SameAs(firstRb));
            follower.SetTarget(second.transform);
            InvokeCallback(auto, "LateUpdate");
            Assert.That(auto.targetRb, Is.SameAs(secondRb));
            Assert.That(ClientTelemetryCollector.ActiveCollector, Is.SameAs(secondData));
            Assert.That(PlayerTimeTrialState.ActiveState, Is.SameAs(secondTrial));
            Assert.That(anchor.transform.position, Is.EqualTo(second.transform.position));
            Assert.That(anchor.transform.parent, Is.SameAs(world.transform));
            var smoothing = typeof(AutoCam).GetField("smoothedVelocity", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.That((Vector3)smoothing.GetValue(auto), Is.EqualTo(Vector3.zero));
            follower.SetTarget(null);
            InvokeCallback(auto, "LateUpdate");
            Assert.That(auto.targetRb, Is.Null);
            Assert.That(ClientTelemetryCollector.ActiveCollector, Is.Null);
            Assert.That(PlayerTimeTrialState.ActiveState, Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(camera);
            UnityEngine.Object.DestroyImmediate(world);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
            ClientTelemetryCollector.SetActiveCollector(null);
            PlayerTimeTrialState.SetActiveState(null);
        }
    }

    [Test]
    public void FpvResolvesSelectedCockpitAndClearsMissingAnchor()
    {
        var anchor = new GameObject("CameraTarget");
        var first = new GameObject("First");
        var second = new GameObject("Second");
        var cockpit = new GameObject("Cockpit");
        var camera = new GameObject("FPV");
        try
        {
            cockpit.tag = "fpv_view";
            cockpit.transform.SetParent(first.transform);
            var follower = anchor.AddComponent<CameraTargetFollower>();
            var fpv = camera.AddComponent<fpv_camLock>();
            fpv.cinemachineCamera = camera.AddComponent<Unity.Cinemachine.CinemachineCamera>();
            fpv.cameraTarget = anchor;
            follower.SetTarget(first.transform);
            InvokeCallback(fpv, "Update");
            Assert.That(fpv.cinemachineCamera.Follow, Is.SameAs(cockpit.transform));
            follower.SetTarget(second.transform);
            InvokeCallback(fpv, "Update");
            Assert.That(fpv.validTarget, Is.False);
            Assert.That(fpv.cinemachineCamera.Follow, Is.Null);
            follower.SetTarget(null);
            InvokeCallback(fpv, "Update");
            Assert.That(fpv.fpv_view, Is.Null);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(camera);
            UnityEngine.Object.DestroyImmediate(anchor);
            UnityEngine.Object.DestroyImmediate(first);
            UnityEngine.Object.DestroyImmediate(second);
        }
    }
    private static void InvokeCallback(object component, string method)
    {
        component.GetType().GetMethod(method, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(component, null);
    }
}
