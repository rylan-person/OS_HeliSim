using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class verticalDrag : MonoBehaviour
{
    [SerializeField] private float verticalSpeed = 0.0f;
    [SerializeField] private float drag = 0.0f;
    [SerializeField] private Rigidbody rb;
    [SerializeField] private float dragCoefficient = 0.1f;

    void FixedUpdate()
    {
        // have the vertical drag be proportional to the velocity of the object
        verticalSpeed = rb.linearVelocity.y;
        drag = dragCoefficient * verticalSpeed;
        if (rb.linearVelocity.y > 0)
        {
            rb.AddForce(new Vector3(0, -drag*rb.mass, 0));
        }
    }
}
