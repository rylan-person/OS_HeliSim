using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class smoothCamera : MonoBehaviour
{
    [SerializeField] private Vector3 offset = new Vector3(0f, 0f, -10f);
    [SerializeField] private float smoothVelocityTime = 0.25f;
    [SerializeField] private float smoothRotationTime = 0.25f;
    private Vector3 velocity = Vector3.zero;

    [SerializeField] private Transform target;

    private void Update()
    {
        // Position
        Vector3 targetPosition = target.TransformPoint(offset);
        transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref velocity, smoothVelocityTime);

        // Rotation
        // Make the rotation use the smooth time like position
        transform.rotation = Quaternion.Slerp(transform.rotation, target.rotation, smoothRotationTime);
    }
}
