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

    public override void OnNetworkSpawn()
    {
        DebugPrint($"OnNetworkSpawn called. IsOwner: {IsOwner}, IsClient: {IsClient}, IsServer: {IsServer}");
        if (!IsOwner)  // or use IsLocalPlayer if preferred
        {
            DebugPrint("Setting up for remote player (not owner)");
            var cameraTarget = GetCameraTarget();
            if (cameraTarget != null)
            {
                if (cameraTarget.transform.parent == null)
                {
                    DebugPrint($"CameraTarget '{cameraTarget.name}' has no parent. Re-parenting to '{this.name}'.");
                    cameraTarget.transform.SetParent(this.transform);
                    cameraTarget.transform.localPosition = Vector3.zero;
                    cameraTarget.transform.localRotation = Quaternion.identity;
                } else
                {
                    DebugPrint($"CameraTarget '{cameraTarget.name}' already has a parent '{cameraTarget.transform.parent.name}'. Not re-parenting.");
                }
            } else
            {
                DebugPrint("CameraTarget is null. Ensure GameObjectAssigner has targetName='CameraTarget' and runs before player spawn.");
            }
            // Disable any scripts you dragged into the array
            foreach (var script in localOnlyScripts)
                if (script != null) script.enabled = false;

            // list local only objects for debugging
            DebugPrint("Length of localOnlyObjects: " + localOnlyObjects.Length);
            foreach (var go in localOnlyObjects)
            {
                if (go != null)
                {
                    DebugPrint($"Local-only GameObject '{go.name}' will be disabled for non-owner player '{gameObject.name}'");
                }
            }
            // Disable any GameObjects (Cameras, UI, etc.)
            foreach (var go in localOnlyObjects)
            {
                var targetName = go != null ? go.name : "<null>";
                DebugPrint($"Disabling local-only GameObject '{targetName}' for non-owner player '{gameObject.name}'");
                if (go != null) go.SetActive(false);
                
            }
        }
        else
        {
            if (WaypointManager.Instance != null) WaypointManager.Instance.helicopterTransform = this.transform;
            // Move cameratarget to be a child of this player
            var cameraTarget = GetCameraTarget();
            if (cameraTarget != null)
            {
                DebugPrint($"CameraTarget '{cameraTarget.name}' will be re-parented to '{this.name}' for owner player.");
                cameraTarget.transform.SetParent(this.transform);
                cameraTarget.transform.localPosition = Vector3.zero;
                cameraTarget.transform.localRotation = Quaternion.identity;
            } else
            {
                DebugPrint("CameraTarget is null. Ensure GameObjectAssigner has targetName='CameraTarget' and runs before player spawn.");
            }
            // Disable any scripts you dragged into the array
            foreach (var script in remoteOnlyScripts)
                if (script != null) script.enabled = false;

            // Disable any GameObjects (Cameras, UI, etc.)
            foreach (var go in remoteOnlyObjects)
                if (go != null) go.SetActive(false);
        }
    }

    public void DebugPrint(string message)
    {
        Debug.Log($"[LocalPlayerSetup] {message}");
    }

    private GameObject GetCameraTarget()
    {
        if (GameObjectTarget.target == null)
        {
            DebugPrint("GameObjectTarget dictionary is null.");
            return null;
        }

        if (!GameObjectTarget.target.TryGetValue("CameraTarget", out var cameraTarget) || cameraTarget == null)
        {
            DebugPrint("CameraTarget not found in GameObjectTarget. Ensure GameObjectAssigner has targetName='CameraTarget' and runs before player spawn.");
            return null;
        }

        return cameraTarget;
    }
}
