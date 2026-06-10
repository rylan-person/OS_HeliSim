using Unity.Cinemachine;
using UnityEngine;

public class CameraTargetSetup : MonoBehaviour
{
    [SerializeField] private GameObject target;
    [SerializeField] private CinemachineCamera cinemachineCamera;

    void Start()
    {
        target = GameObjectTarget.target["CameraTarget"];
        if (target != null && cinemachineCamera != null)
        {
            cinemachineCamera.Follow = target.transform;
            cinemachineCamera.LookAt = target.transform;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
