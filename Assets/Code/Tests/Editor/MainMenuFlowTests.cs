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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Exercises the real menu/scene coordinator using harmless role prefabs and an inactive world.
// This proves loopback/lifecycle and UI behaviour without running flight dynamics or NOVA.
public sealed class MainMenuFlowTests
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
        Assert.That(Object.FindObjectsByType<NovaController>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty);
        MenuSessionCoordinator.RequestReturnToMenu();
        yield return Until(MenuIsReady, "return from local host");
        Assert.That(first == null, Is.True, "Old persistent network manager must be destroyed.");
        Assert.That(CustomConnectionManager.Instance, Is.Null);
        MenuSessionCoordinator.Launch(new MenuSessionOptions { Mode = MenuLaunchMode.Host, Role = ClientRole.Spectator, Port = AvailablePort() });
        yield return Until(() => !MenuSessionCoordinator.IsTransitioning && NetworkManager.Singleton != null && NetworkManager.Singleton.IsConnectedClient, "spectator host startup");
        Assert.That(NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<ClientRoleMarker>(), Is.Not.Null);
        Assert.That(Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        MenuSessionCoordinator.RequestReturnToMenu();
        yield return Until(MenuIsReady, "return from spectator host");
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

    private static void RestorePreference(string key, string suffix)
    {
        if (SessionState.GetBool(StateKey + suffix + ".Exists", false)) PlayerPrefs.SetString(key, SessionState.GetString(StateKey + suffix, ""));
        else PlayerPrefs.DeleteKey(key);
        SessionState.EraseString(StateKey + suffix); SessionState.EraseBool(StateKey + suffix + ".Exists");
    }
}
