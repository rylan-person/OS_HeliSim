using UnityEngine;
using UnityEngine.AI;

public class RandomPatrolAgent : MonoBehaviour
{
    public float patrolRadius = 10f;  // Radius for random points
    public float stoppingDistance = 0.5f;  // Distance at which destination is considered reached

    private NavMeshAgent agent;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        SetNewRandomDestination();
    }

    void Update()
    {
        // Check if the agent has reached the destination
        if (!agent.pathPending && agent.remainingDistance <= stoppingDistance)
        {
            // Choose a new random point on the NavMesh
            SetNewRandomDestination();
        }
    }

    void SetNewRandomDestination()
    {
        Vector3 randomPoint = GetRandomPointOnNavMesh();
        agent.SetDestination(randomPoint);
    }

    Vector3 GetRandomPointOnNavMesh()
    {
        Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
        randomDirection += transform.position;  // Offset from the agent's current position

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, patrolRadius, NavMesh.AllAreas))
        {
            return hit.position;
        }

        return transform.position;  // Fallback if no point is found
    }
    
    // Draw the patrol radius in the Scene view constantly and draw the path in the Scene view when selected
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, patrolRadius);
    }

    void OnDrawGizmos()
    {
        if (agent == null) return;

        Gizmos.color = Color.blue;
        for (int i = 0; i < agent.path.corners.Length - 1; i++)
        {
            Gizmos.DrawLine(agent.path.corners[i], agent.path.corners[i + 1]);
        }
    }
}
