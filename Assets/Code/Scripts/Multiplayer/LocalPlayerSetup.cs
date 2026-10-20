using Unity.Netcode;
using UnityEngine;

public class LocalPlayerSetup : NetworkBehaviour
{
    [Header("Disable these scripts on NON-owners")]
    public Behaviour[] localOnlyScripts;
    [Header("Disable these GameObjects on NON-owners")]
    public GameObject[] localOnlyObjects;
    [Header("Disable these scripts on OWNERS")]
    public Behaviour[] remoteOnlyScripts;
    [Header("Disable these GameObjects on OWNERS")]
    public GameObject[] remoteOnlyObjects;
    [SerializeField] private bool debugLogsEnabled = false;

    public override void OnNetworkSpawn()
    {
        DisableScripts(IsOwner ? remoteOnlyScripts : localOnlyScripts);
        DisableObjects(IsOwner ? remoteOnlyObjects : localOnlyObjects);
        if (!IsOwner) return;
        if (WaypointManager.Instance != null)
            WaypointManager.Instance.BindLocalHelicopter(transform, GetComponent<PlayerTimeTrialState>());
        if (GameObjectTarget.TryGet("CameraTarget", out var target))
        {
            var follower = target.GetComponent<CameraTargetFollower>();
            if (follower != null) follower.SetTarget(transform);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner && WaypointManager.Instance != null) WaypointManager.Instance.UnbindLocalHelicopter(transform);
    }

    private static void DisableScripts(Behaviour[] scripts)
    {
        if (scripts == null) return;
        foreach (var script in scripts) if (script != null) script.enabled = false;
    }

    private static void DisableObjects(GameObject[] objects)
    {
        if (objects == null) return;
        foreach (var item in objects) if (item != null) item.SetActive(false);
    }

    public void DebugPrint(string message)
    {
        if (debugLogsEnabled) Debug.Log($"[LocalPlayerSetup] {message}");
    }
}
