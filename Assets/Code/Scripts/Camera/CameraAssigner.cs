using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class CameraAssigner : MonoBehaviour
{
    public CinemachineCamera vCam;
    public bool followTarget = true;
    public bool lookAtTarget = true;

    public Transform target;

    // Update is called once per frame
    void Update()
    {
        if (target != null)
        {
            return; // Exit if target is set
        }

        target = TimeTrialCameraManager.Instance.GetTarget();

        if (target == null)
        {
            return; // Exit if target is still not set
        }

        if (followTarget)
        {
            vCam.Follow = target; // Set the Follow target
        }

        if (lookAtTarget)
        {
            vCam.LookAt = target; // Set the LookAt target
        }
    }

}
