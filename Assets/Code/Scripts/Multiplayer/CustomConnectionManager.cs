using Unity.Netcode;
using UnityEngine;
using System.Collections.Generic;

public class CustomConnectionManager : MonoBehaviour
{
    public static CustomConnectionManager Instance { get; private set; }

    public GameObject playerPrefab;
    public GameObject spectatorPrefab;

    [Header("Spawn Points")]
    [SerializeField] private Transform[] playerSpawnPoints;
    [SerializeField] private Transform[] spectatorSpawnPoints;
    private int nextPlayerSpawnIndex = 0;
    private int nextSpectatorSpawnIndex = 0;

    [SerializeField] GameObject world;
    [SerializeField] private bool debugLogsEnabled = true;

    public ClientRole hostRole = ClientRole.Player; // set in inspector/UI before StartHost()
    private readonly Dictionary<ulong, ClientRole> pendingClientRoles = new Dictionary<ulong, ClientRole>();
    private readonly Dictionary<ulong, ClientRole> connectedClientRoles = new Dictionary<ulong, ClientRole>();

    private void LogDebug(string message)
    {
        if (!debugLogsEnabled) return;
        Debug.Log($"[CustomConnectionManager] {message}");
    }

    void Awake()
    {
        LogDebug("Awake called.");
        if (Instance != null && Instance != this)
        {
            LogDebug("Duplicate instance detected. Destroying this GameObject.");
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        LogDebug("Singleton instance initialized and marked DontDestroyOnLoad.");
    }   

    private void Start()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            LogDebug("NetworkManager.Singleton is null in Start. Skipping setup.");
            return;
        }

        nm.NetworkConfig.ConnectionApproval = true;
        nm.ConnectionApprovalCallback = ApprovalCheck;

