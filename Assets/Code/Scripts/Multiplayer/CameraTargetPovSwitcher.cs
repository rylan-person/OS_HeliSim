using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CameraTargetPovSwitcher : MonoBehaviour
{
    [SerializeField] private KeyCode switchKey = KeyCode.Tab;
    [SerializeField] private string cameraTargetKey = "CameraTarget";
    [SerializeField] private bool debugLogsEnabled = false;
    private readonly List<NetworkHelicopterRegistry> candidates = new List<NetworkHelicopterRegistry>();
    private CameraTargetFollower follower;
    private NetworkObject lastLocalPlayer;
    private bool registryChanged = true;

    private void OnEnable()
    {
        registryChanged = true;
        lastLocalPlayer = null;
        NetworkHelicopterRegistry.Changed += OnRegistryChanged;
    }

    private void OnDisable()
    {
        NetworkHelicopterRegistry.Changed -= OnRegistryChanged;
        if (follower != null) follower.SetTarget(null);
    }

    private void Update()
    {
        RefreshSelection(); // PlayerObject may arrive after connection and spawn notifications.
        if (Input.GetKeyDown(switchKey)) SwitchToNextPlayer();
    }

    private void OnRegistryChanged()
    {
        registryChanged = true;
        RefreshSelection();
    }

    private bool ResolveFollower()
    {
        if (follower != null) return true;
        if (!GameObjectTarget.TryGet(cameraTargetKey, out var target)) return false;
        follower = target.GetComponent<CameraTargetFollower>();
        return follower != null;
    }

    private void RefreshSelection()
    {
        if (!ResolveFollower()) return;
        var manager = NetworkManager.Singleton;
        var localPlayer = manager != null && manager.IsClient ? manager.LocalClient?.PlayerObject : null;
        if (localPlayer == null || !localPlayer.IsSpawned)
        {
            if (!ReferenceEquals(follower.SelectedTarget, null)) follower.SetTarget(null);
            return;
        }
        if (localPlayer.GetComponent<NetworkHelicopterRegistry>() != null)
        {
            if (follower.SelectedTarget != localPlayer.transform) follower.SetTarget(localPlayer.transform);
            return;
        }
        if (CustomConnectionManager.Instance == null || !CustomConnectionManager.Instance.IsSpectatorClient(manager.LocalClientId)) return;
        if (!registryChanged && lastLocalPlayer == localPlayer) return;
        registryChanged = false;
        lastLocalPlayer = localPlayer;
        NetworkHelicopterRegistry.GetHelicopters(manager, candidates);
        if (follower.SelectedTarget != null && candidates.Exists(h => h.transform == follower.SelectedTarget)) return;
        follower.SetTarget(candidates.Count > 0 ? candidates[0].transform : null);
    }

    public void SwitchToNextPlayer()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsClient || CustomConnectionManager.Instance == null ||
            !CustomConnectionManager.Instance.IsSpectatorClient(manager.LocalClientId) || !ResolveFollower()) return;
        NetworkHelicopterRegistry.GetHelicopters(manager, candidates);
        if (candidates.Count == 0)
        {
            follower.SetTarget(null);
            return;
        }
        int current = candidates.FindIndex(h => h.transform == follower.SelectedTarget);
        follower.SetTarget(candidates[(current + 1) % candidates.Count].transform);
        if (debugLogsEnabled) Debug.Log($"[CameraTargetPovSwitcher] Watching {follower.SelectedTarget.name}");
    }
}
