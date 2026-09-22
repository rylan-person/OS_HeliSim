using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class DashboardManagerTests
{
    private DashboardManager manager;
    private DashboardSlot topLeft;
    private DashboardSlot topRight;
    private DashboardSlot bottomLeft;
    private DashboardSlot bottomRight;
    private DashboardPanel followCameraPanel;
    private DashboardPanel telemetryPanel;

    [SetUp]
    public void SetUp()
    {
        manager = new GameObject("DashboardManager").AddComponent<DashboardManager>();
        topLeft = new GameObject("TopLeft").AddComponent<DashboardSlot>();
        topRight = new GameObject("TopRight").AddComponent<DashboardSlot>();
        bottomLeft = new GameObject("BottomLeft").AddComponent<DashboardSlot>();
        bottomRight = new GameObject("BottomRight").AddComponent<DashboardSlot>();
        followCameraPanel = new GameObject("FollowCameraPanel", typeof(RectTransform)).AddComponent<TestDashboardPanel>();
        telemetryPanel = new GameObject("TelemetryPanel", typeof(RectTransform)).AddComponent<TestDashboardPanel>();

        manager.topLeft = topLeft;
        manager.topRight = topRight;
        manager.bottomLeft = bottomLeft;
        manager.bottomRight = bottomRight;
        manager.fullscreenRoot = new GameObject("FullscreenRoot", typeof(RectTransform)).GetComponent<RectTransform>();
        manager.gridRoot = new GameObject("GridRoot", typeof(RectTransform)).GetComponent<RectTransform>();
        SetPrivateField("availablePanels", new List<DashboardPanelEntry>
        {
            new DashboardPanelEntry
            {
                panelType = DashboardPanelType.FollowCamera,
                prefab = followCameraPanel,
            },
            new DashboardPanelEntry
            {
                panelType = DashboardPanelType.Telemetry,
                prefab = telemetryPanel,
            },
        });
        InvokePrivateMethod("BuildPanelLookup");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(manager.gameObject);
        Object.DestroyImmediate(topLeft.gameObject);
        Object.DestroyImmediate(topRight.gameObject);
        Object.DestroyImmediate(bottomLeft.gameObject);
        Object.DestroyImmediate(bottomRight.gameObject);
        Object.DestroyImmediate(manager.fullscreenRoot.gameObject);
        Object.DestroyImmediate(manager.gridRoot.gameObject);
        Object.DestroyImmediate(followCameraPanel.gameObject);
        Object.DestroyImmediate(telemetryPanel.gameObject);
        SetDashboardManagerInstance(null);
    }

    [Test]
    public void SetPanelBySlotIndex_WhenPanelTypeIsAlreadyOpenInAnotherSlot_LeavesTheCurrentPanelUnchanged()
    {
        manager.SetPanelBySlotIndex(0, (int)DashboardPanelType.FollowCamera);
        manager.SetPanelBySlotIndex(1, (int)DashboardPanelType.Telemetry);
        DashboardPanel existingPanel = topRight.GetCurrentPanel();

        manager.SetPanelBySlotIndex(1, (int)DashboardPanelType.FollowCamera);

        Assert.That(topRight.GetCurrentPanel(), Is.SameAs(existingPanel));
    }

    [Test]
    public void SetPanelBySlotIndex_WhenDuplicatePanelIsRequestedForTheFocusedSlot_LeavesTheSlotFocused()
    {
        manager.SetPanelBySlotIndex(0, (int)DashboardPanelType.FollowCamera);
        manager.SetPanelBySlotIndex(1, (int)DashboardPanelType.Telemetry);
        manager.FocusSlot(topLeft);
        DashboardPanel focusedPanel = topLeft.GetCurrentPanel();

        manager.SetPanelBySlotIndex(0, (int)DashboardPanelType.Telemetry);

        Assert.That(manager.focusedSlot, Is.SameAs(topLeft));
        Assert.That(topLeft.GetCurrentPanel(), Is.SameAs(focusedPanel));
    }

    [Test]
    public void IsAvailable_WhenAnotherSlotHasTheRequestedPanelType_ReturnsFalse()
    {
        bool isAvailable = DashboardPanelAvailability.IsAvailable(
            DashboardPanelType.FollowCamera,
            new[] { DashboardPanelType.FollowCamera });

        Assert.That(isAvailable, Is.False);
    }

    [Test]
    public void IsPointerOver_WhenScreenPointIsInsideThePanel_ReturnsTrue()
    {
        RectTransform panelRect = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panelRect.position = new Vector3(100f, 100f, 0f);
        panelRect.sizeDelta = new Vector2(100f, 100f);

        bool isPointerOver = DashboardPanelPointerHitTest.IsPointerOver(panelRect, new Vector2(100f, 100f));

        Object.DestroyImmediate(panelRect.gameObject);

        Assert.That(isPointerOver, Is.True);
    }

    [Test]
    public void IsPointerOver_WhenPanelIsInactive_ReturnsFalse()
    {
        RectTransform panelRect = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
        panelRect.position = new Vector3(100f, 100f, 0f);
        panelRect.sizeDelta = new Vector2(100f, 100f);
        panelRect.gameObject.SetActive(false);

        bool isPointerOver = DashboardPanelPointerHitTest.IsPointerOver(panelRect, new Vector2(100f, 100f));

        Object.DestroyImmediate(panelRect.gameObject);

        Assert.That(isPointerOver, Is.False);
    }

    private void SetPrivateField(string fieldName, object value)
    {
        typeof(DashboardManager)
            .GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(manager, value);
    }

    private void InvokePrivateMethod(string methodName)
    {
        typeof(DashboardManager)
            .GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(manager, null);
    }

    private static void SetDashboardManagerInstance(DashboardManager instance)
    {
        typeof(DashboardManager)
            .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public)
            .SetValue(null, instance);
    }

    private sealed class TestDashboardPanel : DashboardPanel
    {
        public override string PanelName => "Test";
    }
}
