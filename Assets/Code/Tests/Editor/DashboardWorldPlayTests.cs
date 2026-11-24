using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CesiumForUnity;
using HeliSim.InFlightMenu;
using Mapbox.Unity.Map;
using Mapbox.Unity;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Runs shipped dashboard assets through real player-loop frames without map services or hardware.</summary>
public sealed class DashboardWorldPlayTests
{
    private const string StateKey = "HeliSim.DashboardWorldPlayTests.Setup";
    private const string FolderKey = "HeliSim.DashboardWorldPlayTests.Folder";
    private const string DashboardPath = "Assets/Level/Prefabs/UI/DashboardSystem.prefab";
    private const string TokenTargetKey = "HeliSim.MapServiceTokens.TestTarget";
    private const string PreviousTokenTargetKey = "HeliSim.DashboardWorldPlayTests.PreviousTokenTarget";

    [Serializable] private sealed class Setup { public Record[] scenes; }
    [Serializable] private sealed class Record { public string path; public bool isLoaded; public bool isActive; }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Assert.That(EditorApplication.isPlaying, Is.False, "Run this fixture from EditMode.");
        Assert.That(SessionState.GetString(FolderKey, ""), Is.Empty, "Previous fixture cleanup is incomplete.");
        string authoring = SessionState.GetString(MultiplayerVerificationRunner.AuthoringSetupKey, "");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Assert.That(scene.isDirty, Is.False, "Save authoring scenes before verification.");
            if (string.IsNullOrEmpty(scene.path)) Assert.That(authoring, Is.Not.Empty, "Use MultiplayerVerificationRunner to preserve authoring scenes.");
        }
        SessionState.SetString(StateKey, string.IsNullOrEmpty(authoring)
            ? JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup().Select(s => new Record
                { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive }).ToArray() }) : authoring);
        string folder = "Assets/__DashboardWorldTests_" + Guid.NewGuid().ToString("N");
        SessionState.SetString(FolderKey, folder);
        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        Scene isolated = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Assert.That(EditorSceneManager.SaveScene(isolated, folder + "/Isolated.unity"), Is.True);
        SessionState.SetString(PreviousTokenTargetKey, SessionState.GetString(TokenTargetKey, ""));
        SessionState.SetString(TokenTargetKey, "HeliSim/MapServiceTokens/Tests/" + Guid.NewGuid().ToString("N"));
        Assert.That(MapServiceTokens.TryForget(), Is.True, "Only the isolated synthetic credential target may be used.");
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        string saved = SessionState.GetString(StateKey, "");
        if (!string.IsNullOrEmpty(saved))
        {
            Record[] records = JsonUtility.FromJson<Setup>(saved).scenes;
            EditorSceneManager.RestoreSceneManagerSetup(records.Select(s => new SceneSetup
                { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive }).ToArray());
        }
        string folder = SessionState.GetString(FolderKey, "");
        if (!string.IsNullOrEmpty(folder)) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "Temporary scene cleanup failed.");
        if (SessionState.GetString(PreviousTokenTargetKey, "<not-active>") != "<not-active>")
        {
            try { Assert.That(MapServiceTokens.TryForget(), Is.True); }
            finally
            {
                string previous = SessionState.GetString(PreviousTokenTargetKey, "");
                if (string.IsNullOrEmpty(previous)) SessionState.EraseString(TokenTargetKey);
                else SessionState.SetString(TokenTargetKey, previous);
                SessionState.EraseString(PreviousTokenTargetKey);
            }
        }
        SessionState.EraseString(StateKey);
        SessionState.EraseString(FolderKey);
    }

    [UnityTest]
    public IEnumerator ShippedDashboard_FullscreenHidesOtherCamerasAndRestoresGrid()
    {
        CreateWorldAndCameras();
        DashboardManager manager = CreateDashboard();
        yield return null;
        var panel = (CameraDashboardPanel)manager.topLeft.GetCurrentPanel();
        Camera follow = DashboardCameraRegistry.GetCamera(PanelCameraType.Follow);
        Camera orbit = DashboardCameraRegistry.GetCamera(PanelCameraType.Orbit);
        Assert.That(follow.targetTexture.IsCreated(), Is.True);
        Assert.That(follow.targetTexture.width, Is.EqualTo(1280));
        RenderTexture original = follow.targetTexture;
        manager.FocusSlot(manager.topLeft);
        yield return null;
        Assert.That(manager.focusedSlot, Is.SameAs(manager.topLeft));
        Assert.That(panel.transform.parent, Is.SameAs(manager.fullscreenRoot));
        Assert.That(manager.gridRoot.gameObject.activeSelf, Is.False);
        Assert.That(orbit.enabled, Is.False, "Hidden dashboard cameras must stop rendering.");
        Assert.That(follow.enabled, Is.True);
        Assert.That(follow.targetTexture.width, Is.EqualTo(1920));
        Assert.That(follow.targetTexture.height, Is.EqualTo(1080));
        Assert.That(original == null, Is.True, "Fullscreen replacement must destroy the old texture.");
        manager.Unfocus();
        yield return null;
        Assert.That(manager.focusedSlot, Is.Null);
        Assert.That(panel.transform.parent, Is.SameAs(manager.topLeft.transform));
        Assert.That(manager.gridRoot.gameObject.activeSelf, Is.True);
        Assert.That(orbit.enabled, Is.True);
        Assert.That(follow.targetTexture.width, Is.EqualTo(1280));
    }

    [UnityTest]
    public IEnumerator ShippedDashboard_AllCameraChoicesRenderPixelsAndReleaseOnReassignment()
    {
        CreateWorldAndCameras();
        DashboardManager manager = CreateDashboard();
        yield return null;
        // Free the other default camera types before visiting every shipped camera choice.
        manager.topRight.ClearPanel();
        manager.bottomLeft.ClearPanel();
        foreach (DashboardPanelType choice in new[] { DashboardPanelType.FollowCamera, DashboardPanelType.DynamicCamera,
            DashboardPanelType.FPVCamera, DashboardPanelType.OrbitCamera, DashboardPanelType.TopDownCamera })
        {
            manager.SetPanelBySlotIndex(0, (int)choice);
            yield return null;
            var panel = manager.topLeft.GetCurrentPanel() as CameraDashboardPanel;
            Assert.That(panel, Is.Not.Null, choice.ToString());
            Camera camera = DashboardCameraRegistry.GetCamera(panel.CameraType);
            RawImage image = Field<RawImage>(panel, "displayImage");
            RenderTexture texture = camera.targetTexture;
            Assert.That(texture, Is.Not.Null);
            Assert.That(image.texture, Is.SameAs(texture));
            yield return null;
            yield return null;
            Color pixel = ReadPixel(texture);
            Assert.That(pixel.r, Is.GreaterThan(0.8f), "The actual camera must render its magenta clear color.");
            Assert.That(pixel.b, Is.GreaterThan(0.8f));
            Assert.That(pixel.g, Is.LessThan(0.2f));
            manager.SetPanelBySlotIndex(0, (int)DashboardPanelType.Empty);
            yield return null;
            Assert.That(camera.enabled, Is.False);
            Assert.That(texture == null, Is.True, "Reassignment must release and destroy the previous render texture.");
        }
    }

    [UnityTest]
    public IEnumerator ShippedDashboard_ReassignmentWhileFullscreenPreservesFocusAndOwner()
    {
        CreateWorldAndCameras();
        DashboardManager manager = CreateDashboard();
        yield return null;
        manager.FocusSlot(manager.topLeft);
        manager.SetPanelBySlotIndex(0, (int)DashboardPanelType.TopDownCamera);
        yield return null;
        DashboardPanel panel = manager.topLeft.GetCurrentPanel();
        Assert.That(manager.focusedSlot, Is.SameAs(manager.topLeft));
        Assert.That(panel.OwnerSlot, Is.SameAs(manager.topLeft));
        Assert.That(panel.transform.parent, Is.SameAs(manager.fullscreenRoot));
        Assert.That(DashboardCameraRegistry.GetCamera(PanelCameraType.Follow).enabled, Is.False);
        Assert.That(DashboardCameraRegistry.GetCamera(PanelCameraType.TopDown).targetTexture.width, Is.EqualTo(1920));
        manager.Unfocus();
        yield return null;
        Assert.That(panel.transform.parent, Is.SameAs(manager.topLeft.transform));
    }

    [UnityTest]
    public IEnumerator ShippedStreetMap_WithoutMapServicesRemainsUsableAcrossShowHideAndFullscreen()
    {
        CreateWorldAndCameras();
        DashboardManager manager = CreateDashboard();
        yield return null;
        manager.SetPanelBySlotIndex(0, (int)DashboardPanelType.StreetMap);
        yield return null;
        var panel = manager.topLeft.GetCurrentPanel() as StreetMapDashboardPanel;
        Assert.That(panel, Is.Not.Null);
        Assert.That(Field<RawImage>(panel, "displayImage"), Is.Not.Null);
        Assert.That(Field<Image>(panel, "helicopterMarker").enabled, Is.False);
        Assert.That(Field<Camera>(panel, "mapCamera"), Is.Null, "No map or service request should be created without a tracker.");
        manager.FocusSlot(manager.topLeft);
        yield return null;
        manager.Unfocus();
        manager.SetPanelBySlotIndex(0, (int)DashboardPanelType.Empty);
        yield return null;
        Assert.That(panel == null, Is.True);
    }

    [UnityTest]
    public IEnumerator ShippedStreetMap_OfflineMapAllocatesResizesAndReleasesItsCameraTexture()
    {
        // Supply only an in-memory synthetic credential; the disabled map cannot send requests.
        using var token = new SyntheticMapboxToken();
        CreateWorldAndCameras();
        var mapObject = new GameObject("OfflineStreetMap");
        mapObject.SetActive(false);
        var map = mapObject.AddComponent<AbstractMap>();
        map.enabled = false; // Awake may initialize its visualizer; Start never requests service access or tiles.
        mapObject.SetActive(true);
        var tracker = new GameObject("OfflineMapTracker").AddComponent<HelicopterMapTracker>();
        SetField(tracker, "map", map);
        DashboardManager manager = CreateDashboard();
        yield return null;
        manager.SetPanelBySlotIndex(0, (int)DashboardPanelType.StreetMap);
        yield return null;
        var panel = (StreetMapDashboardPanel)manager.topLeft.GetCurrentPanel();
        Camera camera = Field<Camera>(panel, "mapCamera");
        RawImage image = Field<RawImage>(panel, "displayImage");
        Assert.That(camera, Is.Not.Null);
        Assert.That(camera.enabled, Is.True);
        Assert.That(camera.orthographic, Is.True);
        Assert.That(camera.cullingMask, Is.EqualTo(1 << 8));
        RenderTexture original = camera.targetTexture;
        Assert.That(original.IsCreated(), Is.True);
        Assert.That(image.texture, Is.SameAs(original));
        manager.FocusSlot(manager.topLeft);
        yield return null;
        yield return null;
        Assert.That(camera.targetTexture.width, Is.InRange(256, 1920));
        Assert.That(camera.targetTexture.height, Is.InRange(256, 1080));
        manager.Unfocus();
        panel.OnPanelHidden();
        yield return null;
        Assert.That(camera.enabled, Is.False);
        Assert.That(camera.targetTexture, Is.Null);
        Assert.That(image.texture, Is.Null);
        Assert.That(original == null, Is.True);
        panel.OnPanelShown();
        yield return null;
        Assert.That(camera.enabled, Is.True);
        Assert.That(camera.targetTexture.IsCreated(), Is.True);
        manager.SetPanelBySlotIndex(0, (int)DashboardPanelType.Empty);
        yield return null;
        yield return null;
        Assert.That(camera == null, Is.True, "The map panel owns and destroys its camera.");
    }

    [UnityTest]
    public IEnumerator MapTracker_FollowsPhysicalBodyAcrossFramesWithoutCreatingRemoteMap()
    {
        var geo = new GameObject("IsolatedGeoreference", typeof(CesiumGeoreference));
        var proxy = new GameObject("MapTracker");
        proxy.transform.SetParent(geo.transform);
        proxy.AddComponent<CesiumGlobeAnchor>();
        var tracker = proxy.AddComponent<HelicopterMapTracker>();
        var body = new GameObject("FlightBody", typeof(Rigidbody)).GetComponent<Rigidbody>();
        body.isKinematic = true;
        SetField(tracker, "helicopterTransform", body.transform);
        body.position = new Vector3(80f, 40f, -120f);
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(Vector3.Distance(proxy.transform.position, body.position), Is.LessThan(0.001f));
        Assert.That(tracker.HasHelicopter, Is.True);
        Assert.That(tracker.Map, Is.Null);
        Object.Destroy(body.gameObject);
        yield return null;
        Assert.That(tracker.SyncToHelicopter(), Is.False);
        tracker.SetMapVisible(false);
        yield return null;
    }

    [UnityTest]
    public IEnumerator RuntimeSettings_ApplyAudioAndOfflineCesiumWithoutPersistence()
    {
        float originalVolume = AudioListener.volume;
        var settingsObject = new GameObject("IsolatedSettings");
        settingsObject.SetActive(false);
        var settings = settingsObject.AddComponent<PrefSettings>();
        var tilesObject = new GameObject("OfflineTileset");
        tilesObject.SetActive(false); // Native tileset is never enabled and cannot request remote tiles.
        var tiles = tilesObject.AddComponent<Cesium3DTileset>();
        SetField(settings, "cesiumAsset", tiles);
        try
        {
            settings.volume = 70;
            settings.screenSpaceError = 48;
            settings.MaximumSimultaneousTileLoads = 0;
            settings.LoadingDescendantLimit = 0;
            settings.preloadAncestors = false;
            settings.enableFogCulling = true;
            settings.ApplyToScene();
            yield return null;
            Assert.That(AudioListener.volume, Is.EqualTo(0.35f).Within(0.001f));
            Assert.That(tiles.maximumScreenSpaceError, Is.EqualTo(48));
            Assert.That(tiles.maximumSimultaneousTileLoads, Is.EqualTo(0));
            Assert.That(tiles.loadingDescendantLimit, Is.EqualTo(0));
            Assert.That(tiles.preloadAncestors, Is.False);
            Assert.That(tiles.enableFogCulling, Is.True);
            Object.Destroy(tilesObject);
            yield return null;
            settings.volume = 100;
            Assert.DoesNotThrow(settings.ApplyToScene, "Missing terrain must not prevent audio application.");
            Assert.That(AudioListener.volume, Is.EqualTo(0.5f).Within(0.001f));
        }
        finally { AudioListener.volume = originalVolume; }
    }

    [UnityTest]
    public IEnumerator ShippedFpvPipeline_TracksMovingRotatingTargetSwitchesAndClearsDestroyedTarget()
    {
        // Same pipeline and zero damping as Multiplayer.unity's FPVCamRig; no scene services are loaded.
        var anchor = new GameObject("CameraTarget").AddComponent<CameraTargetFollower>();
        var output = new GameObject("FPVCam", typeof(Camera), typeof(CinemachineBrain));
        output.GetComponent<Camera>().enabled = false;
        var brain = output.GetComponent<CinemachineBrain>();
        brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
        var rig = new GameObject("FPVCamRig");
        rig.SetActive(false);
        var vcam = rig.AddComponent<CinemachineCamera>();
        rig.AddComponent<CinemachineHardLockToTarget>().Damping = 0;
        rig.AddComponent<CinemachineRotateWithFollowTarget>().Damping = 0;
        var fpv = rig.AddComponent<fpv_camLock>();
        fpv.cameraTarget = anchor.gameObject;
        fpv.cinemachineCamera = vcam;
        rig.SetActive(true);
        var first = new GameObject("FirstHelicopter");
        var firstView = new GameObject("FirstFpvView");
        firstView.tag = "fpv_view";
        firstView.transform.SetParent(first.transform, false);
        firstView.transform.localPosition = new Vector3(0, 2, 1);
        anchor.SetTarget(first.transform);
        first.transform.SetPositionAndRotation(new Vector3(15, 30, -20), Quaternion.Euler(12, 40, -15));
        yield return null;
        yield return null;
        Assert.That(fpv.validTarget, Is.True);
        Assert.That(vcam.Follow, Is.SameAs(firstView.transform));
        Assert.That(Vector3.Distance(output.transform.position, firstView.transform.position), Is.LessThan(0.01f));
        Assert.That(Quaternion.Angle(output.transform.rotation, firstView.transform.rotation), Is.LessThan(0.1f));
        first.transform.SetPositionAndRotation(new Vector3(-40, 80, 55), Quaternion.Euler(-18, 120, 25));
        yield return null;
        yield return null;
        Assert.That(Vector3.Distance(output.transform.position, firstView.transform.position), Is.LessThan(0.01f));
        Assert.That(Quaternion.Angle(output.transform.rotation, firstView.transform.rotation), Is.LessThan(0.1f));
        var second = new GameObject("SecondHelicopter");
        var secondView = new GameObject("SecondFpvView");
        secondView.tag = "fpv_view";
        secondView.transform.SetParent(second.transform, false);
        second.transform.SetPositionAndRotation(new Vector3(100, 45, 10), Quaternion.Euler(0, 230, 0));
        anchor.SetTarget(second.transform);
        yield return null;
        yield return null;
        Assert.That(vcam.Follow, Is.SameAs(secondView.transform));
        Assert.That(Vector3.Distance(output.transform.position, secondView.transform.position), Is.LessThan(0.01f));
        Assert.That(Quaternion.Angle(output.transform.rotation, secondView.transform.rotation), Is.LessThan(0.1f));
        Object.Destroy(second);
        yield return null;
        yield return null;
        Assert.That(anchor.SelectedTarget, Is.Null);
        Assert.That(fpv.validTarget, Is.False);
        Assert.That(vcam.Follow, Is.Null);
        Assert.That(anchor.transform.position, Is.EqualTo(Vector3.zero), "Losing the selected aircraft restores the scene anchor.");
    }

    [UnityTest]
    public IEnumerator ShippedInstrumentHud_DisplaysSiTelemetryAndChangesWarningColorsAcrossFrames()
    {
        var source = new GameObject("DeterministicHudTelemetry");
        source.SetActive(false);
        var controller = source.AddComponent<Oyedoyin.Common.Controller>();
        var core = source.AddComponent<Oyedoyin.Common.SilantroCore>();
        controller.enabled = false;
        core.enabled = false;
        controller.m_core = core;
        Rigidbody body = source.GetComponent<Rigidbody>();
        body.isKinematic = true;
        var camera = new GameObject("InstrumentCamera", typeof(Camera)).GetComponent<Camera>();
        camera.enabled = false;
        var container = new GameObject("IsolatedInstrumentHud");
        container.SetActive(false);
        // R66_Default embeds this active HUD. UI/StaticUI.prefab is an inactive older template.
        GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/StaticUI 1.prefab");
        Assert.That(asset, Is.Not.Null);
        GameObject instance = Object.Instantiate(asset, container.transform);
        var hud = instance.GetComponentInChildren<HelicopterHUD>(true);
        Assert.That(hud, Is.Not.Null);
        SetField(hud, "camera", camera);
        SetField(hud, "rb", body);
        SetField(hud, "velocityRoot", new GameObject("VelocityRoot", typeof(RectTransform)).GetComponent<RectTransform>());
        hud.m_vehicle = controller;
        foreach (Compass compass in instance.GetComponentsInChildren<Compass>(true))
        {
            compass.SetCamera(camera);
            compass.SetPlane(body.transform);
        }
        foreach (PitchLadder ladder in instance.GetComponentsInChildren<PitchLadder>(true))
        {
            ladder.SetCamera(camera);
            SetField(ladder, "planeTransform", body.transform);
        }
        core.δz = -12.5;
        core.z = 20;
        core.n = 4.5;
        core.u = 3;
        core.v = 4;
        controller.m_powerLevel = 0.75f;
        controller._collectiveInput = 0.4f;
        container.SetActive(true);
        Assert.That(hud.isActiveAndEnabled, Is.True, "Exercise the active HUD used by the shipped R66.");
        yield return null;
        Assert.That(hud.climbRateText.text, Is.EqualTo((-12.5).ToString("0.0") + "m/s >"));
        Assert.That(hud.altitudeText.text, Is.EqualTo("< " + (20 - 6.65).ToString("0.0") + "m"));
        Assert.That(hud.speedText.text, Is.EqualTo(18f.ToString("0.0") + " kmh"), "3/4 m/s body velocity components produce 18 km/h.");
        Assert.That(hud.powerLevelText.text, Is.EqualTo(75f.ToString("0.0") + "%"));
        Assert.That(hud.collectiveLevelText.text, Is.EqualTo("40 %"));
        Assert.That(hud.gLoadText.text, Is.EqualTo(4.5.ToString("0.00")));
        Assert.That(hud.climbRateText.color, Is.EqualTo(Color.red));
        Assert.That(hud.altitudeText.color, Is.EqualTo(Color.red));
        Assert.That(hud.gLoadText.color, Is.EqualTo(Color.red));
        core.δz = 2;
        core.z = 100;
        core.n = 1;
        yield return null;
        Assert.That(hud.climbRateText.text, Is.EqualTo(2d.ToString("0.0") + "m/s >"));
        Assert.That(hud.altitudeText.text, Is.EqualTo("< " + (100 - 6.65).ToString("0.0") + "m"));
        Assert.That(hud.climbRateText.color, Is.Not.EqualTo(Color.red));
        Assert.That(hud.altitudeText.color, Is.Not.EqualTo(Color.red));
        Assert.That(hud.gLoadText.color, Is.Not.EqualTo(Color.red));
        core.n = -1.5;
        yield return null;
        Assert.That(hud.gLoadText.color, Is.EqualTo(Color.red), "Both high and low G-load boundaries warn.");
    }

    [UnityTest]
    public IEnumerator DirectionalHud_ProjectsForwardVelocityAndHidesRearwardVelocity()
    {
        var camera = new GameObject("HUDCamera", typeof(Camera)).GetComponent<Camera>();
        camera.enabled = false;
        var body = new GameObject("HudFlightBody", typeof(Rigidbody)).GetComponent<Rigidbody>();
        body.useGravity = false;
        var root = new GameObject("DirectionalHUD");
        root.SetActive(false);
        var hud = root.AddComponent<HelicopterHUDDirectional>();
        var center = new GameObject("Center", typeof(RectTransform)).transform;
        var marker = new GameObject("Velocity", typeof(RectTransform)).transform;
        SetField(hud, "camera", camera);
        SetField(hud, "rb", body);
        SetField(hud, "hudCenter", center);
        SetField(hud, "velocityMarker", marker);
        root.SetActive(true);
        body.linearVelocity = Vector3.forward * 10f;
        yield return null;
        Assert.That(center.gameObject.activeSelf, Is.True);
        Assert.That(marker.gameObject.activeSelf, Is.True);
        Assert.That(marker.localPosition.x, Is.EqualTo(0).Within(0.1f));
        body.linearVelocity = Vector3.back * 10f;
        yield return null;
        Assert.That(marker.gameObject.activeSelf, Is.False);
    }

    private static DashboardManager CreateDashboard()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DashboardPath);
        Assert.That(prefab, Is.Not.Null);
        return Object.Instantiate(prefab).GetComponentInChildren<DashboardManager>(true);
    }

    private sealed class SyntheticMapboxToken : IDisposable
    {
        private readonly FieldInfo runtimeField = typeof(MapboxAccess).GetField(
            "_runtimeAccessToken", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly object previous;

        public SyntheticMapboxToken()
        {
            previous = runtimeField.GetValue(null);
            runtimeField.SetValue(null, "synthetic-dashboard-token");
        }

        public void Dispose()
        {
            runtimeField.SetValue(null, previous);
        }
    }

    private static void CreateWorldAndCameras()
    {
        var worldObject = new GameObject("WorldLookup");
        worldObject.SetActive(false);
        var world = worldObject.AddComponent<WorldLookup>();
        SetField(world, "lookupEntries", new List<WorldLookupEntry> { new WorldLookupEntry
            { lookupType = WorldLookupType.CesiumGeoreference, prefab = new GameObject("OfflineWorld") } });
        worldObject.SetActive(true);
        foreach (PanelCameraType type in Enum.GetValues(typeof(PanelCameraType)))
        {
            var go = new GameObject(type + "Camera");
            go.SetActive(false);
            Camera camera = go.AddComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 0;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.magenta;
            var source = go.AddComponent<DashboardCameraSource>();
            source.cameraType = type;
            go.SetActive(true);
        }
    }

    private static Color ReadPixel(RenderTexture texture)
    {
        RenderTexture previous = RenderTexture.active;
        var readback = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = texture;
            readback.ReadPixels(new Rect(texture.width / 2, texture.height / 2, 1, 1), 0, 0);
            readback.Apply();
            return readback.GetPixel(0, 0);
        }
        finally { RenderTexture.active = previous; Object.Destroy(readback); }
    }

    private static T Field<T>(object target, string name) => (T)target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void SetField(object target, string name, object value) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
}

