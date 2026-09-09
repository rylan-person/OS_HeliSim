using System.Threading.Tasks;
using UnityEngine;
using Unity.Cinemachine;

public class AutoCam : MonoBehaviour
{
    public CinemachineCamera vCam;
    public Transform target;

    [SerializeField] private Vector3 smoothedVelocity;
    [SerializeField] private Vector3 averageVelocity;

    [Range(0f, 1f)] public float velocitySmoothing = 0.1f;  // 0.1 = 10% new, 90% old

    private Vector3 lastTargetPosition = Vector3.zero;
    [SerializeField] private Vector3 projectedPosition = Vector3.zero;
    public float maxDistance = 500f; // Maximum distance from the target to reposition the camera
    public float repositionRadius = 160f; // Radius around the target to search for a new position
    public float velocityWeight = 5f;

    [SerializeField] private float distanceToTarget = 0f;
    public float distanceToTargetThreshold = 100f; // Distance threshold to trigger repositioning
    [SerializeField] private float distanceToProjectedTarget = 0f;
    public Rigidbody targetRb;
    public bool DrawDebugLines = false;

    [Header("Debug")]
    [SerializeField] private bool debugLogsEnabled = false;

    [Header("Reposition Search Tuning")]
    [Tooltip("Velocity difference (m/s) between smoothed velocity and the target rigidbody's actual velocity before we snap the projected position back to the target instead of extrapolating.")]
    [SerializeField] private float velocityDivergenceThreshold = 10f;
    [Tooltip("Height above the projected position from which the ground-detection raycast is cast.")]
    [SerializeField] private float groundRaycastHeight = 100f;
    [Tooltip("Maximum distance of the ground-detection raycast.")]
    [SerializeField] private float groundRaycastDistance = 400f;
    [Tooltip("Fallback height offset below the target used when the ground-detection raycast finds nothing.")]
    [SerializeField] private float fallbackGroundOffset = -150f;
    [Tooltip("Minimum/maximum height above the detected ground the new camera position can be placed at.")]
    [SerializeField] private float minHeightAboveGround = 10f;
    [SerializeField] private float maxHeightAboveGround = 400f;
    [Tooltip("Extra distance added past the target when raycasting for line-of-sight, to avoid the target's own collider blocking the check.")]
    [SerializeField] private float visibilityRaycastPadding = 10f;
    [Tooltip("Number of candidate positions to try before giving up and keeping the last attempted position.")]
    [SerializeField] private int maxRepositionAttempts = 100;

    private void LateUpdate()
    {
        if (target == null || targetRb == null)
        {
            if (GameObjectTarget.TryGet("CameraTarget", out var cameraTargetGO))
            {
                target = cameraTargetGO.transform;
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

        if (Mathf.Abs(smoothedVelocity.magnitude - targetRb.linearVelocity.magnitude) > velocityDivergenceThreshold)
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
            LogDebug($"AutoCam: Repositioning camera. Distance to projected target: {distanceToProjectedTarget:F2}, Max allowed: {maxDistance}, Target position: {target.position}, Projected position: {projectedPosition}, Current camera position: {transform.position}, smoothedVelocity: {smoothedVelocity}, targetRb.velocity: {targetRb.linearVelocity}");
            bool foundPosition = false;
            int attempts = 0;
            while (!foundPosition)
            {
                Vector2 randomCircle = Random.insideUnitCircle * repositionRadius;
                Vector3 randomPosition = projectedPosition + new Vector3(randomCircle.x, 0f, randomCircle.y);

                float groundPosition;

                RaycastHit hit;
                Vector3 rayOrigin = projectedPosition + Vector3.up * groundRaycastHeight;
                if (Physics.Raycast(rayOrigin, Vector3.down, out hit, groundRaycastDistance))
                {
                    groundPosition = hit.point.y;
                }
                else
                {
                    groundPosition = target.position.y + fallbackGroundOffset;
                }

                randomPosition.y = groundPosition + Random.Range(minHeightAboveGround, maxHeightAboveGround);

                // Check if randomPosition can see the helicopter (target)
                Vector3 directionToTarget = target.position - randomPosition;
                float distanceToTarget = directionToTarget.magnitude;

                RaycastHit visibilityHit;
                if (!Physics.Raycast(randomPosition, directionToTarget.normalized, out visibilityHit, distanceToTarget + visibilityRaycastPadding) ||
                    visibilityHit.transform == target.parent)
                {
                    foundPosition = true;
                    transform.position = randomPosition;
                    if (DrawDebugLines)
                    {
                        Debug.DrawLine(randomPosition, target.position, Color.green, 10f);
                    }
                } else
                {
                    LogDebug($"AutoCam: Position at {randomPosition} cannot see the target. Hit object: {visibilityHit.transform.name}, Distance to target: {distanceToTarget:F2}");
                    if (DrawDebugLines)
                    {
                        Debug.DrawLine(randomPosition, visibilityHit.point, Color.red, 5f);
                    }

                    attempts++;
                    if (attempts > maxRepositionAttempts)
                    {
                        Debug.LogWarning($"AutoCam: Unable to find a suitable camera position after {maxRepositionAttempts} attempts.");
                        foundPosition = true;
                    }
                }
            }
        }
    }

    private void LogDebug(string message)
    {
        if (!debugLogsEnabled)
        {
            return;
        }

        Debug.Log(message);
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