        nm.OnServerStarted += OnServerStarted;
        nm.OnClientConnectedCallback += OnClientConnected;
        nm.OnClientDisconnectCallback += OnClientDisconnected;
        LogDebug("Connection approval enabled, callback assigned, and OnServerStarted subscribed.");
    }

    private void OnDestroy()
    {
        LogDebug("OnDestroy called.");
        if (Instance == this)
        {
            Instance = null;
            LogDebug("Singleton instance cleared.");
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            LogDebug("Unsubscribed from OnServerStarted.");
        }
        else
        {
            LogDebug("NetworkManager.Singleton was null during OnDestroy; no event unsubscription needed.");
        }
    }

    private void OnServerStarted()
    {
        LogDebug("Server started. Checking host client role and player object state.");
        // Only relevant for Host (server + local client)
        if (!NetworkManager.Singleton.IsHost)
        {
            LogDebug("Not running as host. Skipping host local spawn path.");
            return;
        }

        ulong hostClientId = NetworkManager.Singleton.LocalClientId;
        LogDebug($"Host local client id: {hostClientId}. Host role: {hostRole}.");

        // If NGO already created a player object somehow, don't double-spawn
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(hostClientId, out var client)
            && client.PlayerObject != null)
        {
            LogDebug("Host already has a player object. Skipping duplicate spawn.");
            return;
        }

        LogDebug("No existing host player object found. Spawning now.");
        SpawnForClient(hostClientId, hostRole);
    }

    private void OnClientConnected(ulong clientId)
    {
        LogDebug($"Client connected callback fired for client {clientId}. IsServer={NetworkManager.Singleton.IsServer}, IsClient={NetworkManager.Singleton.IsClient}, LocalClientId={NetworkManager.Singleton.LocalClientId}.");
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        LogDebug($"OnClientConnected callback fired for client {clientId}. IsServer={nm.IsServer}, IsClient={nm.IsClient}, LocalClientId={nm.LocalClientId}.");

        if (nm.IsClient && clientId == nm.LocalClientId)
        {
            if (world != null)
            {
                world.SetActive(true);
                LogDebug("Local client connected. World object set active on this client.");
            }
            else
            {
                LogDebug("Local client connected, but world reference is null.");
            }
        }

        if (!nm.IsServer)
            return;

        if (nm.ConnectedClients.TryGetValue(clientId, out var connectedClient)
            && connectedClient.PlayerObject != null)
        {
            LogDebug($"Client {clientId} already has a player object. Skipping server spawn.");
            pendingClientRoles.Remove(clientId);
            return;
        }

        ClientRole role = ClientRole.Player;
        if (nm.IsHost && clientId == nm.LocalClientId)
        {
            role = hostRole;
            LogDebug($"Using host role for local host client {clientId}: {role}.");
        }
        else if (pendingClientRoles.TryGetValue(clientId, out var pendingRole))
        {
            role = pendingRole;
            LogDebug($"Using pending role for client {clientId}: {role}.");
        }
        else
        {
            LogDebug($"No pending role found for client {clientId}. Defaulting to Player.");
        }

        pendingClientRoles.Remove(clientId);
        connectedClientRoles[clientId] = role;
        SpawnForClient(clientId, role);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        pendingClientRoles.Remove(clientId);
        connectedClientRoles.Remove(clientId);
        LogDebug($"Client disconnected: {clientId}. Removed cached role data.");
    }

    public bool IsSpectatorClient(ulong clientId)
    {
        if (connectedClientRoles.TryGetValue(clientId, out var role))
        {
            return role == ClientRole.Spectator;
        }

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsClient && clientId == nm.LocalClientId)
        {
            if (RoleSelectionUI.SelectedRole == ClientRole.Spectator)
            {
                return true;
            }

            var localPlayerObject = nm.LocalClient?.PlayerObject;
            if (localPlayerObject != null)
            {
                return !localPlayerObject.CompareTag("Helicopter");
            }
        }

        return false;
    }

    private void ApprovalCheck(
        NetworkManager.ConnectionApprovalRequest request,
        NetworkManager.ConnectionApprovalResponse response)
    {
        LogDebug($"ApprovalCheck received for client {request.ClientNetworkId}. Payload length: {(request.Payload == null ? 0 : request.Payload.Length)}.");

        // Always approve
        response.Approved = true;
        response.CreatePlayerObject = false;
        LogDebug("Client approved with CreatePlayerObject=false (manual spawning path).");

        // ✅ Host-local client special case:
        // Don't spawn here, OnServerStarted will handle it.
        if (NetworkManager.Singleton.IsHost &&
            request.ClientNetworkId == NetworkManager.Singleton.LocalClientId)
        {
            pendingClientRoles[request.ClientNetworkId] = hostRole;
            LogDebug("ApprovalCheck host-local special case hit. Deferring spawn to OnServerStarted.");
            return;
        }

        // Remote clients: read payload
        ClientRole role = ClientRole.Player;
        if (request.Payload != null && request.Payload.Length > 0)
        {
            role = (ClientRole)request.Payload[0];
            LogDebug($"Parsed remote client role from payload: {role}.");
        }
        else
        {
            LogDebug("No payload role provided. Defaulting role to Player.");
        }

        pendingClientRoles[request.ClientNetworkId] = role;
        LogDebug($"Stored pending role for client {request.ClientNetworkId}: {role}.");
    }

    private void SpawnForClient(ulong clientId, ClientRole role)
    {
        LogDebug($"SpawnForClient called. ClientId={clientId}, Role={role}.");

        if (world != null)
        {
            world.SetActive(true);
            LogDebug("World object set active.");
        }
        else
        {
            LogDebug("World reference is null. Skipping world activation.");
        }

        var prefab = (role == ClientRole.Spectator) ? spectatorPrefab : playerPrefab;
        LogDebug($"Selected prefab: {(prefab == null ? "null" : prefab.name)}.");

        if (prefab == null)
        {
            LogDebug("Selected prefab is null. Aborting spawn.");
            return;
        }

        var spawnPoint = GetSpawnPoint(role);
        var spawnPosition = spawnPoint != null ? spawnPoint.position : prefab.transform.position;
        var spawnRotation = spawnPoint != null ? spawnPoint.rotation : prefab.transform.rotation;

        if (spawnPoint != null)
        {
            LogDebug($"Using spawn point '{spawnPoint.name}' for role {role}.");
        }
        else
        {
            LogDebug($"No spawn point configured for role {role}. Using prefab transform as fallback.");
        }

        var go = Instantiate(prefab, spawnPosition, spawnRotation);
        var no = go.GetComponent<NetworkObject>();

        if (no == null)
        {
            LogDebug($"Spawned prefab '{go.name}' is missing NetworkObject. Destroying instance and aborting spawn.");
            Destroy(go);
            return;
        }

        no.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        LogDebug($"Spawned network player object '{go.name}' for client {clientId}.");
    }

    private Transform GetSpawnPoint(ClientRole role)
    {
        if (role == ClientRole.Spectator)
        {
            return GetNextSpawnPoint(spectatorSpawnPoints, ref nextSpectatorSpawnIndex);
        }

        return GetNextSpawnPoint(playerSpawnPoints, ref nextPlayerSpawnIndex);
    }

    private Transform GetNextSpawnPoint(Transform[] spawnPoints, ref int nextIndex)
    {
        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            return null;
        }

        int attempts = spawnPoints.Length;
        for (int i = 0; i < attempts; i++)
        {
            int index = nextIndex % spawnPoints.Length;
            nextIndex++;

            var spawnPoint = spawnPoints[index];
            if (spawnPoint != null)
            {
                return spawnPoint;
            }
        }

        return null;
    }
}