public sealed class DashboardWorldAssetTests
{
    [Test]
    public void ShippedR66Hud_WiresFlightBodyCoreAndInstrumentReadouts()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Helicopters/R66_Default.prefab");
        Assert.That(prefab, Is.Not.Null);
        var hud = prefab.GetComponentInChildren<HelicopterHUD>(true);
        Assert.That(hud, Is.Not.Null);
        Assert.That(hud.m_vehicle, Is.Not.Null);
        // Controller.Initialize resolves the core from children; it is intentionally not preassigned.
        Assert.That(hud.m_vehicle.GetComponentInChildren<Oyedoyin.Common.SilantroCore>(true), Is.Not.Null);
        Assert.That(hud.gameObject.activeSelf, Is.True);
        Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(hud), Is.SameAs(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/StaticUI 1.prefab").GetComponent<HelicopterHUD>()));
        var fields = new SerializedObject(hud);
        foreach (string field in new[] { "camera", "rb", "velocityRoot", "hudCenter", "velocityMarker", "climbRateText",
            "climbRateSlider", "altitudeText", "altitudeSlider", "powerLevelText", "collectiveLevelText", "gLoadText", "speedText" })
        {
            Assert.That(fields.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
    }

    [Test]
    public void ShippedDashboard_RegistersEveryShippedPanelWithWiredCameraDisplays()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/UI/DashboardSystem.prefab");
        Assert.That(prefab, Is.Not.Null);
        var manager = prefab.GetComponentInChildren<DashboardManager>(true);
        var serialized = new SerializedObject(manager);
        SerializedProperty entries = serialized.FindProperty("availablePanels");
        Assert.That(entries.arraySize, Is.EqualTo(Enum.GetValues(typeof(DashboardPanelType)).Length));
        var seen = new HashSet<int>();
        for (int i = 0; i < entries.arraySize; i++)
        {
            SerializedProperty entry = entries.GetArrayElementAtIndex(i);
            Assert.That(seen.Add(entry.FindPropertyRelative("panelType").intValue), Is.True, "Duplicate panel registration.");
            var panel = entry.FindPropertyRelative("prefab").objectReferenceValue as DashboardPanel;
            Assert.That(panel, Is.Not.Null);
            Assert.That(panel.panelDropdown, Is.Not.Null, panel.name);
            if (panel is CameraDashboardPanel)
            {
                var cameraPanel = new SerializedObject(panel);
                Assert.That(cameraPanel.FindProperty("displayImage").objectReferenceValue, Is.Not.Null, panel.name);
                Assert.That(cameraPanel.FindProperty("fullscreenButton").objectReferenceValue, Is.Not.Null, panel.name);
            }
        }
    }

    [Test]
    public void DisplayRuntime_CapturesActualDisplayAndIsolatedCanvasScaleWithoutApplyingOptions()
    {
        var go = new GameObject("IsolatedMenuCanvas", typeof(Canvas), typeof(CanvasScaler));
        try
        {
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var runtime = new UnityDisplaySettings(scaler);
            DisplaySettingsValue state = runtime.Capture();
            Assert.That(state.Width, Is.EqualTo(Screen.width));
            Assert.That(state.Height, Is.EqualTo(Screen.height));
            Assert.That(state.WindowMode, Is.EqualTo(Screen.fullScreenMode));
            Assert.That(state.FrameRateCap, Is.EqualTo(Application.targetFrameRate));
            Assert.That(state.QualityLevel, Is.EqualTo(QualitySettings.GetQualityLevel()));
            scaler.referenceResolution = new Vector2(1280, 720);
            Assert.That(runtime.Capture().UiScale, Is.EqualTo(1.5f).Within(0.001f));
        }
        finally { Object.DestroyImmediate(go); }
    }
}
