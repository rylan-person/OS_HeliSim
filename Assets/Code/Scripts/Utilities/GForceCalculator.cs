using UnityEngine;

public class GForceCalculator : MonoBehaviour
{
    [SerializeField] private Rigidbody rb;
    [SerializeField] private Transform heliTransform;

    private Vector3 lastVelocity = Vector3.zero;
    private Vector3 currentVelocity = Vector3.zero;

    private Vector3 rotationComp = Vector3.zero;
    private Vector3 accelerationComp = Vector3.zero;

    [SerializeField] private LineRenderer lineRenderer;
    public Vector3[] points;

    private Vector3 lastGForce = Vector3.zero;
    private Vector3 currentGForce = Vector3.zero;

    public Vector3 gForceDirection = Vector3.zero;
    public float gForceMagnitude = 0f;

    private void Start()
    {
        points[0] = Vector3.zero;
        points[1] = Vector3.zero;
        lineRenderer = GetComponent<LineRenderer>();
        SetupLineRenderer();
    }

    void SetupLineRenderer()
    {
        if (points == null || points.Length < 2)
            return;

        lineRenderer.positionCount = points.Length;

        for (int i = 0; i < points.Length; i++)
        {
            lineRenderer.SetPosition(i, points[i]);
        }

        // Optional: Set Line Renderer properties
        lineRenderer.startWidth = 0.1f;
        lineRenderer.endWidth = 0.1f;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default")); // Assign a default material
        lineRenderer.startColor = Color.red;
        lineRenderer.endColor = Color.red;
    }

    void FixedUpdate()
    {
        // Calculate acceleration based on change in velocity
        currentVelocity = rb.linearVelocity;
        var acceleration = (currentVelocity - lastVelocity) / Time.fixedDeltaTime;
        lastVelocity = currentVelocity;

        // Get the angle that points straight down not relative to the helicopter
        Vector3 down = -heliTransform.up * 9.81f;
        down = Quaternion.Inverse(heliTransform.rotation) * down;
        rotationComp = down;

        accelerationComp = acceleration * -1f;

        // Update gForce calculations
        lastGForce = currentGForce;
        currentGForce = rotationComp + accelerationComp;

        // Transform gForce to the local space of the object
        Vector3 localGForce = rotationComp;

        currentGForce = localGForce;

        // Use localGForce for further calculations or display
        Debug.Log("Local GForce: " + localGForce);
    }


    void Update()
    {
        // Interpolate between last and current gForce values for smoothness
        float interpolationFactor = (Time.time - Time.fixedTime) / Time.fixedDeltaTime;
        Vector3 interpolatedGForce = Vector3.Lerp(lastGForce, currentGForce, interpolationFactor);

        // Update gForce direction and magnitude
        gForceMagnitude = interpolatedGForce.magnitude;
        gForceDirection = interpolatedGForce.normalized;

        // Update line renderer
        points[0] = transform.position;
        points[1] = transform.position + gForceDirection * gForceMagnitude;

        for (int i = 0; i < points.Length; i++)
        {
            lineRenderer.SetPosition(i, points[i]);
        }

        // Log Values
        Debug.Log("Rotation Comp: " + rotationComp);
        Debug.Log("Acceleration Comp: " + accelerationComp);
        Debug.Log("GForce Direction: " + gForceDirection);
        Debug.Log("GForce Magnitude: " + gForceMagnitude);
    }
}
