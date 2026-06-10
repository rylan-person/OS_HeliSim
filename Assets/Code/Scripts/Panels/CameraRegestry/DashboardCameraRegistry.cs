using System.Collections.Generic;
using UnityEngine;

public static class DashboardCameraRegistry
{
    private static readonly Dictionary<PanelCameraType, DashboardCameraSource> cameras = new();

    public static void Register(DashboardCameraSource source)
    {
        //Debug.Log($"Registering dashboard camera: {source.name} of type {source.cameraType}");
        if (source == null)
            return;

        if (cameras.ContainsKey(source.cameraType))
        {
            Debug.LogWarning(
                $"Replacing registered dashboard camera of type {source.cameraType}. " +
                $"Old: {cameras[source.cameraType].name}, New: {source.name}"
            );
        }

        cameras[source.cameraType] = source;
    }

    public static void Unregister(DashboardCameraSource source)
    {
        if (source == null)
            return;

        if (cameras.TryGetValue(source.cameraType, out DashboardCameraSource current))
        {
            if (current == source)
            {
                cameras.Remove(source.cameraType);
            }
        }
    }

    public static DashboardCameraSource Get(PanelCameraType type)
    {
        cameras.TryGetValue(type, out DashboardCameraSource source);
        return source;
    }

    public static Camera GetCamera(PanelCameraType type)
    {
        DashboardCameraSource source = Get(type);
        return source != null ? source.Camera : null;
    }

    public static bool TryGet(PanelCameraType type, out DashboardCameraSource source)
    {
        return cameras.TryGetValue(type, out source);
    }
}