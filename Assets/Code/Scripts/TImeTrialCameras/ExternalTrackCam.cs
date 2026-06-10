using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using Unity.VisualScripting;
using UnityEngine;

public class ExternalTrackCam : MonoBehaviour
{
    private readonly int activePriority = 10;
    private readonly int inactivePriority = 0;
    private TimeTrialCameraManager timeTrialCameraManager;
    private Transform target;
    public float smoothSpeed = 0.125f;
    [SerializeField] public int startingWaypoint = 0;
    [SerializeField] public CinemachineVirtualCameraBase externalTrackCam;

    [SerializeField] private float maxZoom = 6f;
    [SerializeField] private float minZoom = 50f;

    [SerializeField] private float maxDistance = 400f;
    [SerializeField] private float minDistance = 20f;

    private void Start()
    {
        timeTrialCameraManager = FindObjectOfType<TimeTrialCameraManager>();
        target = timeTrialCameraManager.GetTarget();
    }

    public void EnableCamera()
    {
        externalTrackCam.Priority = activePriority;
    }

    public void DisableCamera()
    {
        externalTrackCam.Priority = inactivePriority;
    }

    void Update()
    {
        if (target == null)
        {
            target = timeTrialCameraManager.GetTarget();
        }
    }
}
