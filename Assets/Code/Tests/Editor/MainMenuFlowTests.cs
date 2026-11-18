using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using HeliSim.FrontEnd;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Exercises the real menu/scene coordinator using harmless role prefabs and an inactive world.
// This proves loopback/lifecycle and UI behaviour without running flight dynamics or NOVA.
public sealed partial class MainMenuFlowTests
{
    private const string StateKey = "HeliSim.MainMenuFlowTests";
    [Serializable] private class SavedSetup { public SavedScene[] scenes; }
    [Serializable] private class SavedScene { public string path; public bool isLoaded; public bool isActive; }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Assert.That(EditorApplication.isPlaying, Is.False);
        string setup = SessionState.GetString(MultiplayerVerificationRunner.AuthoringSetupKey, "");
        Assert.That(setup, Is.Not.Empty, "Run through MultiplayerVerificationRunner to preserve authoring scenes.");
        SessionState.SetString(StateKey + ".Setup", setup);
        SavePreference(MenuSessionOptionsStore.PreferenceKey, ".Options");
        SavePreference("ServerIP", ".ServerIP");
        CaptureSimulatorSettings();
        string folder = "Assets/__MainMenuFlow_" + Guid.NewGuid().ToString("N");
        SessionState.SetString(StateKey + ".Folder", folder);
        AssetDatabase.CreateFolder("Assets", folder.Substring(7));
        CreateSafePrefab(folder + "/Player.prefab", true);
        CreateSafePrefab(folder + "/Spectator.prefab", false);
        EditorSceneManager.OpenScene(MenuSessionCoordinator.MenuScenePath, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        SceneManager.sceneLoaded += ConfigureHarmlessFlightScene;
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlaying)
        {
            SceneManager.sceneLoaded -= ConfigureHarmlessFlightScene;
            if (SceneManager.GetActiveScene().path != MenuSessionCoordinator.MenuScenePath || MenuSessionCoordinator.IsTransitioning)
            {
                MenuSessionCoordinator.RequestReturnToMenu();
                float deadline = Time.realtimeSinceStartup + 20f;
                while ((MenuSessionCoordinator.IsTransitioning || NetworkManager.Singleton != null) && Time.realtimeSinceStartup < deadline) yield return null;
            }
            if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
            yield return new ExitPlayMode();
        }
        RestorePreference(MenuSessionOptionsStore.PreferenceKey, ".Options");
        RestorePreference("ServerIP", ".ServerIP");
        RestoreSimulatorSettings();
        string setup = SessionState.GetString(StateKey + ".Setup", "");
        if (!string.IsNullOrEmpty(setup))
        {
            var scenes = JsonUtility.FromJson<SavedSetup>(setup).scenes;
            EditorSceneManager.RestoreSceneManagerSetup(scenes.Select(s => new SceneSetup { path = s.path, isLoaded = s.isLoaded, isActive = s.isActive }).ToArray());
        }
        string folder = SessionState.GetString(StateKey + ".Folder", "");
        if (!string.IsNullOrEmpty(folder)) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True);
        SessionState.EraseString(StateKey + ".Setup");
        SessionState.EraseString(StateKey + ".Folder");
    }

    [UnityTest]
    public IEnumerator StartupAndSharedSettings_WorkBeforeAHelicopterExists()
    {
        var router = Object.FindFirstObjectByType<MainMenuPageRouter>();
        Assert.That(router, Is.Not.Null);
        Assert.That(router.ActivePageId, Is.EqualTo("fly"));
        Assert.That(NetworkManager.Singleton, Is.Null);
        Assert.That(Object.FindObjectsByType<NovaController>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        Assert.That(Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>(), Is.Not.Null);
        Directory.CreateDirectory("TestResults/main-menu-20261020");
        ScreenCapture.CaptureScreenshot("TestResults/main-menu-20261020/main-menu-fly.png");
        yield return null;
        foreach (string id in new[] { "display", "audio", "controls", "streaming", "hardware", "recordings", "help", "multiplayer" })
        {
            Assert.That(router.SelectPage(id), Is.True, id);
            yield return null;
            Assert.That(router.HasPendingOperation, Is.False, id);
            if (id == "display" || id == "audio" || id == "controls" || id == "streaming")
            {
                var scroll = (UnityEngine.UI.ScrollRect)new SerializedObject(router).FindProperty("pageScrollRect").objectReferenceValue;
                Assert.That(scroll, Is.Not.Null, id);
                // A page that fits the viewport reports a normalized position of 0 even at the top, so check
                // the content offset instead: 0 means the page's top edge is at the viewport's top edge.
                Assert.That(scroll.content.anchoredPosition.y, Is.EqualTo(0f).Within(0.5f), id + " must enter at the top.");
            }
        }
        ScreenCapture.CaptureScreenshot("TestResults/main-menu-20261020/main-menu-multiplayer.png");
        yield return null;
    }

    [UnityTest]
    public IEnumerator LocalHostReturnAndSpectatorHost_CanRunConsecutively()
    {
        ushort firstPort = AvailablePort();
        MenuSessionCoordinator.Launch(new MenuSessionOptions { Port = firstPort });
        yield return Until(() => !MenuSessionCoordinator.IsTransitioning && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient, "local host startup");
        var first = NetworkManager.Singleton;
        Assert.That(first.NetworkConfig.NetworkTransport, Is.TypeOf<UnityTransport>());
        Assert.That(((UnityTransport)first.NetworkConfig.NetworkTransport).ConnectionData.ServerListenAddress, Is.EqualTo("127.0.0.1"));
        Assert.That(first.LocalClient.PlayerObject.GetComponent<NetworkHelicopterRegistry>(), Is.Not.Null);
        AssertMenuRolePresentation(ClientRole.Player);
        Assert.That(Object.FindObjectsByType<NovaController>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        // Exercise the shipped dialog and its serialized return button on a live session.
        var shell = new GameObject("Pilot pause menu", typeof(RectTransform), typeof(Canvas));
        shell.SetActive(false);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/UI/Menu.prefab");
        var inFlight = Object.Instantiate(prefab, shell.transform);
        // Scene references for flight commands and persistent settings belong to the full aircraft.
        foreach (var simulationMenu in inFlight.GetComponentsInChildren<MainMenu>(true)) simulationMenu.enabled = false;
        foreach (var settings in inFlight.GetComponentsInChildren<PrefSettings>(true)) settings.enabled = false;
        if (UnityEngine.EventSystems.EventSystem.current == null) shell.AddComponent<UnityEngine.EventSystems.EventSystem>();
        shell.SetActive(true);
        yield return null;
        var pause = inFlight.GetComponentInChildren<PauseMenuController>(true);
        pause.Open();
        Assert.That(pause.IsOpen);
        var quitButton = inFlight.GetComponentsInChildren<UnityEngine.UI.Button>(true).Single(button =>
            Enumerable.Range(0, button.onClick.GetPersistentEventCount())
                .Any(i => button.onClick.GetPersistentMethodName(i) == "RequestReturnToMenu"));
        quitButton.onClick.Invoke();
        Assert.That(pause.IsConfirmingAction);
        pause.Close();
        Assert.That(pause.IsOpen && !pause.IsConfirmingAction && first.IsListening);
        quitButton.onClick.Invoke();
        pause.ConfirmPendingAction();
        yield return Until(MenuIsReady, "return from local host");
        Assert.That(first == null, Is.True, "Old persistent network manager must be destroyed.");
        Assert.That(CustomConnectionManager.Instance, Is.Null);
        MenuSessionCoordinator.Launch(new MenuSessionOptions { Mode = MenuLaunchMode.Host, Role = ClientRole.Spectator, Port = AvailablePort() });
        yield return Until(() => !MenuSessionCoordinator.IsTransitioning && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient, "spectator host startup");
        Assert.That(NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<ClientRoleMarker>(), Is.Not.Null);
        AssertMenuRolePresentation(ClientRole.Spectator);
        Assert.That(Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        MenuSessionCoordinator.RequestReturnToMenu();
        yield return Until(MenuIsReady, "return from spectator host");
    }

    [UnityTest]
    public IEnumerator MenuJoin_HidesLegacyRolePromptBeforeConnectionStarts()
    {
        bool observedSceneLoad = false;
        bool legacyPanelEnabled = true;
        bool roleUiActive = false;
        bool networkWasListening = true;
        int legacyOrder = 0;
        int transitionOrder = 0;
        MenuSessionCoordinator.Launch(new MenuSessionOptions { Mode = MenuLaunchMode.Join, Role = ClientRole.Spectator, Port = AvailablePort() });
        // Launch registers the coordinator first, so this observes its scene-load handoff
        // before Start callbacks or the connection coroutine can run.
        UnityAction<Scene, LoadSceneMode> observe = (scene, mode) =>
        {
            if (!scene.path.EndsWith("Multiplayer.unity")) return;
            var ui = Object.FindFirstObjectByType<RoleSelectionUI>(FindObjectsInactive.Include);
            var panel = ui.GetComponent<Canvas>();
            observedSceneLoad = true;
            legacyPanelEnabled = panel.enabled;
            roleUiActive = ui.isActiveAndEnabled;
            networkWasListening = NetworkManager.Singleton.IsListening;
            legacyOrder = panel.sortingOrder;
            transitionOrder = Object.FindFirstObjectByType<MenuTransitionView>().GetComponent<Canvas>().sortingOrder;
        };
        SceneManager.sceneLoaded += observe;
        try
        {
            yield return Until(() => observedSceneLoad, "menu-owned scene-load presentation");
            Assert.That(networkWasListening, Is.False, "Observe the initial handoff, before network startup.");
            Assert.That(roleUiActive, Is.True, "Startup scripts must remain active.");
            Assert.That(legacyPanelEnabled, Is.False, "Main-menu entry must suppress the legacy role prompt immediately.");
            Assert.That(transitionOrder, Is.GreaterThan(legacyOrder), "Connecting/cancel feedback must render above gameplay overlays.");
            yield return Until(() => Object.FindFirstObjectByType<RoleSelectionUI>().IsConnecting, "join connection attempt");
            AssertMenuRolePresentation(ClientRole.Spectator);
            var roleUi = Object.FindFirstObjectByType<RoleSelectionUI>();
            typeof(RoleSelectionUI).GetMethod("OnTransportFailure", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(roleUi, null);
            Assert.That(roleUi.IsConnecting, Is.False);
            Assert.That(roleUi.StatusMessage, Does.Contain("Transport failed"));
            AssertMenuRolePresentation(ClientRole.Spectator);
            MenuSessionCoordinator.RequestReturnToMenu();
            yield return Until(MenuIsReady, "return from pending join");
        }
        finally { SceneManager.sceneLoaded -= observe; }
    }

    [UnityTest]
    public IEnumerator CancelDuringJoin_ReturnsToAUsableMenu()
    {
        MenuSessionCoordinator.Launch(new MenuSessionOptions { Mode = MenuLaunchMode.Join, Port = AvailablePort() });
        yield return Until(() => SceneManager.GetActiveScene().path.EndsWith("Multiplayer.unity"), "join scene load");
        MenuSessionCoordinator.RequestReturnToMenu();
        yield return Until(MenuIsReady, "cancelled join return");
        Assert.That(NetworkManager.Singleton, Is.Null);
        Assert.That(Object.FindFirstObjectByType<MainMenuPageRouter>().SelectPage("multiplayer"), Is.True);
    }

    private static void AssertMenuRolePresentation(ClientRole role)
    {
        var ui = Object.FindFirstObjectByType<RoleSelectionUI>(FindObjectsInactive.Include);
        Assert.That(ui, Is.Not.Null);
        Assert.That(ui.isActiveAndEnabled, Is.True);
        Assert.That(ui.GetComponent<Canvas>().enabled, Is.False);
        Assert.That(RoleSelectionUI.SelectedRole, Is.EqualTo(role));
        Assert.That(CustomConnectionManager.Instance.hostRole, Is.EqualTo(role));
        Assert.That(NetworkManager.Singleton.NetworkConfig.ConnectionData, Is.EqualTo(new byte[] { (byte)role }));
    }

    private static bool MenuIsReady() => !MenuSessionCoordinator.IsTransitioning &&
        SceneManager.GetActiveScene().path == MenuSessionCoordinator.MenuScenePath &&
        Object.FindFirstObjectByType<MainMenuPageRouter>() != null && NetworkManager.Singleton == null;

    private static IEnumerator Until(Func<bool> predicate, string operation)
    {
        float deadline = Time.realtimeSinceStartup + 25f;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, operation);
    }

    private static void ConfigureHarmlessFlightScene(Scene scene, LoadSceneMode mode)
    {
        if (scene.path != "Assets/Level/Scenes/Multiplayer.unity") return;
        var manager = NetworkManager.Singleton;
        var connection = CustomConnectionManager.Instance;
        Assert.That(manager, Is.Not.Null);
        Assert.That(connection, Is.Not.Null);
        // Keep only the real connection/startup path; no terrain, flight or hardware simulation.
        foreach (var root in scene.GetRootGameObjects())
            if (root.name != "JoinUI" && root.GetComponent<NetworkManager>() == null && root.GetComponent<CustomConnectionManager>() == null)
                root.SetActive(false);
        typeof(CustomConnectionManager).GetField("world", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(connection, null);
        string folder = SessionState.GetString(StateKey + ".Folder", "");
        var transport = manager.GetComponent<UnityTransport>();
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport, ConnectionApproval = true, EnableSceneManagement = false };
        connection.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Player.prefab");
        connection.spectatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Spectator.prefab");
        manager.AddNetworkPrefab(connection.playerPrefab);
        manager.AddNetworkPrefab(connection.spectatorPrefab);
    }

    private static void CreateSafePrefab(string path, bool player)
    {
        var root = new GameObject(player ? "HarmlessPlayer" : "HarmlessSpectator");
        try
        {
            root.AddComponent<NetworkObject>();
            if (player) root.AddComponent<NetworkHelicopterRegistry>(); else root.AddComponent<ClientRoleMarker>();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static ushort AvailablePort()
    {
        using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) return (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
    }

    private static void SavePreference(string key, string suffix)
    {
        SessionState.SetBool(StateKey + suffix + ".Exists", PlayerPrefs.HasKey(key));
        SessionState.SetString(StateKey + suffix, PlayerPrefs.GetString(key, ""));
    }

    // The shipped menu's PrefSettings.Start applies the profile and writes both stores.
    private static readonly string[] SimulatorPreferences =
    {
        "screenSpaceError", "preloadAncestors", "preloadSiblings", "forbidHoles",
        "MaximumSimultaneousTileLoads", "MaximumCachedBytes", "LoadingDescendantLimit",
        "enableFrustumCulling", "enableFogCulling", "enableForceScreenSpaceError", "culledScreenSpaceError",
        "controlScheme", "enableNovaPopup", "volume"
    };

    private static void CaptureSimulatorSettings()
    {
        foreach (string key in SimulatorPreferences)
        {
            SessionState.SetBool(StateKey + key + ".HadInt", PlayerPrefs.HasKey(key));
            SessionState.SetInt(StateKey + key, PlayerPrefs.GetInt(key));
        }
        string path = Path.Combine(Application.persistentDataPath, "Settings", "settings.json");
        SessionState.SetString(StateKey + ".SettingsBytes", File.Exists(path) ? Convert.ToBase64String(File.ReadAllBytes(path)) : "-");
        SessionState.SetFloat(StateKey + ".Volume", AudioListener.volume);
        SessionState.SetBool(StateKey + ".Captured", true);
    }

    private static void RestoreSimulatorSettings()
    {
        string bytes = SessionState.GetString(StateKey + ".SettingsBytes", "");
        if (!SessionState.GetBool(StateKey + ".Captured", false)) return;
        foreach (string key in SimulatorPreferences)
        {
            if (SessionState.GetBool(StateKey + key + ".HadInt", false)) PlayerPrefs.SetInt(key, SessionState.GetInt(StateKey + key, 0));
            else PlayerPrefs.DeleteKey(key);
            SessionState.EraseBool(StateKey + key + ".HadInt");
            SessionState.EraseInt(StateKey + key);
        }
        PlayerPrefs.Save();
        string path = Path.Combine(Application.persistentDataPath, "Settings", "settings.json");
        if (bytes == "-") { if (File.Exists(path)) File.Delete(path); }
        else File.WriteAllBytes(path, Convert.FromBase64String(bytes));
        AudioListener.volume = SessionState.GetFloat(StateKey + ".Volume", 1);
        SessionState.EraseString(StateKey + ".SettingsBytes");
        SessionState.EraseFloat(StateKey + ".Volume");
        SessionState.EraseBool(StateKey + ".Captured");
    }

    private static void RestorePreference(string key, string suffix)
    {
        if (SessionState.GetBool(StateKey + suffix + ".Exists", false)) PlayerPrefs.SetString(key, SessionState.GetString(StateKey + suffix, ""));
        else PlayerPrefs.DeleteKey(key);
        SessionState.EraseString(StateKey + suffix); SessionState.EraseBool(StateKey + suffix + ".Exists");
    }
}
