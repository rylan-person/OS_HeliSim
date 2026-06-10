using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class XJoyGrapher : Graphic
{
    [Header("Source")]
    [Tooltip("Optional explicit source. If null, uses currently spectated source.")]
    public ClientTelemetryCollector dataCollector;

    [Header("Time Window")]
    [Tooltip("How many seconds of history to show.")]
    public float timeWindow = 10f; 

    [Header("Visual")]
    [Tooltip("Line thickness in UI units (pixels).")]
    public float lineThickness = 2f;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false; // so the graph doesn't block UI clicks
    }

    void Update()
    {
        SetVerticesDirty();
    }

    // Called by Unity to rebuild the mesh
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var collector = ResolveCollector();
        if (collector == null)
            return;

        var samples = collector.GetSamples(TelemetrySeries.XJoy);

        //Debug.Log($"XJoyGrapher: Found {samples.Count} samples from collector '{(collector != null ? collector.name : "null")}'");

        if (samples.Count < 2)
            return;

        /* log how many non 0 samples there are, to check if we're actually getting data
        int nonZeroCount = 0;
        foreach (var s in samples)        {
            if (Mathf.Abs(s.value) > Mathf.Epsilon)
                nonZeroCount++;
        }

        //Debug.Log($"XJoyGrapher: Found {nonZeroCount} non-zero samples from collector '{(collector != null ? collector.name : "null")}'");
        */
        Rect rect = GetPixelAdjustedRect();
        float width = rect.width;
        float height = rect.height;

        float now = Time.time;
        float minTime = now - timeWindow;

        // Build line segments as quads
        Vector2 prevPos = SampleToLocalPosition(samples[0], minTime, rect, width, height);
        for (int i = 1; i < samples.Count; i++)
        {
            Vector2 currPos = SampleToLocalPosition(samples[i], minTime, rect, width, height);
            AddLineSegment(vh, prevPos, currPos, lineThickness);
            prevPos = currPos;
        }
    }

    private Vector2 SampleToLocalPosition(ClientTelemetryCollector.Sample s, float minTime, Rect rect, float width, float height)
    {
        // Time → X in [0, width]
        float tNorm = Mathf.InverseLerp(minTime, minTime + timeWindow, s.time);
        float x = rect.xMin + tNorm * width;

        // Value [-1, 1] → Y in [0, height] (0 at bottom, 1 at top), centred
        float vNorm = (s.value + 1f) * 0.5f; // map [-1,1] to [0,1]
        float y = rect.yMin + vNorm * height;

        return new Vector2(x, y);
    }

    private ClientTelemetryCollector ResolveCollector()
    {
        //Debug.Log($"Resolving XJoy collector. Explicit: {(dataCollector != null ? dataCollector.name : "null")}, Active: {(ClientTelemetryCollector.ActiveCollector != null ? ClientTelemetryCollector.ActiveCollector.name : "null")}");
        return dataCollector != null ? dataCollector : ClientTelemetryCollector.ActiveCollector;
    }

    private void AddLineSegment(VertexHelper vh, Vector2 start, Vector2 end, float thickness)
    {
        Vector2 dir = (end - start);
        if (dir.sqrMagnitude < Mathf.Epsilon)
            return;

        dir.Normalize();
        Vector2 normal = new Vector2(-dir.y, dir.x); // perpendicular
        float halfThickness = thickness * 0.5f;

        Vector2 v0 = start - normal * halfThickness;
        Vector2 v1 = start + normal * halfThickness;
        Vector2 v2 = end + normal * halfThickness;
        Vector2 v3 = end - normal * halfThickness;

        int index = vh.currentVertCount;

        // Color is this.color from the Graphic (set in Inspector)
        UIVertex vert = UIVertex.simpleVert;
        vert.color = color;

        vert.position = v0;
        vh.AddVert(vert);
        vert.position = v1;
        vh.AddVert(vert);
        vert.position = v2;
        vh.AddVert(vert);
        vert.position = v3;
        vh.AddVert(vert);

        vh.AddTriangle(index + 0, index + 1, index + 2);
        vh.AddTriangle(index + 0, index + 2, index + 3);
    }
}
