using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[Serializable]
public class TelemetrySeriesConfig
{
    public TelemetrySeries series;
    public Color color = Color.white;
    [Tooltip("Label shown in the legend.")]
    public string label;
}

[RequireComponent(typeof(RectTransform))]
public class TelemetryLineGraphic : Graphic
{
    [Header("Series")]
    [Tooltip("Each entry is one line drawn on this graph.")]
    public List<TelemetrySeriesConfig> series = new();

    [Header("Time Window")]
    [Tooltip("How many seconds of history to show.")]
    public float timeWindow = 10f;

    [Header("Y Axis")]
    [Tooltip("Bottom of the value range.")]
    public float valueMin = -1f;
    [Tooltip("Top of the value range.")]
    public float valueMax = 1f;

    [Header("Visual")]
    [Tooltip("Line thickness in UI units.")]
    public float lineThickness = 2f;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    private void Update()
    {
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();

        var collector = ClientTelemetryCollector.ActiveCollector;
        if (collector == null)
            return;

        Rect rect = GetPixelAdjustedRect();
        float now = Time.time;
        float minTime = now - timeWindow;

        foreach (TelemetrySeriesConfig config in series)
        {
            var samples = collector.GetSamples(config.series);
            if (samples.Count < 2)
                continue;

            Vector2 prev = SampleToLocal(samples[0], minTime, rect);
            for (int i = 1; i < samples.Count; i++)
            {
                Vector2 curr = SampleToLocal(samples[i], minTime, rect);
                AddSegment(vh, prev, curr, lineThickness, config.color);
                prev = curr;
            }
        }
    }

    private Vector2 SampleToLocal(ClientTelemetryCollector.Sample s, float minTime, Rect rect)
    {
        float tNorm = Mathf.InverseLerp(minTime, minTime + timeWindow, s.time);
        float x = rect.xMin + tNorm * rect.width;

        float vNorm = Mathf.InverseLerp(valueMin, valueMax, s.value);
        float y = rect.yMin + vNorm * rect.height;

        return new Vector2(x, y);
    }

    public bool HasSeries(TelemetrySeries target)
    {
        return series.Exists(c => c.series == target);
    }

    public void AddSeries(TelemetrySeries target, Color color, string label = "")
    {
        if (HasSeries(target))
            return;

        series.Add(new TelemetrySeriesConfig { series = target, color = color, label = label });
    }

    public void RemoveSeries(TelemetrySeries target)
    {
        series.RemoveAll(c => c.series == target);
    }

    private static void AddSegment(VertexHelper vh, Vector2 start, Vector2 end, float thickness, Color col)
    {
        Vector2 dir = end - start;
        if (dir.sqrMagnitude < Mathf.Epsilon)
            return;

        dir.Normalize();
        Vector2 normal = new Vector2(-dir.y, dir.x);
        float half = thickness * 0.5f;

        Vector2 v0 = start - normal * half;
        Vector2 v1 = start + normal * half;
        Vector2 v2 = end + normal * half;
        Vector2 v3 = end - normal * half;

        int idx = vh.currentVertCount;

        UIVertex vert = UIVertex.simpleVert;
        vert.color = col;

        vert.position = v0; vh.AddVert(vert);
        vert.position = v1; vh.AddVert(vert);
        vert.position = v2; vh.AddVert(vert);
        vert.position = v3; vh.AddVert(vert);

        vh.AddTriangle(idx + 0, idx + 1, idx + 2);
        vh.AddTriangle(idx + 0, idx + 2, idx + 3);
    }
}
