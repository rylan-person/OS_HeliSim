using System.Threading.Tasks;
using UnityEngine;
using Unity.Cinemachine;

public class AutoCam : MonoBehaviour
{
    public CinemachineCamera vCam;
    public Transform target;

    public Vector3 smoothedVelocity;
    public Vector3 averageVelocity;

    [Range(0f, 1f)] public float velocitySmoothing = 0.1f;  // 0.1 = 10% new, 90% old

    private Vector3 lastTargetPosition = Vector3.zero;
    public Vector3 projectedPosition = Vector3.zero;
    public float maxDistance = 500f; // Maximum distance from the target to reposition the camera
    public float repositionRadius = 160f; // Radius around the target to search for a new position
    public float velocityWeight = 5f;

    public float distanceToTarget = 0f;
    public float distanceToTargetThreshold = 100f; // Distance threshold to trigger repositioning
    public float distanceToProjectedTarget = 0f;
    public Rigidbody targetRb;
    public bool DrawDebugLines = false;

    private void LateUpdate()
    {
        if (target == null || targetRb == null)
        {
            if (GameObjectTarget.target.ContainsKey("CameraTarget"))
            {
                target = GameObjectTarget.target["CameraTarget"].transform;
                if (target.parent == null) return;
                targetRb = target.parent.GetComponent<Rigidbody>();
                vCam.LookAt = target;
            }
            return;
        }

        // Calculate raw velocity
        Vector3 rawVelocity = (target.position - lastTargetPosition) / Time.deltaTime;
        lastTargetPosition = target.position;

        // Smooth the velocity (EMA)
        smoothedVelocity = Vector3.Lerp(smoothedVelocity, rawVelocity, velocitySmoothing);

        projectedPosition = target.position + smoothedVelocity * velocityWeight;

        if (Mathf.Abs(smoothedVelocity.magnitude - targetRb.linearVelocity.magnitude) > 10f)
        {
            // If the smoothed velocity differs significantly from the target's rigidbody velocity,
            // we can use the target's velocity for the projected position.
            projectedPosition = target.position;
        }

        

        // Optional: store for reference
        averageVelocity = smoothedVelocity;

        distanceToTarget = Vector3.Distance(transform.position, target.position);
        distanceToProjectedTarget = Vector3.Distance(transform.position, projectedPosition);
    

        if (distanceToProjectedTarget > maxDistance)
        {
            Debug.Log($"AutoCam: Repositioning camera. Distance to projected target: {distanceToProjectedTarget:F2}, Max allowed: {maxDistance}, Target position: {target.position}, Projected position: {projectedPosition}, Current camera position: {transform.position}, smoothedVelocity: {smoothedVelocity}, targetRb.velocity: {targetRb.linearVelocity}");
            bool foundPosition = false;
            int attempts = 0;
            while (!foundPosition)
            {

                Vector2 randomCircle = Random.insideUnitCircle * repositionRadius;
                Vector3 randomPosition = projectedPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

                float groundPosition;

                RaycastHit hit;
                Vector3 rayOrigin = projectedPosition + Vector3.up * 100f;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, 400f))
                {
                    groundPosition = hit.point.y;
                }
                else
                {
                    groundPosition = target.position.y - 150f;
                }

                randomPosition.y = groundPosition + Random.Range(10f, 400f);

                // Check if randomPosition can see the helicopter (target)
                Vector3 directionToTarget = target.position - randomPosition;
                float distanceToTarget = directionToTarget.magnitude;

                RaycastHit visibilityHit;
                if (!Physics.Raycast(randomPosition, directionToTarget.normalized, out visibilityHit, distanceToTarget + 10) ||
                    visibilityHit.transform == target.parent)
                {
                    // Debug.Log($"AutoCam: Found new camera position at {randomPosition} that can see the target. Distance to target: {distanceToTarget:F2}");
                    foundPosition = true;
                    transform.position = randomPosition;
                     Debug.DrawLine(randomPosition, target.position, Color.green, 10f);
                } else
                {
                    Debug.Log($"AutoCam: Position at {randomPosition} cannot see the target. Hit object: {visibilityHit.transform.name}, Distance to target: {distanceToTarget:F2}");
                    // info about visibilityHit
                    Debug.Log($"AutoCam: Visibility hit info - Hit point: {visibilityHit.point}, Hit normal: {visibilityHit.normal}, Hit distance: {visibilityHit.distance}");
                    // draw the raycast for debugging
                    Debug.DrawLine(randomPosition, visibilityHit.point, Color.red, 5f);

                    attempts++;
                    if (attempts > 100)
                    {
                        Debug.LogWarning("AutoCam: Unable to find a suitable camera position after 100 attempts.");
                        foundPosition = true;
                    }
                }
            }

        }

    }


    // Draw a circle at the project position
    private void OnDrawGizmos()
    {
        if (projectedPosition != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(projectedPosition, 5f);
        }
        if (DrawDebugLines && target != null)
        {
            // draw circle around camera to show reposition radius
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(target.position, repositionRadius);
            Gizmos.color = Color.green;
        }
    }
}
