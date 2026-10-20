using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CustomConnectionManager : MonoBehaviour
{
    public static CustomConnectionManager Instance { get; private set; }

    public GameObject playerPrefab;
    public GameObject spectatorPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] playerSpawnPoints;
    [SerializeField] private Transform[] spectatorSpawnPoints;
    private int nextPlayerSpawnIndex;
    private int nextSpectatorSpawnIndex;

    [SerializeField] private GameObject world;
    [SerializeField] private bool debugLogsEnabled = true;
    public ClientRole hostRole = ClientRole.Player;

    private readonly Dictionary<ulong, ClientRole> pendingClientRoles = new Dictionary<ulong, ClientRole>();
    private readonly Dictionary<ulong, ClientRole> connectedClientRoles = new Dictionary<ulong, ClientRole>();
    private NetworkManager subscribedManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        if (Application.isPlaying)
        {
            DontDestroyOnLoad(gameObject);
        }
        EnsureCallbacks();
    }

    private void OnEnable() => EnsureCallbacks();
    private void Start() => EnsureCallbacks();

    private void Update()
    {
        if (subscribedManager != NetworkManager.Singleton)
        {
            EnsureCallbacks();
        }
    }

    private void OnDisable() => DetachCallbacks();

    private void OnDestroy()
    {
        DetachCallbacks();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private bool EnsureCallbacks()
    {
        if (Instance != null && Instance != this)
        {
            return false;
        }
        var manager = NetworkManager.Singleton;
        if (subscribedManager == manager && manager != null)
        {
            return true;
        }
        DetachCallbacks();
        if (manager == null)
        {
            return false;
        }
        if (manager.ConnectionApprovalCallback != null && manager.ConnectionApprovalCallback != ApprovalCheck)
        {
            return false;
        }
        subscribedManager = manager;
        manager.NetworkConfig.ConnectionApproval = true;
        manager.ConnectionApprovalCallback = ApprovalCheck;
        manager.OnServerStarted += OnServerStarted;
        manager.OnClientConnectedCallback += OnClientConnected;
        manager.OnClientDisconnectCallback += OnClientDisconnected;
        manager.OnServerStopped += OnSessionStopped;
        manager.OnClientStopped += OnSessionStopped;
        return true;
    }

    private void DetachCallbacks()
    {
        if (subscribedManager != null)
        {
            subscribedManager.OnServerStarted -= OnServerStarted;
            subscribedManager.OnClientConnectedCallback -= OnClientConnected;
            subscribedManager.OnClientDisconnectCallback -= OnClientDisconnected;
            subscribedManager.OnServerStopped -= OnSessionStopped;
            subscribedManager.OnClientStopped -= OnSessionStopped;
            if (subscribedManager.ConnectionApprovalCallback == ApprovalCheck)
            {
                subscribedManager.ConnectionApprovalCallback = null;
            }
        }
        subscribedManager = null;
        ClearSessionRoles();
    }

    private void OnSessionStopped(bool wasHost) => ClearSessionRoles();

    private void ClearSessionRoles()
    {
        pendingClientRoles.Clear();
        connectedClientRoles.Clear();
        nextPlayerSpawnIndex = 0;
        nextSpectatorSpawnIndex = 0;
    }

    // Missing payloads are the explicit compatibility path for legacy Player clients.
    public static bool TryParseRole(byte[] payload, out ClientRole role)
    {
        role = ClientRole.Player;
        if (payload == null || payload.Length == 0)
        {
            return true;
        }
        if (payload.Length != 1 || payload[0] > (byte)ClientRole.Spectator)
        {
            return false;
        }
        role = (ClientRole)payload[0];
        return true;
    }

    public static bool TryGetSpawnedRole(NetworkObject playerObject, out ClientRole role)
    {
        role = ClientRole.Player;
        if (playerObject == null)
        {
            return false;
        }
        bool helicopter = playerObject.GetComponent<NetworkHelicopterRegistry>() != null;
        bool spectator = playerObject.GetComponent<ClientRoleMarker>() != null;
        if (helicopter == spectator)
        {
            return false;
        }
        role = spectator ? ClientRole.Spectator : ClientRole.Player;
        return true;
    }

    public bool TryValidateSpawnConfiguration(ClientRole role, out string reason)
    {
        reason = null;
        if (role != ClientRole.Player && role != ClientRole.Spectator)
        {
            reason = "Unsupported client role.";
            return false;
        }
        var prefab = role == ClientRole.Player ? playerPrefab : spectatorPrefab;
        if (prefab == null || !prefab.TryGetComponent<NetworkObject>(out var networkObject))
        {
            reason = $"The {role} prefab is missing or has no root NetworkObject.";
            return false;
        }
        if (!TryGetSpawnedRole(networkObject, out var prefabRole) || prefabRole != role)
        {
            reason = $"The {role} prefab does not have the matching helicopter or spectator role component.";
            return false;
        }
        if (!EnsureCallbacks())
        {
            reason = "NetworkManager or connection approval handler is unavailable.";
            return false;
        }
        var prefabs = subscribedManager.NetworkConfig.Prefabs;
        uint hash = networkObject.PrefabIdHash;
        if (hash == 0)
        {
            reason = $"The {role} prefab has no valid network prefab identity.";
            return false;
        }
        // Before startup NGO's processed list may still be empty. Inspect both
        // configured lists and runtime registrations without initializing NGO here.
        var entries = new HashSet<NetworkPrefab>(prefabs.Prefabs);
        foreach (var list in prefabs.NetworkPrefabsLists)
        {
            if (list == null) continue;
            foreach (var entry in list.PrefabList) entries.Add(entry);
        }
        bool matchingIdentity = false;
        foreach (var entry in entries)
        {
            if (entry == null) continue;
            // This spawner instantiates the configured prefab directly. Overrides
            // need their own explicit spawning contract and are not supported here.
            if (entry.Override == NetworkPrefabOverride.None && entry.Prefab == prefab &&
                entry.SourcePrefabGlobalObjectIdHash == hash)
            {
                matchingIdentity = true;
            }
            else if (entry.Prefab != null && entry.Prefab.TryGetComponent<NetworkObject>(out var other) && other.PrefabIdHash == hash)
            {
                reason = $"The {role} prefab network identity conflicts with another registration.";
                return false;
            }
        }
        if (!matchingIdentity)
        {
            reason = $"The {role} prefab is not registered with its matching network identity.";
            return false;
        }
        return true;
    }

    private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.Approved = false;
        response.CreatePlayerObject = false;
        response.Pending = false;
        pendingClientRoles.Remove(request.ClientNetworkId);
        if (!TryParseRole(request.Payload, out var role))
        {
            response.Reason = "Unsupported role payload. Send exactly one Player (0) or Spectator (1) byte.";
            return;
        }
        var manager = NetworkManager.Singleton;
        if (manager != null && manager.IsHost && request.ClientNetworkId == manager.LocalClientId)
        {
            role = hostRole;
        }
        if (!TryValidateSpawnConfiguration(role, out var reason))
        {
            response.Reason = reason;
            return;
        }
        pendingClientRoles[request.ClientNetworkId] = role;
        response.Approved = true;
    }

    private void OnServerStarted()
    {
        var manager = subscribedManager;
        if (manager != null && manager.IsHost)
        {
            connectedClientRoles[manager.LocalClientId] = hostRole;
            pendingClientRoles.Remove(manager.LocalClientId);
            EnsurePlayerObject(manager.LocalClientId, hostRole);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        var manager = subscribedManager;
        if (manager == null) return;
        if (manager.IsClient && clientId == manager.LocalClientId && world != null)
        {
            world.SetActive(true);
        }
        if (!manager.IsServer) return;
        ClientRole role;
        if (manager.IsHost && clientId == manager.LocalClientId)
        {
            role = hostRole;
        }
        else if (!pendingClientRoles.TryGetValue(clientId, out role) && !connectedClientRoles.TryGetValue(clientId, out role))
        {
            FailSpawn(clientId, "No approved role was recorded for this connection.");
            return;
        }
        pendingClientRoles.Remove(clientId);
        connectedClientRoles[clientId] = role;
        EnsurePlayerObject(clientId, role);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        pendingClientRoles.Remove(clientId);
        connectedClientRoles.Remove(clientId);
    }

    public bool IsSpectatorClient(ulong clientId)
    {
        var manager = subscribedManager != null ? subscribedManager : NetworkManager.Singleton;
        if (manager == null) return false;
        NetworkObject playerObject = null;
        if (manager.IsClient && clientId == manager.LocalClientId)
        {
            playerObject = manager.LocalClient?.PlayerObject;
        }
        else if (manager.IsServer && manager.ConnectedClients.TryGetValue(clientId, out var client))
        {
            playerObject = client.PlayerObject;
        }
        return TryGetSpawnedRole(playerObject, out var role) && role == ClientRole.Spectator;
    }

    private void EnsurePlayerObject(ulong clientId, ClientRole role)
    {
        var manager = subscribedManager;
        if (manager == null || !manager.IsServer || manager.ShutdownInProgress) return;
        if (manager.ConnectedClients.TryGetValue(clientId, out var client) && client.PlayerObject != null)
        {
            if (!TryGetSpawnedRole(client.PlayerObject, out var existingRole) || existingRole != role)
            {
                FailSpawn(clientId, "Existing player object does not match the approved role.");
            }
            return;
        }
        if (!TryValidateSpawnConfiguration(role, out var reason))
        {
            FailSpawn(clientId, reason);
            return;
        }
        GameObject instance = null;
        try
        {
            if (world != null) world.SetActive(true);
            var prefab = role == ClientRole.Spectator ? spectatorPrefab : playerPrefab;
            var spawnPoint = role == ClientRole.Spectator
                ? GetNextSpawnPoint(spectatorSpawnPoints, ref nextSpectatorSpawnIndex)
                : GetNextSpawnPoint(playerSpawnPoints, ref nextPlayerSpawnIndex);
            instance = Instantiate(prefab, spawnPoint != null ? spawnPoint.position : prefab.transform.position,
                spawnPoint != null ? spawnPoint.rotation : prefab.transform.rotation);
            var playerObject = instance.GetComponent<NetworkObject>();
            playerObject.SpawnAsPlayerObject(clientId, destroyWithScene: true);
            if (!playerObject.IsSpawned || playerObject.OwnerClientId != clientId ||
                !manager.ConnectedClients.TryGetValue(clientId, out var connectedClient) || connectedClient.PlayerObject != playerObject)
            {
                throw new InvalidOperationException("Netcode did not assign the spawned player object to this client.");
            }
        }
        catch (Exception exception)
        {
            if (instance != null)
            {
                var spawnedObject = instance.GetComponent<NetworkObject>();
                if (spawnedObject != null && spawnedObject.IsSpawned) spawnedObject.Despawn();
                else Destroy(instance);
            }
            FailSpawn(clientId, $"Could not spawn the approved {role} object: {exception.Message}");
        }
    }

    private void FailSpawn(ulong clientId, string reason)
    {
        OnClientDisconnected(clientId);
        Debug.LogWarning($"[CustomConnectionManager] Client {clientId}: {reason}");
        var manager = subscribedManager;
        if (manager == null || !manager.IsServer || manager.ShutdownInProgress) return;
        if (manager.IsHost && clientId == manager.LocalClientId) manager.Shutdown();
        else manager.DisconnectClient(clientId, reason);
    }

    private Transform GetNextSpawnPoint(Transform[] spawnPoints, ref int nextIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0) return null;
        for (int i = 0; i < spawnPoints.Length; i++)
        {
            int index = nextIndex % spawnPoints.Length;
            nextIndex = (index + 1) % spawnPoints.Length;
            if (spawnPoints[index] != null) return spawnPoints[index];
        }
        return null;
    }
}
