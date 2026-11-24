#if UNITY_EDITOR
using System.Reflection;
using CesiumForUnity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;
using Mapbox.Unity;
using Mapbox.Unity.Map;
using TMPro;
using UnityEngine.UI;

public class HelicopterMapTrackerTests
{
    [Test]
    public void MissingToken_DoesNotInitializeMapboxOrInstantiateMapAndShowsSettingsPath()
    {
        // Isolate SDK memory only. Never load, save, or delete the user's credential store.
        FieldInfo instanceField = typeof(MapboxAccess).GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic);
        FieldInfo runtimeField = typeof(MapboxAccess).GetField("_runtimeAccessToken", BindingFlags.Static | BindingFlags.NonPublic);
        object previousInstance = instanceField.GetValue(null);
        object previousRuntime = runtimeField.GetValue(null);
        string previousJson = MapboxAccess.ConfigurationJSON;
        GameObject trackerObject = new GameObject("TokenlessTracker");
        GameObject mapObject = new GameObject("UninitializedMap");
        mapObject.SetActive(false);
        AbstractMap map = mapObject.AddComponent<AbstractMap>();
        GameObject panelObject = new GameObject("TokenlessMapPanel", typeof(RectTransform));
        GameObject imageObject = new GameObject("MapImage", typeof(RectTransform), typeof(RawImage));
        imageObject.transform.SetParent(panelObject.transform, false);
        try
        {
            instanceField.SetValue(null, null);
            runtimeField.SetValue(null, null);
            MapboxAccess.ConfigurationJSON = "{\"AccessToken\":\"\"}";
            HelicopterMapTracker tracker = trackerObject.AddComponent<HelicopterMapTracker>();
            typeof(HelicopterMapTracker).GetField("mapPrefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tracker, map);
            Assert.That(tracker.HasMapAccessToken, Is.False);
            Assert.That(tracker.GetOrCreateMap(), Is.Null);
            Assert.That(tracker.Map, Is.Null);

            StreetMapDashboardPanel panel = panelObject.AddComponent<StreetMapDashboardPanel>();
            typeof(StreetMapDashboardPanel).GetField("displayImage", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(panel, imageObject.GetComponent<RawImage>());
            panel.OnPanelShown();
            TMP_Text message = imageObject.GetComponentInChildren<TMP_Text>();
            Assert.That(message, Is.Not.Null);
            Assert.That(message.text, Does.Contain("Settings > World streaming"));
            Assert.That(message.text, Does.Contain("Mapbox token"));
            Assert.That(imageObject.GetComponent<RawImage>().texture, Is.Null);
            Assert.That(panelObject.GetComponentInChildren<Camera>(), Is.Null);
            panel.SetFullscreen(true);
            panel.OnPanelHidden();
            panel.OnPanelShown();

            // The legacy scene-assigned map must also be paused before its Start can initialize services.
            typeof(HelicopterMapTracker).GetField("map", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(tracker, map);
            InvokeAwake(tracker);
            Assert.That(map.enabled, Is.False);
            Assert.That(tracker.GetOrCreateMap(), Is.Null);
            runtimeField.SetValue(null, "synthetic-recovery-token");
            Assert.That(tracker.GetOrCreateMap(), Is.SameAs(map));
            Assert.That(map.enabled, Is.True, "Adding a session token should release the paused map.");
            Assert.That(instanceField.GetValue(null), Is.Null, "Availability checks must not construct Mapbox services.");
        }
        finally
        {
            Object.DestroyImmediate(panelObject);
            Object.DestroyImmediate(trackerObject);
            Object.DestroyImmediate(mapObject);
            instanceField.SetValue(null, previousInstance);
            runtimeField.SetValue(null, previousRuntime);
            MapboxAccess.ConfigurationJSON = previousJson;
        }
    }

    [Test]
    public void SyncToHelicopter_FollowsTheFlightRigidbody()
    {
        GameObject georeferenceObject = new GameObject("Georeference");
        georeferenceObject.AddComponent<CesiumGeoreference>();
        GameObject proxy = new GameObject("MapTracker");
        proxy.transform.SetParent(georeferenceObject.transform);
        CesiumGlobeAnchor anchor = proxy.AddComponent<CesiumGlobeAnchor>();
        HelicopterMapTracker tracker = proxy.AddComponent<HelicopterMapTracker>();
        GameObject helicopter = new GameObject("Helicopter");
        Rigidbody flightBody = helicopter.AddComponent<Rigidbody>();

        try
        {
            typeof(HelicopterMapTracker)
                .GetField("helicopterTransform", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(tracker, flightBody.transform);
            InvokeAwake(tracker);
            Vector3 position = new Vector3(120f, 80f, -45f);
            Quaternion rotation = Quaternion.Euler(0f, 40f, 0f);
            // EditMode does not advance physics to copy Rigidbody setters back to Transform.
            flightBody.transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            Assert.That(flightBody.position, Is.EqualTo(position), "The test body must move away from the origin.");
            // Native physics and Cesium normalize quaternions; compare within float precision.
            Assert.That(flightBody.rotation, Is.EqualTo(rotation).Using(QuaternionEqualityComparer.Instance));

            Assert.That(tracker.SyncToHelicopter(), Is.True);
            Assert.That(proxy.transform.position, Is.EqualTo(flightBody.position));
            Assert.That(proxy.transform.rotation, Is.EqualTo(flightBody.rotation).Using(QuaternionEqualityComparer.Instance));
            Assert.That(anchor.longitudeLatitudeHeight.y, Is.InRange(-90d, 90d));
        }
        finally
        {
            Object.DestroyImmediate(helicopter);
            Object.DestroyImmediate(georeferenceObject);
        }
    }

    private static void InvokeAwake(HelicopterMapTracker tracker)
    {
        MethodInfo awake = typeof(HelicopterMapTracker).GetMethod(
            "Awake", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(awake, Is.Not.Null, "Expected Awake to initialize the Cesium globe anchor.");
        awake.Invoke(tracker, null);
    }
}
#endif
