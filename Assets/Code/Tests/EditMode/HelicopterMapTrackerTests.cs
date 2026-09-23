using System.Reflection;
using CesiumForUnity;
using NUnit.Framework;
using UnityEngine;

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
            flightBody.position = new Vector3(120f, 80f, -45f);
            flightBody.rotation = Quaternion.Euler(0f, 40f, 0f);

            Assert.That(tracker.SyncToHelicopter(), Is.True);
            Assert.That(proxy.transform.position, Is.EqualTo(flightBody.position));
            Assert.That(proxy.transform.rotation, Is.EqualTo(flightBody.rotation));
            Assert.That(anchor.longitudeLatitudeHeight.y, Is.InRange(-90d, 90d));
        }
        finally
        {
            Object.DestroyImmediate(helicopter);
            Object.DestroyImmediate(georeferenceObject);
        }
    }
}
