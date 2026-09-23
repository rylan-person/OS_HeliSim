using UnityEngine;
using Unity.XR.CoreUtils;

public class Recenter : MonoBehaviour
{
    public XROrigin XROrigin;
    public Transform TargetTransform;

    public void RecenterHeadset()
    {
        if (XROrigin == null || TargetTransform == null)
        {
            Debug.LogWarning("VR recenter is unavailable because the seated origin or target is not assigned.", this);
            return;
        }

        Debug.Log("RecenterHeadset called");
        XROrigin.MoveCameraToWorldLocation(TargetTransform.position);
    }
}
