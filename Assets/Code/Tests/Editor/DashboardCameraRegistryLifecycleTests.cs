using NUnit.Framework;
using UnityEngine;

public sealed class DashboardCameraRegistryLifecycleTests
{
    [Test]
    public void DestroyedCameraDoesNotBlockRegistrationInTheNextSession()
    {
        var oldRoot = new GameObject("PreviousSessionCamera", typeof(Camera), typeof(DashboardCameraSource));
        var previous = oldRoot.GetComponent<DashboardCameraSource>();
        DashboardCameraRegistry.Register(previous);
        Object.DestroyImmediate(oldRoot);
        var newRoot = new GameObject("NextSessionCamera", typeof(Camera), typeof(DashboardCameraSource));
        var current = newRoot.GetComponent<DashboardCameraSource>();
        try
        {
            Assert.DoesNotThrow(() => DashboardCameraRegistry.Register(current));
            Assert.That(DashboardCameraRegistry.Get(current.cameraType), Is.SameAs(current));
        }
        finally { DashboardCameraRegistry.Unregister(current); Object.DestroyImmediate(newRoot); }
    }

    [Test]
    public void DestroyedCameraIsUnavailableToConsumers()
    {
        var root = new GameObject("DisposedCamera", typeof(Camera), typeof(DashboardCameraSource));
        var source = root.GetComponent<DashboardCameraSource>();
        var type = source.cameraType;
        DashboardCameraRegistry.Register(source);
        Object.DestroyImmediate(root);
        Assert.That(DashboardCameraRegistry.TryGet(type, out _), Is.False);
        Assert.That(DashboardCameraRegistry.Get(type) == null, Is.True);
    }
}
