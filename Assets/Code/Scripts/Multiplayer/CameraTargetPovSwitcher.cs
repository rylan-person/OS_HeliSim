using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class CameraTargetPovSwitcher : MonoBehaviour
{
    [SerializeField] private KeyCode switchKey = KeyCode.Tab;
    [SerializeField] private string cameraTargetKey = "CameraTarget";
    [SerializeField] private bool debugLogsEnabled = false;

    private void Start()
    {
        if (TryGetCameraTarget(out var cameraTarget) && cameraTarget.transform.parent != null)
        {
            ClientTelemetryCollector.SetActiveCollectorFromTransform(cameraTarget.transform.parent);
            PlayerTimeTrialState.SetActiveStateFromTransform(cameraTarget.transform.parent);
        }
    }

    private void Update()
    {
        if (!Input.GetKeyDown(switchKey))
        {
            return;
        }

        SwitchToNextPlayer();
    }

    private void SwitchToNextPlayer()
    {
        // check if player type is spectator and if so, do allow switching
        if (CustomConnectionManager.Instance != null && CustomConnectionManager.Instance.IsSpectatorClient(NetworkManager.Singleton.LocalClientId))
        {
            LogDebug("Current client is a spectator. POV switching is allowed.");
        }
        else
        {
            LogDebug("Current client is not a spectator. POV switching is not allowed.");
            return;
        }

        if (!TryGetCameraTarget(out var cameraTarget))
        {
            LogDebug($"No GameObjectTarget entry found for '{cameraTargetKey}'.");
            return;
        }

        var candidates = GetPlayerObjectTransforms();
        if (candidates.Count == 0)
        {
            LogDebug("No network player objects available to switch POV.");
            return;
        }

        var currentParent = cameraTarget.transform.parent;
        int currentIndex = candidates.IndexOf(currentParent);
        int nextIndex = (currentIndex + 1) % candidates.Count;
        Transform nextParent = candidates[nextIndex];

        cameraTarget.transform.SetParent(nextParent);
        cameraTarget.transform.localPosition = Vector3.zero;
        cameraTarget.transform.localRotation = Quaternion.identity;
        ClientTelemetryCollector.SetActiveCollectorFromTransform(nextParent);
        PlayerTimeTrialState.SetActiveStateFromTransform(nextParent);

        LogDebug($"CameraTarget switched to '{nextParent.name}'.");
    }

    private bool TryGetCameraTarget(out GameObject target)
    {
        target = null;

        if (GameObjectTarget.target == null)
        {
            return false;
        }

        if (!GameObjectTarget.target.TryGetValue(cameraTargetKey, out target))
        {
            return false;
        }

        return target != null;
    }

    private List<Transform> GetPlayerObjectTransforms()
    {
        var result = new List<Transform>();
        var networkManager = NetworkManager.Singleton;

        if (networkManager == null || !networkManager.IsClient)
        {
            return result;
        }

        if (networkManager.ConnectedClients == null || networkManager.ConnectedClients.Count == 0)
        {
            return result;
        }

        var clientIds = new List<ulong>(networkManager.ConnectedClients.Keys);
        clientIds.Sort();

        foreach (var clientId in clientIds)
        {
            if (!networkManager.ConnectedClients.TryGetValue(clientId, out var client))
            {
                continue;
            }

            if (CustomConnectionManager.Instance != null && CustomConnectionManager.Instance.IsSpectatorClient(clientId))
            {
                continue;
            }

            var playerObject = client.PlayerObject;
            if (playerObject == null)
            {
                continue;
            }

            if (!playerObject.CompareTag("Helicopter"))
            {
                continue;
            }

            result.Add(playerObject.transform);
        }

        return result;
    }

    private void LogDebug(string message)
    {
        if (!debugLogsEnabled)
        {
            return;
        }

        Debug.Log($"[CameraTargetPovSwitcher] {message}");
    }
}