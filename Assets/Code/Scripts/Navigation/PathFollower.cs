using UnityEngine;
using System.Collections.Generic;

public class PathFollower : MonoBehaviour
{
    public List<Transform> waypoints;
    public float speed = 3f;
    public int currentWaypointIndex = 0;

    void Update()
    {
        if (waypoints.Count == 0) return;

        // Move towards the current waypoint
        Transform targetWaypoint = waypoints[currentWaypointIndex];
        Vector3 direction = targetWaypoint.position - transform.position;
        transform.position += direction.normalized * speed * Time.deltaTime;

        // Check if we've reached the waypoint
        if (Vector3.Distance(transform.position, targetWaypoint.position) < 0.1f)
        {
            currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Count;
        }
    }
}

