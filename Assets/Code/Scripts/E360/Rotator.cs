using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour
{
    [SerializeField] float rollVelocity = 0.0f;  // Z axis
    [SerializeField] float pitchVelocity = 0.0f;  // X axis
    [SerializeField] float yawVelocity = 0.0f;  // Y axis
    
    void FixedUpdate()
    {
        var roll = rollVelocity * Time.deltaTime;
        var pitch = pitchVelocity * Time.deltaTime;
        var yaw = yawVelocity * Time.deltaTime;
        
        transform.Rotate(pitch, yaw, roll, Space.Self);
    }
}
