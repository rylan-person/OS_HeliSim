using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class Waypoint : MonoBehaviour
{
    public int waypointNumber;
    [SerializeField] private int waypointSectorEnd = -1;
    [SerializeField] private WaypointManager waypointManager;
    [SerializeField] private GameObject[] checkpointFills;
    public float distanceFromEnd = 0f;

    public void setAsNext()
    {
        checkpointFills[0].SetActive(true);
        checkpointFills[1].SetActive(false);
    }

    public void checkpointPassed()
    {
        checkpointFills[0].SetActive(false);
        checkpointFills[1].SetActive(false);
    }

    public void resetCheckpoint()
    {
        checkpointFills[0].SetActive(false);
        checkpointFills[1].SetActive(true);
    }

    // If colliding with the helicopter call the finishSector method in the waypoint manager
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Helicopter"))
        {
            return;
        }

        var networkObject = other.GetComponentInParent<NetworkObject>();
        if (networkObject != null && !networkObject.IsOwner)
        {
            return;
        }

        Debug.Log("Waypoint " + waypointNumber + " reached");
        bool correctCheckpoint = waypointManager.finishWaypoint(waypointNumber);
        if (correctCheckpoint && waypointSectorEnd != -1)
        {
            waypointManager.finishSector(waypointSectorEnd);
        }
    }
}
