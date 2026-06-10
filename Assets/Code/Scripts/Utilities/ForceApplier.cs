using System.Collections;
using System.Collections.Generic;
using Oyedoyin.RotaryWing;
using UnityEngine;

public class ForceApplier : MonoBehaviour
{
    public Rigidbody rb;
    public RotaryController rotaryController;
    public float locationY = 0f;
    public float locationX = 0f;
    public float locationZ = 0f;
    public float forceMagnitude = 0f;
    public float base_wind = 0.4f;
    public float gust_wind = 2f;

    [Tooltip("In degrees")]
    [Range(-180f, 180f)]
    public float forceRotation = 0f;
    // Start is called before the first frame update
    void Start()
    {
        rb = HelicopterComponents.Instance.helicopterRb;
        rotaryController = HelicopterComponents.Instance.rotaryController;

        // Initialize forceRotation to a random direction
        forceRotation = Random.Range(-180f, 180f);
    }

    // Update is called once per frame
    void Update()
    {
        // Simulate wind gusts
        // Make gusts much rarer by reducing Perlin noise frequency
        float gustFrequency = 0.25f; // Lower value = rarer gusts
        forceMagnitude = base_wind + Mathf.Pow(Mathf.PerlinNoise(Time.time * gustFrequency, 0f), 3.5f) * gust_wind;

        // Simulate wind direction changes
        float directionChangeSpeed = 5f; // Speed of direction change
        forceRotation += (Mathf.PerlinNoise(Time.time * 0.1f, 1f) - 0.5f) * directionChangeSpeed * Time.deltaTime;
        forceRotation = Mathf.Repeat(forceRotation + 180f, 360f) - 180f; // Keep within -180 to 180 degrees

        rb.AddForceAtPosition(
            Quaternion.Euler(0f, forceRotation, 0f) * Vector3.forward * forceMagnitude * rotaryController.emptyWeight,
            transform.position + new Vector3(locationX, locationY, locationZ),
            ForceMode.Force);
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position + new Vector3(locationX, locationY, locationZ),
                        transform.position + new Vector3(locationX, locationY, locationZ) + Quaternion.Euler(0f, forceRotation, 0f) * Vector3.forward * forceMagnitude);
    }
}
