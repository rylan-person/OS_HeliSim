using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// Real loopback socket proof using harmless role prefabs, not the flight/NOVA prefab.
/// Run from EditMode: the fixture owns entering Play mode and restoring saved scenes.
/// Same-process peers prove transport/lifecycle behavior, not separate-machine connectivity.
/// </summary>
public sealed class MultiplayerIntegrationTests
{
    private const string StateKey = "HeliSim.MultiplayerIntegrationTests.SceneSetup";
    private const string FolderKey = "HeliSim.MultiplayerIntegrationTests.Folder";
    private const string Prefix = "MultiplayerIntegration-";

    [Serializable]
    private sealed class SavedSetup
    {
        public SavedScene[] scenes;
    }

    [Serializable]
    private sealed class SavedScene
    {
        public string path;
        public bool isLoaded;
        public bool isActive;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Assert.That(EditorApplication.isPlaying, Is.False, "Select this fixture from the EditMode runner.");
        Assert.That(SessionState.GetString(FolderKey, ""), Is.Empty,
            "An earlier fixture needs cleanup before another run.");
        string authoringSetup = SessionState.GetString(MultiplayerVerificationRunner.AuthoringSetupKey, "");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            Assert.That(scene.isDirty, Is.False, "Save authoring scenes before running integration tests.");
            // Unity's EditMode runner opens a clean empty scene before invoking fixtures.
            if (string.IsNullOrEmpty(scene.path))
                Assert.That(authoringSetup, Is.Not.Empty, "Run through MultiplayerVerificationRunner so authoring scenes are checked before the Test Runner replaces them.");
        }
        SessionState.SetString(StateKey, !string.IsNullOrEmpty(authoringSetup) ? authoringSetup : JsonUtility.ToJson(new SavedSetup
        {
            scenes = EditorSceneManager.GetSceneManagerSetup().Where(scene => !string.IsNullOrEmpty(scene.path)).Select(scene => new SavedScene
            {
                path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive
            }).ToArray()
        }));
        string folder = "Assets/__MultiplayerIntegration_" + Guid.NewGuid().ToString("N");
        SessionState.SetString(FolderKey, folder);
        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        var isolatedScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Assert.That(EditorSceneManager.SaveScene(isolatedScene, folder + "/Isolated.unity"), Is.True);
        CreatePrefab(folder + "/Player.prefab", true);
        CreatePrefab(folder + "/Spectator.prefab", false);
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlaying)
        {
            var managers = Object.FindObjectsByType<NetworkManager>(FindObjectsSortMode.None)
                .Where(manager => manager.name.StartsWith(Prefix, StringComparison.Ordinal)).ToArray();
            foreach (var manager in managers) manager.Shutdown();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (managers.Any(manager => manager != null && (manager.IsListening || manager.ShutdownInProgress)) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            bool cleanShutdown = managers.All(manager => manager == null || (!manager.IsListening && !manager.ShutdownInProgress));
            foreach (var manager in managers)
            {
                if (manager != null) Object.Destroy(manager.gameObject);
            }
            GameObjectTarget.Clear();
            yield return null;
            // Report cleanup failures after leaving Play mode so scenes can still be restored below.
            SessionState.SetBool(StateKey + ".ShutdownFailed", !cleanShutdown);
            yield return new ExitPlayMode();
        }
        string setupJson = SessionState.GetString(StateKey, "");
        string folder = SessionState.GetString(FolderKey, "");
        if (!string.IsNullOrEmpty(setupJson))
        {
            var savedScenes = JsonUtility.FromJson<SavedSetup>(setupJson).scenes;
            if (savedScenes.Length == 0) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            else EditorSceneManager.RestoreSceneManagerSetup(savedScenes
                .Select(scene => new SceneSetup
                {
                    path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive
                }).ToArray());
        }
        if (!string.IsNullOrEmpty(folder)) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "Temporary fixture assets cleanup failed.");
        SessionState.EraseString(StateKey);
        SessionState.EraseString(FolderKey);
        bool shutdownFailed = SessionState.GetBool(StateKey + ".ShutdownFailed", false);
        SessionState.EraseBool(StateKey + ".ShutdownFailed");
        Assert.That(shutdownFailed, Is.False, "Owned network managers did not finish shutdown before cleanup.");
    }

    [UnityTest]
    public IEnumerator JoinUIPrefabReportsInvalidAddressAndCanHostAgainAfterShutdown()
    {
        ushort port = AvailablePort();
        NetworkManager host = CreateManager("UIHost", port);
        host.SetSingleton();
        ConfigureHost(host, ClientRole.Spectator);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/JoinUI.prefab");
        Assert.That(prefab, Is.Not.Null);
        GameObject instance = Object.Instantiate(prefab);
        instance.name = Prefix + "JoinUI";
        yield return null; // Let the prefab's normal Start callbacks initialise its saved-address field.
        RoleSelectionUI ui = instance.GetComponentInChildren<RoleSelectionUI>(true);
        Assert.That(ui, Is.Not.Null);
        TMP_InputField address = UIField<TMP_InputField>(ui, "ipInputField");
        Canvas panel = UIField<Canvas>(ui, "roleSelectionPanel");
        TMP_Text status = UIField<TMP_Text>(ui, "connectionStatusText");
        Selectable[] controls = UIField<Selectable[]>(ui, "connectionControls");
        Assert.That(address, Is.Not.Null);
        Assert.That(panel, Is.Not.Null);
        Assert.That(status, Is.Not.Null);
        Assert.That(controls, Is.Not.Empty);
        Assert.That(controls.All(control => control != null), Is.True);
        typeof(RoleSelectionUI).GetField("serverPort", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(ui, port);
        Button hostButton = WiredButton(instance, ui, "StartAsHost");
        Button clientButton = WiredButton(instance, ui, "StartAsClient");

        address.text = "invalid address";
        clientButton.onClick.Invoke();
        Assert.That(host.IsListening, Is.False);
        Assert.That(panel.enabled, Is.True);
        Assert.That(status.text, Does.Contain("valid IPv4"));
        Assert.That(address.text, Is.EqualTo("invalid address"));
        Assert.That(controls.All(control => control.interactable), Is.True);

        ui.OnClickSpectator();
        int serverStarts = 0;
        host.OnServerStarted += () => serverStarts++;
        hostButton.onClick.Invoke();
        yield return Until(() => host.IsHost && host.LocalClient?.PlayerObject != null, "JoinUI host startup");
        AssertRole(host, host.LocalClientId, ClientRole.Spectator);
        Assert.That(panel.enabled, Is.False);
        Assert.That(status.text, Is.EqualTo("Connected."));
        Assert.That(controls.All(control => !control.interactable), Is.True);
        NetworkObject originalPlayer = host.LocalClient.PlayerObject;
        hostButton.onClick.Invoke(); // Invoke explicitly even though the visible button is disabled.
        ui.OnClickPlayer();
        yield return null;
        Assert.That(serverStarts, Is.EqualTo(1));
        Assert.That(host.LocalClient.PlayerObject, Is.EqualTo(originalPlayer));
        Assert.That(RoleSelectionUI.SelectedRole, Is.EqualTo(ClientRole.Spectator));
        Assert.That(host.ConnectedClients.Count, Is.EqualTo(1));

        host.Shutdown();
        yield return Until(() => !host.IsListening && !host.ShutdownInProgress && panel.enabled,
            "JoinUI shutdown and control restoration");
        Assert.That(controls.All(control => control.interactable), Is.True);
        Assert.That(status.text, Does.Contain("retry").Or.Contain("again"));
        hostButton.onClick.Invoke();
        yield return Until(() => host.IsHost && host.LocalClient?.PlayerObject != null, "JoinUI host retry");
        Assert.That(serverStarts, Is.EqualTo(2));
        AssertRole(host, host.LocalClientId, ClientRole.Spectator);
        Assert.That(panel.enabled, Is.False);
    }

    [UnityTest]
    public IEnumerator HostPlayerApprovesRolesRejectsMalformedRequestsAndRestarts()
    {
        ushort port = AvailablePort();
        NetworkManager host = CreateManager("Host", port);
        host.SetSingleton();
        CustomConnectionManager connection = ConfigureHost(host, ClientRole.Player);
        Assert.That(host.StartHost(), Is.True);
        yield return Until(() => host.LocalClient?.PlayerObject != null, "host player spawn");
        AssertRole(host, host.LocalClientId, ClientRole.Player);

        NetworkManager spectator = CreateManager("Spectator", port, new byte[] { 1 });
        Assert.That(spectator.StartClient(), Is.True);
        yield return Connected(host, spectator, ClientRole.Spectator);
        Assert.That(connection.IsSpectatorClient(spectator.LocalClientId), Is.True);

        NetworkManager legacyPlayer = CreateManager("LegacyPlayer", port, Array.Empty<byte>());
        Assert.That(legacyPlayer.StartClient(), Is.True);
        yield return Connected(host, legacyPlayer, ClientRole.Player);
        yield return Until(() => RegistryCount(spectator) == 2, "spectator late player replication");
        AssertRegistry(host, 2);
        AssertRegistry(spectator, 2);
        AssertRegistry(legacyPlayer, 2);
        AssertRoleCache(connection, 3, 0);

        foreach (byte[] malformed in new[] { new byte[] { 2 }, new byte[] { 0, 1 } })
        {
            NetworkManager rejected = CreateManager("Rejected", port, malformed);
            bool disconnected = false;
            rejected.OnClientDisconnectCallback += _ => disconnected = true;
            Assert.That(rejected.StartClient(), Is.True);
            yield return Until(() => disconnected, "malformed request rejection");
            Assert.That(rejected.DisconnectReason, Does.Contain("Unsupported role payload"));
            Assert.That(host.ConnectedClients.Count, Is.EqualTo(3));
            AssertRoleCache(connection, 3, 0);
        }

        ulong oldId = legacyPlayer.LocalClientId;
        legacyPlayer.Shutdown();
        yield return Until(() => !host.ConnectedClients.ContainsKey(oldId) && !legacyPlayer.ShutdownInProgress, "player disconnect");
        AssertRoleCache(connection, 2, 0);
        Assert.That(legacyPlayer.StartClient(), Is.True);
        yield return Connected(host, legacyPlayer, ClientRole.Player);
        AssertRoleCache(connection, 3, 0);

        host.Shutdown();
        yield return Until(() => !host.IsListening && !host.ShutdownInProgress &&
            !spectator.IsListening && !legacyPlayer.IsListening, "session shutdown");
        AssertRoleCache(connection, 0, 0);
        AssertRegistry(host, 0);
        Assert.That(host.StartHost(), Is.True);
        yield return Until(() => host.LocalClient?.PlayerObject != null, "host restart");
        AssertRole(host, host.LocalClientId, ClientRole.Player);
        AssertRoleCache(connection, 1, 0);
        AssertRegistry(host, 1);
    }

    [UnityTest]
    public IEnumerator HostSpectatorSelectsLatePlayersAndSurvivesWatchedAndLastPlayerDisconnect()
    {
        ushort port = AvailablePort();
        NetworkManager host = CreateManager("Host", port);
        host.SetSingleton();
        CustomConnectionManager connection = ConfigureHost(host, ClientRole.Spectator);
        GameObject world = new GameObject(Prefix + "World");
        GameObject anchor = new GameObject(Prefix + "CameraTarget");
        anchor.transform.SetParent(world.transform);
        var follower = anchor.AddComponent<CameraTargetFollower>();
        GameObjectTarget.Register("CameraTarget", anchor);
        var switcher = world.AddComponent<CameraTargetPovSwitcher>();
        Assert.That(host.StartHost(), Is.True);
        yield return Until(() => host.LocalClient?.PlayerObject != null, "host spectator spawn");
        AssertRole(host, host.LocalClientId, ClientRole.Spectator);
        Assert.That(connection.IsSpectatorClient(host.LocalClientId), Is.True);
        switcher.SwitchToNextPlayer();
        Assert.That(follower.SelectedTarget, Is.Null);
        AssertRegistry(host, 0);

        NetworkManager first = CreateManager("FirstPlayer", port, new byte[] { 0 });
        Assert.That(first.StartClient(), Is.True);
        yield return Connected(host, first, ClientRole.Player);
        Transform firstTarget = host.ConnectedClients[first.LocalClientId].PlayerObject.transform;
        yield return Until(() => follower.SelectedTarget == firstTarget, "late first-player selection");

        NetworkManager second = CreateManager("SecondPlayer", port, new byte[] { 0 });
        Assert.That(second.StartClient(), Is.True);
        yield return Connected(host, second, ClientRole.Player);
        Transform secondTarget = host.ConnectedClients[second.LocalClientId].PlayerObject.transform;
        yield return Until(() => RegistryCount(first) == 2, "existing player's late join replication");
        Assert.That(follower.SelectedTarget, Is.EqualTo(firstTarget));
        AssertRegistry(host, 2);
        AssertRegistry(first, 2);
        AssertRegistry(second, 2);
        switcher.SwitchToNextPlayer();
        Assert.That(follower.SelectedTarget, Is.EqualTo(secondTarget));
        Assert.That(secondTarget.GetComponent<NetworkObject>().OwnerClientId, Is.EqualTo(second.LocalClientId));
        AssertAnchor(anchor, world);

        switcher.enabled = false;
        Assert.That(follower.SelectedTarget, Is.Null);
        Assert.That(ClientTelemetryCollector.ActiveCollector, Is.Null);
        Assert.That(PlayerTimeTrialState.ActiveState, Is.Null);
        switcher.enabled = true;
        // No join/despawn or Tab input occurs: enabling must invalidate the cached selection.
        yield return Until(() => follower.SelectedTarget == firstTarget, "automatic selection after switcher re-enable");
        AssertAnchor(anchor, world);
        switcher.SwitchToNextPlayer();
        Assert.That(follower.SelectedTarget, Is.EqualTo(secondTarget));

        ulong secondId = second.LocalClientId;
        second.Shutdown();
        yield return Until(() => !host.ConnectedClients.ContainsKey(secondId) && follower.SelectedTarget == firstTarget,
            "watched-player replacement");
        AssertAnchor(anchor, world);
        ulong firstId = first.LocalClientId;
        first.Shutdown();
        yield return Until(() => !host.ConnectedClients.ContainsKey(firstId) && follower.SelectedTarget == null &&
            !first.ShutdownInProgress, "last-player removal");
        AssertRegistry(host, 0);
        Assert.That(ClientTelemetryCollector.ActiveCollector, Is.Null);
        Assert.That(PlayerTimeTrialState.ActiveState, Is.Null);
        switcher.SwitchToNextPlayer();
        AssertAnchor(anchor, world);

        Assert.That(first.StartClient(), Is.True);
        yield return Connected(host, first, ClientRole.Player);
        yield return Until(() => follower.SelectedTarget == host.ConnectedClients[first.LocalClientId].PlayerObject.transform,
            "selection after rejoin");
        AssertRoleCache(connection, 2, 0);
        AssertAnchor(anchor, world);
    }

    private static IEnumerator Connected(NetworkManager host, NetworkManager client, ClientRole role)
    {
        yield return Until(() => client.IsConnectedClient && client.LocalClient?.PlayerObject != null &&
            host.ConnectedClients.TryGetValue(client.LocalClientId, out var peer) && peer.PlayerObject != null,
            role + " connection and replicated player object");
        AssertRole(host, client.LocalClientId, role);
        Assert.That(CustomConnectionManager.TryGetSpawnedRole(client.LocalClient.PlayerObject, out var localRole), Is.True);
        Assert.That(localRole, Is.EqualTo(role));
        Assert.That(client.SpawnManager.SpawnedObjectsList.Count(obj => obj.IsPlayerObject && obj.OwnerClientId == client.LocalClientId), Is.EqualTo(1));
        yield return Until(() => RegistryCount(client) == RegistryCount(host), "replicated helicopter registry");
    }

    private static void AssertRole(NetworkManager host, ulong clientId, ClientRole expected)
    {
        NetworkObject player = host.ConnectedClients[clientId].PlayerObject;
        Assert.That(CustomConnectionManager.TryGetSpawnedRole(player, out var role), Is.True);
        Assert.That(role, Is.EqualTo(expected));
        Assert.That(player.OwnerClientId, Is.EqualTo(clientId));
        Assert.That(host.SpawnManager.SpawnedObjectsList.Count(obj => obj.IsPlayerObject && obj.OwnerClientId == clientId), Is.EqualTo(1));
    }

    private static void AssertRegistry(NetworkManager manager, int expected)
    {
        var result = new List<NetworkHelicopterRegistry>();
        NetworkHelicopterRegistry.GetHelicopters(manager, result);
        Assert.That(result.Count, Is.EqualTo(expected), manager.name);
        Assert.That(result.All(item => item.NetworkManager == manager && item.NetworkObject.IsPlayerObject && item.IsSpawned), Is.True);
        Assert.That(result.Select(item => item.OwnerClientId), Is.Ordered.Ascending);
    }

    private static int RegistryCount(NetworkManager manager)
    {
        var result = new List<NetworkHelicopterRegistry>();
        NetworkHelicopterRegistry.GetHelicopters(manager, result);
        return result.Count;
    }

    private static T UIField<T>(RoleSelectionUI ui, string field)
    {
        FieldInfo info = typeof(RoleSelectionUI).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(info, Is.Not.Null, field);
        return (T)info.GetValue(ui);
    }

    private static Button WiredButton(GameObject root, RoleSelectionUI ui, string method)
    {
        Button[] matches = root.GetComponentsInChildren<Button>(true).Where(button =>
            Enumerable.Range(0, button.onClick.GetPersistentEventCount()).Any(index =>
                button.onClick.GetPersistentTarget(index) == ui &&
                button.onClick.GetPersistentMethodName(index) == method &&
                button.onClick.GetPersistentListenerState(index) != UnityEngine.Events.UnityEventCallState.Off)).ToArray();
        Assert.That(matches.Length, Is.EqualTo(1), "Exactly one prefab button must be wired to " + method);
        return matches[0];
    }

    private static void AssertRoleCache(CustomConnectionManager connection, int connected, int pending)
    {
        Assert.That(((IDictionary)typeof(CustomConnectionManager).GetField("connectedClientRoles", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(connection)).Count, Is.EqualTo(connected));
        Assert.That(((IDictionary)typeof(CustomConnectionManager).GetField("pendingClientRoles", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(connection)).Count, Is.EqualTo(pending));
    }

    private static void AssertAnchor(GameObject anchor, GameObject world)
    {
        Assert.That(anchor, Is.Not.Null);
        Assert.That(anchor.transform.parent, Is.EqualTo(world.transform));
        Assert.That(GameObjectTarget.TryGet("CameraTarget", out var registered), Is.True);
        Assert.That(registered, Is.EqualTo(anchor));
    }

    private static IEnumerator Until(Func<bool> condition, string operation)
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Timed out waiting for " + operation);
    }

    private static NetworkManager CreateManager(string suffix, ushort port, byte[] payload = null)
    {
        var gameObject = new GameObject(Prefix + suffix);
        var transport = gameObject.AddComponent<UnityTransport>();
        transport.SetConnectionData("127.0.0.1", port, "127.0.0.1");
        var manager = gameObject.AddComponent<NetworkManager>();
        manager.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = false,
            ConnectionApproval = true,
            ConnectionData = payload ?? new byte[] { 0 }
        };
        string folder = SessionState.GetString(FolderKey, "");
        manager.AddNetworkPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Player.prefab"));
        manager.AddNetworkPrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Spectator.prefab"));
        return manager;
    }

    private static CustomConnectionManager ConfigureHost(NetworkManager host, ClientRole role)
    {
        var connection = new GameObject(Prefix + "Connection").AddComponent<CustomConnectionManager>();
        string folder = SessionState.GetString(FolderKey, "");
        connection.playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Player.prefab");
        connection.spectatorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "/Spectator.prefab");
        connection.hostRole = role;
        Assert.That(connection.TryValidateSpawnConfiguration(role, out var reason), Is.True, reason);
        return connection;
    }

    private static void CreatePrefab(string path, bool helicopter)
    {
        var gameObject = new GameObject(helicopter ? "TestHelicopter" : "TestSpectator");
        try
        {
            gameObject.AddComponent<NetworkObject>();
            if (helicopter) gameObject.AddComponent<NetworkHelicopterRegistry>();
            else gameObject.AddComponent<ClientRoleMarker>();
            var prefab = PrefabUtility.SaveAsPrefabAsset(gameObject, path);
            Assert.That(prefab.GetComponent<NetworkObject>().PrefabIdHash, Is.Not.Zero);
        }
        finally
        {
            Object.DestroyImmediate(gameObject);
        }
    }

    private static ushort AvailablePort()
    {
        using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
        {
            return (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
        }
    }
}
