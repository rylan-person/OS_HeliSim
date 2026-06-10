using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TimeTrialCameraManager : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Volume volume;
    public ExternalTrackCam[] externalTrackCams;
    public int currentCam = 0;
    public int currentWaypoint = 0;
    public CesiumCameraManager cesiumCameraManager;
    public bool isFixedCamEnabled = false;

    public static TimeTrialCameraManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public void Start()
    {
        // get the cesium camera manager
        //cesiumCameraManager = CesiumCameraManager.GetOrCreate(gameObject);
        for (int i = 0; i < externalTrackCams.Length; i++)
        {
            externalTrackCams[i].DisableCamera();
            //externalTrackCams[i].externalTrackCam.LookAt = target;
            externalTrackCams[i].externalTrackCam.Follow = target;
        }
    }

    public void IncrementWaypoint()
    {
        currentWaypoint++;
        if (currentCam == externalTrackCams.Length-1)
        {
            Debug.Log("End of cameras");
            return;
        }
        Debug.Log("Starting waypoint: " + externalTrackCams[currentCam+1].startingWaypoint + " Current waypoint: " + currentWaypoint);
        if (externalTrackCams[currentCam+1].startingWaypoint == currentWaypoint)
        {
            externalTrackCams[currentCam].DisableCamera();
            
            currentCam++;
            if (isFixedCamEnabled)
            {
                externalTrackCams[currentCam].EnableCamera();
            }
        }
    }

    public void FixedCamCMEnable()
    {
        externalTrackCams[currentCam].EnableCamera();
    }

   public void Restart()
   {
        externalTrackCams[currentCam].DisableCamera();
        currentCam = 0;
        currentWaypoint = 0;
        if (isFixedCamEnabled)
        {
            externalTrackCams[currentCam].EnableCamera();
        }
   }

    private void Update()
    {
        if (target == null)
        {
            // find the target with tag "Helicopter"
            target = GameObject.FindGameObjectWithTag("Helicopter")?.transform;
        }
    }

    public Transform GetTarget()
    {
        return target;
    }

    public void DisableFixedCams()
    {
        externalTrackCams[currentCam].DisableCamera();
        isFixedCamEnabled = false;
    }

    public void EnableFixedCams()
    {
        isFixedCamEnabled = true;
        externalTrackCams[currentCam].EnableCamera();
    }

     
}
