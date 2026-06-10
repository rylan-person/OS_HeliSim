using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HelicopterHUDDirectional : MonoBehaviour
{
    GameObject hudCenterGO;
    GameObject velocityMarkerGO;

    [SerializeField]
    new Camera camera;
    Transform cameraTransform;
    [SerializeField]
    Transform hudCenter;
    [SerializeField]
    Transform velocityMarker;
    [SerializeField]
    Rigidbody rb;

    UnityEngine.Vector3 TransformToHUDSpace(Vector3 worldSpace)
    {
        var screenSpace = camera.WorldToScreenPoint(worldSpace);
        return screenSpace - new Vector3(camera.pixelWidth / 2, camera.pixelHeight / 2);
    }

    void UpdateHUDCenter()
    {
        var rotation = cameraTransform.localEulerAngles;
        var hudPos = TransformToHUDSpace(cameraTransform.position + rb.transform.forward); //


        hudCenterGO.SetActive(true);
        hudCenter.localPosition = new Vector3(hudPos.x, hudPos.y, 0);
        hudCenter.localEulerAngles = new Vector3(0, 0, -rotation.z);

    }

    private void Start()
    {
        cameraTransform = camera.transform;
        hudCenterGO = hudCenter.gameObject;
        velocityMarkerGO = velocityMarker.gameObject;
    }


    void UpdateVelocityMarker()
    {
        var velocity = rb.transform.forward;

        if (rb.linearVelocity.sqrMagnitude > 1)
        {
            velocity = rb.linearVelocity;
        }

        var hudPos = TransformToHUDSpace(cameraTransform.position + velocity);

        if (hudPos.z > 0)
        {
            velocityMarkerGO.SetActive(true);
            velocityMarker.localPosition = new Vector3(hudPos.x, hudPos.y, 0);
        }
        else
        {
            velocityMarkerGO.SetActive(false);
        }
    }

    // Update is called once per frame
    void LateUpdate()
    {
        UpdateVelocityMarker();
        UpdateHUDCenter();
    }
}
