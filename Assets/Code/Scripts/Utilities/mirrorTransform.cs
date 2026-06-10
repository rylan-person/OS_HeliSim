using UnityEngine;

public class mirrorTransform : MonoBehaviour
{
    public Transform heliTransformTarget;
    public Transform vrTransformTarget;

    // Update is called once per frame
    void FixedUpdate()
    {
        if (heliTransformTarget != null && vrTransformTarget != null)
        {
            // copy the position
            transform.position = vrTransformTarget.position;
            // copy the rotation
            transform.rotation = vrTransformTarget.rotation;
        }
    }
}
