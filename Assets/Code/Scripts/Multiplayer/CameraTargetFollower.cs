using UnityEngine;

/// <summary>Follows a view source without transferring the scene anchor's lifetime.</summary>
[DefaultExecutionOrder(-50)]
public class CameraTargetFollower : MonoBehaviour
{
    public Transform SelectedTarget { get; private set; }
    private Vector3 unassignedPosition;
    private Quaternion unassignedRotation;

    private void Awake()
    {
        unassignedPosition = transform.position;
        unassignedRotation = transform.rotation;
    }

    public void SetTarget(Transform target)
    {
        SelectedTarget = target;
        ClientTelemetryCollector.SetActiveCollectorFromTransform(target);
        PlayerTimeTrialState.SetActiveStateFromTransform(target);
        FollowTarget();
    }

    private void LateUpdate()
    {
        if (SelectedTarget == null && !ReferenceEquals(SelectedTarget, null)) SetTarget(null);
        FollowTarget();
    }

    private void FollowTarget()
    {
        if (SelectedTarget != null)
            transform.SetPositionAndRotation(SelectedTarget.position, SelectedTarget.rotation);
        else
            transform.SetPositionAndRotation(unassignedPosition, unassignedRotation);
    }

    private void OnDestroy()
    {
        if (!ReferenceEquals(SelectedTarget, null)) SetTarget(null);
    }
}
