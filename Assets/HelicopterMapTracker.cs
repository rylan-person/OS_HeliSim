using UnityEngine;
using CesiumForUnity;
using Mapbox.Unity.Map;
using Mapbox.Utils;
using System.Globalization;

public class HelicopterMapTracker : MonoBehaviour
{
    [SerializeField]
    private CesiumGlobeAnchor helicopterAnchor;

    [SerializeField]
    private AbstractMap map;

    [SerializeField]
    private AbstractMap mapPrefab;

    [SerializeField]
    private Transform helicopterTransform;

    [SerializeField]
    private float zoom = 15f;

    [SerializeField]
    private float mapUpdateInterval = 0.5f;

    private float updateTimer;
    private Vector2d lastMapPosition;
    private bool hasMapPosition;
    private bool mapVisible;
    private int mapCreatedFrame = -1;

    public AbstractMap Map => map;
    public bool HasHelicopter => helicopterTransform != null;

    private void Awake()
    {
        if (helicopterAnchor == null)
            helicopterAnchor = GetComponent<CesiumGlobeAnchor>();

        if (map != null)
            ConfigureMap();
    }

    private void LateUpdate()
    {
        if (!SyncToHelicopter())
            return;

        if (!mapVisible || Time.frameCount <= mapCreatedFrame)
            return;

        updateTimer += Time.deltaTime;

        if (updateTimer >= Mathf.Max(0.1f, mapUpdateInterval))
        {
            updateTimer = 0f;
            UpdateMapPosition();
        }
    }

    public bool SyncToHelicopter()
    {
        if (helicopterTransform == null)
        {
            HelicopterComponents localHelicopter = HelicopterComponents.Instance;
            if (localHelicopter != null)
            {
                helicopterTransform = localHelicopter.helicopterRb != null
                    ? localHelicopter.helicopterRb.transform
                    : localHelicopter.helicopterTransform != null
                        ? localHelicopter.helicopterTransform
                        : localHelicopter.transform;
            }
        }

        if (helicopterTransform == null || helicopterAnchor == null)
            return false;

        transform.SetPositionAndRotation(helicopterTransform.position, helicopterTransform.rotation);
        helicopterAnchor.Sync();
        return true;
    }

    public void SetMapVisible(bool visible)
    {
        mapVisible = visible;
        if (visible)
            updateTimer = Mathf.Max(0.1f, mapUpdateInterval);
    }

    public AbstractMap GetOrCreateMap()
    {
        if (map == null && mapPrefab != null)
        {
            map = Instantiate(mapPrefab);
            mapCreatedFrame = Time.frameCount;
            ConfigureMap();
        }

        return map;
    }

    private void ConfigureMap()
    {
        // Keep Mapbox's flat street tiles away from the Cesium world cameras.
        map.transform.position = new Vector3(0f, -1000000f, 0f);
        map.gameObject.layer = 8;
        CesiumGeoreference georeference = GetComponentInParent<CesiumGeoreference>();
        if (georeference != null)
        {
            map.Options.locationOptions.latitudeLongitude = string.Format(
                CultureInfo.InvariantCulture, "{0},{1}", georeference.latitude, georeference.longitude);
        }
        map.Options.locationOptions.zoom = zoom;
        map.Terrain.SetElevationType(ElevationLayerType.FlatTerrain);
        map.Terrain.EnableCollider(false);
        map.Terrain.AddToUnityLayer(8);
        map.Options.extentOptions.extentType = MapExtentType.RangeAroundCenter;
        map.Options.extentOptions.defaultExtents.rangeAroundCenterOptions.SetOptions(2, 2, 2, 2);
    }

    private void UpdateMapPosition()
    {
        if (helicopterAnchor == null || map == null)
            return;

        var cesiumPosition =
            helicopterAnchor.longitudeLatitudeHeight;

        Vector2d latLon = new Vector2d(
            cesiumPosition.y, // latitude
            cesiumPosition.x  // longitude
        );

        if (hasMapPosition &&
            System.Math.Abs(latLon.x - lastMapPosition.x) < 0.000001d &&
            System.Math.Abs(latLon.y - lastMapPosition.y) < 0.000001d)
            return;

        map.UpdateMap(latLon, zoom);
        lastMapPosition = latLon;
        hasMapPosition = true;
    }
}
