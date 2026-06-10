using UnityEngine;

[DisallowMultipleComponent]
public class DashboardCameraSource : MonoBehaviour
{
    public PanelCameraType cameraType;

    public Camera Camera { get; private set; }

    private void Awake()
    {
        Camera = GetComponentInChildren<Camera>(true);

        if (Camera == null)
        {
            Debug.LogError($"{name} has no Camera component assigned or found in children.", this);
        }

        DashboardCameraRegistry.Register(this);
    }

    // private void OnEnable()
    // {
    //     DashboardCameraRegistry.Register(this);
    // }

    // private void OnDisable()
    // {
    //     DashboardCameraRegistry.Unregister(this);
    // }
}