using Unity.Cinemachine;
using UnityEngine;

public class CameraTargetSetup : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private CinemachineCamera cinemachineCamera;

    void Start()
    {
        if (!GameObjectTarget.TryGet("CameraTarget", out target))
        {
            Debug.LogWarning($"{nameof(CameraTargetSetup)}: no 'CameraTarget' registered in GameObjectTarget. " +
                              "Ensure a GameObjectAssigner with targetName='CameraTarget' runs before this script.");
            return;
        }

        if (cinemachineCamera != null)
        {
            cinemachineCamera.Follow = target.transform;
            cinemachineCamera.LookAt = target.transform;
        }
    }
}
