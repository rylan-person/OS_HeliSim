using System;
using HeliSim.InFlightMenu;
using NUnit.Framework;
using UnityEngine;

public sealed class DisplaySettingsModelTests
{
    private sealed class Runtime : IDisplaySettingsRuntime
    {
        public DisplaySettingsValue Current;
        public bool SupportsUiScale { get; set; } = true;
        public int QualityLevelCount { get; set; } = 3;
        public bool FailApply;
        public int ApplyCount;

        public DisplaySettingsValue Capture() => Current;
        public bool SupportsResolution(int width, int height) => width == 1920 && height == 1080 || width == 1280 && height == 720;
        public bool TryApply(DisplaySettingsValue value, out string error)
        {
            ApplyCount++;
            error = FailApply ? "Display unavailable" : string.Empty;
            if (FailApply) return false;
            Current = value;
            return true;
        }
    }

    private sealed class Store : IDisplaySettingsStore
    {
        public DisplaySettingsValue Saved;
        public bool HasSaved;
        public bool FailSave;

        public bool TryLoad(out DisplaySettingsValue value) { value = Saved; return HasSaved; }
        public void Save(DisplaySettingsValue value)
        {
            if (FailSave) throw new InvalidOperationException("Storage unavailable");
            Saved = value;
            HasSaved = true;
        }
    }

    private static DisplaySettingsValue Defaults => new DisplaySettingsValue
    {
        WindowMode = FullScreenMode.Windowed,
        Width = 1920,
        Height = 1080,
        VSync = true,
        FrameRateCap = -1,
        QualityLevel = 1,
        UiScale = 1f,
    };

    [Test]
    public void StageAndRevert_LeaveRuntimeUnchanged()
    {
        Runtime runtime = new Runtime { Current = Defaults };
        DisplaySettingsModel model = new DisplaySettingsModel(runtime, new Store());
        DisplaySettingsValue draft = Defaults;
        draft.FrameRateCap = 60;

        model.Stage(draft);
        Assert.That(model.IsDirty, Is.True);
        Assert.That(runtime.Current.FrameRateCap, Is.EqualTo(-1));

        model.Revert();
        Assert.That(model.IsDirty, Is.False);
        Assert.That(model.Draft.FrameRateCap, Is.EqualTo(-1));
    }

    [TestCase(29)]
    [TestCase(241)]
    public void InvalidFrameCap_RejectsEntireDraft(int cap)
    {
        Runtime runtime = new Runtime { Current = Defaults };
        Store store = new Store();
        DisplaySettingsModel model = new DisplaySettingsModel(runtime, store);
        DisplaySettingsValue draft = Defaults;
        draft.FrameRateCap = cap;
        draft.QualityLevel = 2;
        model.Stage(draft);

        Assert.That(model.TryApply(), Is.False);
        Assert.That(runtime.ApplyCount, Is.Zero);
        Assert.That(store.HasSaved, Is.False);
        Assert.That(runtime.Current.QualityLevel, Is.EqualTo(1));
    }

    [Test]
    public void UnavailableUiScale_RejectsChangedScale()
    {
        Runtime runtime = new Runtime { Current = Defaults, SupportsUiScale = false };
        DisplaySettingsModel model = new DisplaySettingsModel(runtime, new Store());
        DisplaySettingsValue draft = Defaults;
        draft.UiScale = 1.25f;
        model.Stage(draft);

        Assert.That(model.TryApply(), Is.False);
        Assert.That(model.Error, Does.Contain("CanvasScaler"));
    }

    [Test]
    public void SaveFailure_RollsBackRuntimeAndKeepsDraft()
    {
        Runtime runtime = new Runtime { Current = Defaults };
        DisplaySettingsModel model = new DisplaySettingsModel(runtime, new Store { FailSave = true });
        DisplaySettingsValue draft = Defaults;
        draft.QualityLevel = 2;
        model.Stage(draft);

        Assert.That(model.TryApply(), Is.False);
        Assert.That(runtime.Current.QualityLevel, Is.EqualTo(1));
        Assert.That(model.IsDirty, Is.True);
    }

    [Test]
    public void RuntimeFailure_DoesNotPersistOrAdvanceSnapshot()
    {
        Runtime runtime = new Runtime { Current = Defaults, FailApply = true };
        Store store = new Store();
        DisplaySettingsModel model = new DisplaySettingsModel(runtime, store);
        DisplaySettingsValue draft = Defaults;
        draft.QualityLevel = 2;
        model.Stage(draft);

        Assert.That(model.TryApply(), Is.False);
        Assert.That(model.Error, Is.EqualTo("Display unavailable"));
        Assert.That(store.HasSaved, Is.False);
        Assert.That(model.Applied.QualityLevel, Is.EqualTo(1));
    }

    [Test]
    public void NoQualityOptions_RejectsDraftWithoutMutation()
    {
        Runtime runtime = new Runtime { Current = Defaults, QualityLevelCount = 0 };
        DisplaySettingsModel model = new DisplaySettingsModel(runtime, new Store());

        Assert.That(model.TryApply(), Is.False);
        Assert.That(runtime.ApplyCount, Is.Zero);
    }

    [Test]
    public void RestoreSaved_AppliesValidSavedSettingsAtStartup()
    {
        Runtime runtime = new Runtime { Current = Defaults };
        DisplaySettingsValue saved = Defaults;
        saved.Width = 1280;
        saved.Height = 720;
        saved.UiScale = 1.25f;
        DisplaySettingsModel model = new DisplaySettingsModel(runtime, new Store { HasSaved = true, Saved = saved });

        Assert.That(model.RestoreSaved(), Is.True);
        Assert.That(runtime.Current.Width, Is.EqualTo(1280));
        Assert.That(model.Applied.UiScale, Is.EqualTo(1.25f));
        Assert.That(model.IsDirty, Is.False);
    }

    [Test]
    public void RestoreSaved_WithoutCanvasScalerKeepsCurrentScale()
    {
        Runtime runtime = new Runtime { Current = Defaults, SupportsUiScale = false };
        DisplaySettingsValue saved = Defaults;
        saved.UiScale = 1.25f;
        saved.FrameRateCap = 60;
        DisplaySettingsModel model = new DisplaySettingsModel(runtime, new Store { HasSaved = true, Saved = saved });

        Assert.That(model.RestoreSaved(), Is.True);
        Assert.That(runtime.Current.UiScale, Is.EqualTo(1f));
        Assert.That(runtime.Current.FrameRateCap, Is.EqualTo(60));
    }

    [Test]
    public void RestoreSaved_UnavailableResolutionPreservesCurrent()
    {
        Runtime runtime = new Runtime { Current = Defaults };
        DisplaySettingsValue saved = Defaults;
        saved.Width = 2560;
        DisplaySettingsModel model = new DisplaySettingsModel(runtime, new Store { HasSaved = true, Saved = saved });

        Assert.That(model.RestoreSaved(), Is.False);
        Assert.That(runtime.ApplyCount, Is.Zero);
        Assert.That(model.Applied.Width, Is.EqualTo(1920));
    }
}
