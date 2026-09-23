using UnityEngine;
using CesiumForUnity;
using Mapbox.Unity.Map;
using Mapbox.Utils;

public class HelicopterMapTracker : MonoBehaviour
{
    [SerializeField]
    private CesiumGlobeAnchor helicopterAnchor;

    [SerializeField]
    private AbstractMap map;

    [SerializeField]
    private float zoom = 15f;

    [SerializeField]
    private float mapUpdateInterval = 0.5f;

    private float updateTimer;

    private void Start()
    {
        UpdateMapPosition();
    }

    private void Update()
    {
        updateTimer += Time.deltaTime;

        if (updateTimer >= mapUpdateInterval)
        {
            updateTimer = 0f;
            UpdateMapPosition();
        }
    }

    private void UpdateMapPosition()
    {
        if (helicopterAnchor == null || map == null)
            return;

        var cesiumPosition =
            helicopterAnchor.longitudeLatitudeHeight;

        var latLon = new Vector2d(
            cesiumPosition.y, // latitude
            cesiumPosition.x  // longitude
        );

        map.UpdateMap(latLon, zoom);
    }
}