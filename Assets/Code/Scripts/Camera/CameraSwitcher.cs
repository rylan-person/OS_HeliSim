using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI; // Required for Dropdown
using Unity.Cinemachine;
using TMPro; // Assuming you are using Cinemachine for virtual cameras

public class CameraSwitcher : MonoBehaviour
{
    // Singleton
    public static CameraSwitcher Instance { get; private set; }

    public TMP_Dropdown cameraDropdown; // Assign this in the inspector
    public CinemachineVirtualCameraBase thirdPersonCam;
    public CinemachineVirtualCameraBase groundCam;
    public CinemachineVirtualCameraBase mountedCam;
    public TimeTrialCameraManager fixedCams;

    private List<CinemachineVirtualCameraBase> allCameras;

    // Priority levels: higher value means more preferred camera
    private readonly int activePriority = 10;
    private readonly int inactivePriority = 0;
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Populate a list with all the vcams
        allCameras = new List<CinemachineVirtualCameraBase> { thirdPersonCam, groundCam, mountedCam };

        // Set all cameras to inactive priority at the start
        foreach (var cam in allCameras)
        {
            if (cam != null)
            {
                cam.Priority = inactivePriority;
            }
        }

        // Default selection: activate the first camera
        if (allCameras.Count > 0)
        {
            allCameras[0].Priority = activePriority;
        }

        // Add listener to dropdown
        cameraDropdown.onValueChanged.AddListener(OnCameraSelected);

        thirdPersonCam.Priority = activePriority;
        thirdPersonCam.Follow = GameObject.FindGameObjectWithTag("Helicopter")?.transform;
        thirdPersonCam.LookAt = GameObject.FindGameObjectWithTag("Helicopter")?.transform;

        groundCam.Priority = inactivePriority;
        groundCam.Follow = GameObject.FindGameObjectWithTag("Helicopter")?.transform;
        groundCam.LookAt = GameObject.FindGameObjectWithTag("Helicopter")?.transform;
    }

    // This is called when the dropdown value changes
    public void OnCameraSelected(int index)
    {
        // Set all cameras to the inactive priority
        foreach (var cam in allCameras)
        {
            if (cam != null)
            {
                cam.Priority = inactivePriority;
            }
        }
        fixedCams.DisableFixedCams();

        Debug.Log($"Camera selected: {index}");

        // Set the selected camera to active priority based on dropdown index
        switch (index)
        {
            case 0: // 3rd Person Cam
                thirdPersonCam.Priority = activePriority;
                break;
            case 1: // Ground Cam
                groundCam.Priority = activePriority;
                break;
            case 2: // Mounted Cam
                mountedCam.Priority = activePriority;
                break;
            case 3: // Waypoint Cam
                fixedCams.EnableFixedCams();
                break;
            default:
                Debug.LogError("Unknown camera selection!");
                break;
        }
    }

    void OnDestroy()
    {
        // Remove listener when the object is destroyed
        cameraDropdown.onValueChanged.RemoveListener(OnCameraSelected);
    }
}
