using UnityEngine;
using Unity.XR.CoreUtils;

public class Recenter : MonoBehaviour
{
    public XROrigin XROrigin;
    public Transform TargetTransform;

    public void RecenterHeadset()
    {
        Debug.Log("RecenterHeadset called");
        XROrigin.MoveCameraToWorldLocation(TargetTransform.position);
    }
}
