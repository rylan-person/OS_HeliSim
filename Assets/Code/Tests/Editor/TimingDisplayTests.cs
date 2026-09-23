using NUnit.Framework;
using UnityEngine;

public sealed class TimingDisplayTests
{
    private GameObject root;
    private Timing timing;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Timing Test");
        timing = root.AddComponent<Timing>();
        timing.currentLapTimeText = CreateText("Current");
        timing.lastLapTimeText = CreateText("Last");
        timing.lastLapTimeDifferenceText = CreateText("Last Difference");
        timing.bestLapTimeText = CreateText("Best");
        timing.optLapTimeText = CreateText("Optimal");
        timing.sectorTimesText = new[] { CreateText("Sector") };
        timing.sectorTimesDifferenceText = new[] { CreateText("Sector Difference") };
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
    }

    [Test]
    public void CurrentLapTime_RoundsAcrossTheMinuteBoundary()
    {
        timing.updateCurrentLapTime(59.9996f);

        Assert.That(timing.currentLapTimeText.text, Is.EqualTo("1:00.000"));
    }

    [Test]
    public void FasterSector_DifferenceHasOneMinusSign()
    {
        timing.updateSectorTimeDifference(0, 9f, 0f, 10f, 10f);

        Assert.That(timing.sectorTimesDifferenceText[0].text, Is.EqualTo("-1.00"));
    }

    [Test]
    public void NoSelectedPilot_ClearsAllDisplayedTimes()
    {
        timing.currentLapTimeText.text = "12.000";
        timing.lastLapTimeText.text = "13.000";
        timing.bestLapTimeText.text = "11.000";
        timing.optLapTimeText.text = "10.000";
        timing.sectorTimesText[0].text = "2.000";

        timing.Render(null);

        Assert.That(timing.currentLapTimeText.text, Is.EqualTo("-.---"));
        Assert.That(timing.lastLapTimeText.text, Is.EqualTo("-.---"));
        Assert.That(timing.bestLapTimeText.text, Is.EqualTo("-.---"));
        Assert.That(timing.optLapTimeText.text, Is.EqualTo("-.---"));
        Assert.That(timing.sectorTimesText[0].text, Is.EqualTo("-.---"));
    }

    private TMPro.TextMeshProUGUI CreateText(string name)
    {
        TMPro.TextMeshProUGUI text = new GameObject(name, typeof(RectTransform)).AddComponent<TMPro.TextMeshProUGUI>();
        text.transform.SetParent(root.transform);
        return text;
    }
}
