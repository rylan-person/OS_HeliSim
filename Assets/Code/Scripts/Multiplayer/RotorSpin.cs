using UnityEngine;

public class RotorSpin : MonoBehaviour
{
    [SerializeField] private Transform rotor; // blades transform
    [SerializeField] private float rpm = 400f;

    private void Update()
    {
        float degreesPerSecond = rpm * 293f / 60f;
        rotor.Rotate(Vector3.forward, degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
