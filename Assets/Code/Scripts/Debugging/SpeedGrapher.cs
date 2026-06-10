using System.Collections.Generic;
using Oyedoyin.RotaryWing;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public class SpeedGrapher : Graphic
{
    // singleton
    public static SpeedGrapher Instance { get; private set; }

    [Header("Input")]
    [Tooltip("Input axis name for the joystick X (Project Settings > Input).")]
    public string axisName = "XJoy"; // or "Horizontal"

    public RotaryController m_vehicle;

    [Header("Time Window")]
    [Tooltip("How many seconds of history to show.")]
    public float timeWindow = 25f; 

    [Tooltip("Samples taken per second.")]
    public int samplesPerSecond = 60;

    [Header("Visual")]
    [Tooltip("Line thickness in UI units (pixels).")]
    public float lineThickness = 2f;

    [Header("Input Amount")]
    public float xJoy_input; 

    public float MaxSpeed = 0f; // max speed for normalizing the graph

    // Internal storage of samples: (time, value)
    private struct Sample
    {
        public float time;
        public float value;
    }

    private readonly List<Sample> samples = new List<Sample>();
    private float sampleInterval;
    private float sampleTimer;

    protected override void Awake()
    {
        base.Awake();

        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        sampleInterval = 1f / Mathf.Max(1, samplesPerSecond);
        sampleTimer = 0f;
        raycastTarget = false; // so the graph doesn't block UI clicks
    }

    void Update()
    {
        float dt = Time.deltaTime;
        sampleTimer += dt;

        // Take samples at fixed intervals
        while (sampleTimer >= sampleInterval)
        {
            sampleTimer -= sampleInterval;
            AddSample();
        }

        // Remove samples older than timeWindow
        float now = Time.time;
        float cutoff = now - timeWindow;
        while (samples.Count > 0 && samples[0].time < cutoff)
        {
            samples.RemoveAt(0);
        }

        // Mark the graphic as dirty so it redraws
        SetVerticesDirty();
    }

    private void AddSample()
    {
        //float value = xJoy_input; // use the input amount directly
        if (m_vehicle == null || m_vehicle.m_core == null)
        {
            return;
        }
        double u = m_vehicle.m_core.u;
        double v = m_vehicle.m_core.v;
        float Speed = (float)System.Math.Sqrt((u * u) + (v * v));

        float value = Speed * 3.6f;
        MaxSpeed = Mathf.Max(MaxSpeed, value);
        samples.Add(new Sample { time = Time.time, value = value });
    }

    // Called by Unity to rebuild the mesh
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        if (samples.Count < 2)
            return;

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

    private Vector2 SampleToLocalPosition(Sample s, float minTime, Rect rect, float width, float height)
    {
        // Time → X in [0, width]
        float tNorm = Mathf.InverseLerp(minTime, minTime + timeWindow, s.time);
        float x = rect.xMin + tNorm * width;

        // Value [0, MaxSpeed] → Y in [0, height] (0 at bottom, 1 at top), centred
        float vNorm = Mathf.InverseLerp(0f, MaxSpeed, s.value);
        float y = rect.yMin + vNorm * height;

        return new Vector2(x, y);
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
