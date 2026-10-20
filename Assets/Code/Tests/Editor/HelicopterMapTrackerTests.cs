#if UNITY_EDITOR
using System.Reflection;
using CesiumForUnity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Utils;

public class HelicopterMapTrackerTests
{
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
