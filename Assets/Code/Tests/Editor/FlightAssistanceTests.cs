using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Oyedoyin.Common;
using Oyedoyin.RotaryWing;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Inactive isolated components: no engine initialization, networking, physics or hardware output.
public sealed class FlightAssistanceTests
{
    private GameObject root;
    private RotaryComputer computer;
    private RotaryController controller;
    private SilantroCore core;
    private AutoTrim trim;
    private MainMenu menu;
    private HelicopterComponents components;

    [SetUp]
    public void SetUp()
    {
        Assert.That(HelicopterComponents.Instance == null, Is.True, "Do not replace an authoring helicopter singleton.");
        root = new GameObject("Flight assistance isolated test");
        root.SetActive(false);
        computer = root.AddComponent<RotaryComputer>();
        controller = root.AddComponent<RotaryController>();
        core = root.AddComponent<SilantroCore>();
        computer.controller = controller;
        controller.m_core = core;
        trim = root.AddComponent<AutoTrim>();
        Set(trim, "_flcs", computer);
        Set(trim, "_core", core);
        SetTrims(10, -10, 9, -9, 18, -9);
        Invoke(trim, "Start");
        components = root.AddComponent<HelicopterComponents>();
        components.autoTrim = trim;
        components.rotaryComputer = computer;
        components.rotaryController = controller;
        Invoke(components, "TryBecomeSingleton");
        menu = root.AddComponent<MainMenu>();
        var sliderObject = new GameObject("Preset", typeof(RectTransform));
        sliderObject.transform.SetParent(root.transform);
        var slider = sliderObject.AddComponent<Slider>();
        slider.maxValue = 5;
        slider.wholeNumbers = true;
        Set(menu, "AssistPresets", slider);
        var labelObject = new GameObject("Preset name", typeof(RectTransform));
        labelObject.transform.SetParent(root.transform);
        Set(menu, "AssistPresetName", labelObject.AddComponent<TextMeshProUGUI>());
    }

    public void TearDown()
    {
        // EditMode never activates these objects, so Unity does not promise OnDestroy.
        // Release only our singleton before destroying the isolated hierarchy.
        if (components != null && root != null && !root.activeSelf) Invoke(components, "OnDestroy");
        if (root != null) Object.DestroyImmediate(root);
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        TearDown();
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator PresetTransitionsDriveRealFixedTicksAndYieldToPilotInput()
    {
        Assert.That(SessionState.GetString(MultiplayerVerificationRunner.AuthoringSetupKey, ""), Is.Not.Empty,
            "Run through MultiplayerVerificationRunner to preserve authoring scenes.");
        TearDown();
        yield return new EnterPlayMode();
        // The Test Runner can rerun SetUp when resuming after domain reload.
        if (root == null) SetUp();
        // Only AutoTrim runs its lifecycle. Engine/vehicle initialization and menu session logic stay disabled.
        computer.enabled = false;
        controller.enabled = false;
        core.enabled = false;
        menu.enabled = false;
        root.SetActive(true);
        yield return null;
        Assert.That(menu.TrySetAssistancePreset(2), Is.True);
        core.q = 0.5;
        core.p = -0.5;
        core.r = -0.5;
        Assert.That(trim.isActiveAndEnabled, Is.True);
        Assert.That(trim._autoTrim, Is.True);
        Assert.That(computer.enabled, Is.False);
        Assert.That(controller.enabled, Is.False);
        Assert.That(core.enabled, Is.False);
        Assert.That(Time.timeScale, Is.GreaterThan(0), "Physics progression requires a running isolated simulation.");
        float startedAt = Time.fixedTime;
        float deadline = Time.realtimeSinceStartup + 3;
        while (computer.LongitudinalUpper == 10 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Time.fixedTime, Is.GreaterThan(startedAt), "Unity must deliver real fixed ticks before trim can respond.");
        Assert.That(computer.LongitudinalUpper, Is.GreaterThan(10));
        Assert.That(computer.LateralUpper, Is.GreaterThan(9));
        Assert.That(computer.PedalUpper, Is.GreaterThan(18));
        computer.b_pitchInput = computer.b_rollInput = computer.b_yawInput = 0.5;
        double pitch = computer.LongitudinalUpper;
        double roll = computer.LateralUpper;
        double yaw = computer.PedalUpper;
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();
        Assert.That(computer.LongitudinalUpper, Is.EqualTo(pitch));
        Assert.That(computer.LateralUpper, Is.EqualTo(roll));
        Assert.That(computer.PedalUpper, Is.EqualTo(yaw));
        Assert.That(menu.TrySetAssistancePreset(1), Is.True);
        computer.b_pitchInput = computer.b_rollInput = computer.b_yawInput = 0;
        yield return new WaitForFixedUpdate();
        AssertTrims(10, -10, 9, -9, 18, -9);
        Assert.That(computer.m_mode, Is.EqualTo(Computer.Mode.Manual));
        Assert.That(trim._autoTrim, Is.False);
    }

    [TestCase(0)]
    [TestCase(1)]
    public void ManualPresetClearsPreviouslyComputedStabilityCorrection(int manualPreset)
    {
        Assert.That(menu.TrySetAssistancePreset(5), Is.True);
        core.θ = 12;
        core.ф = -8;
        core.q = 0.3;
        core.p = -0.2;
        core.r = 0.1;
        Invoke(computer, "ComputeSAS");
        Assert.That(computer.Bɪc_afcs, Is.Not.EqualTo(0));
        Assert.That(computer.Aɪc_afcs, Is.Not.EqualTo(0));
        Assert.That(computer.θt_afcs, Is.Not.EqualTo(0));
        Assert.That(menu.TrySetAssistancePreset(manualPreset), Is.True);
        Assert.That(computer.Bɪc_afcs, Is.Zero);
        Assert.That(computer.Aɪc_afcs, Is.Zero);
        Assert.That(computer.θt_afcs, Is.Zero);
    }

    [TestCase(0, false, Computer.Mode.Manual, RotaryController.TorqueMode.Conventional)]
    [TestCase(1, false, Computer.Mode.Manual, RotaryController.TorqueMode.Corrected)]
    [TestCase(2, true, Computer.Mode.Augmented, RotaryController.TorqueMode.Corrected)]
    [TestCase(3, false, Computer.Mode.Augmented, RotaryController.TorqueMode.Corrected)]
    [TestCase(4, false, Computer.Mode.Augmented, RotaryController.TorqueMode.Corrected)]
    [TestCase(5, false, Computer.Mode.Augmented, RotaryController.TorqueMode.Corrected)]
    public void ProductionPresetSelectsComputerTorqueAndTrim(int preset, bool autoTrim, Computer.Mode mode, RotaryController.TorqueMode torque)
    {
        Assert.That(menu.TrySetAssistancePreset(preset), Is.True);
        Assert.That(trim._autoTrim, Is.EqualTo(autoTrim));
        Assert.That(computer.m_mode, Is.EqualTo(mode));
        Assert.That(controller.m_torqueMode, Is.EqualTo(torque));
        if (mode == Computer.Mode.Augmented)
        {
            assistValues values = menu.AssistValuesList[preset];
            Assert.That(computer.Gbq, Is.EqualTo(0.12 * values.rateLimitValue).Within(1e-10));
            Assert.That(computer.Gap, Is.EqualTo(-0.12 * values.rateLimitValue).Within(1e-10));
            Assert.That(computer.Gbθ, Is.EqualTo(0.15 * values.stabilizationValue).Within(1e-10));
            Assert.That(computer.Gaф, Is.EqualTo(-0.175 * values.stabilizationValue).Within(1e-10));
        }
    }

    [TestCase(2)]
    [TestCase(3)]
    [TestCase(5)]
    public void PresetFeedsProductionStabilityCorrection(int preset)
    {
        Assert.That(menu.TrySetAssistancePreset(preset), Is.True);
        core.θ = 12;
        core.ф = -8;
        core.q = 0.3;
        core.p = -0.2;
        core.r = 0.1;
        Invoke(computer, "ComputeSAS");
        assistValues values = menu.AssistValuesList[preset];
        Assert.That(computer.Bɪc_afcs, Is.EqualTo(0.15 * values.stabilizationValue * 12 * Mathf.Deg2Rad + 0.12 * values.rateLimitValue * 0.3).Within(1e-9));
        Assert.That(computer.Aɪc_afcs, Is.EqualTo(-0.175 * values.stabilizationValue * -8 * Mathf.Deg2Rad - 0.12 * values.rateLimitValue * -0.2).Within(1e-9));
        Assert.That(computer.θt_afcs, Is.EqualTo(0.33 * values.stabilizationValue * 0.1).Within(1e-9));
    }

    [TestCase(0.1, 0.01)]
    [TestCase(0.3, 0.02)]
    [TestCase(0.5, 0.03)]
    public void BodyRatesDriveConfiguredTrimRatesAndAxisDirections(double rate, double step)
    {
        trim.AutoTrimOn();
        core.q = rate;
        core.p = -rate;
        core.r = -rate;
        Invoke(trim, "FixedUpdate");
        Assert.That(computer.LongitudinalUpper, Is.EqualTo(10 + step).Within(1e-10));
        Assert.That(computer.LateralUpper, Is.EqualTo(9 + step).Within(1e-10));
        Assert.That(computer.PedalUpper, Is.EqualTo(18 + step / 2).Within(1e-10));
    }

    [TestCase((double)0.1f)]
    [TestCase((double)-0.1f)]
    [TestCase(1)]
    [TestCase(-1)]
    public void ActivePilotInputPreventsTrimOnItsAxes(double input)
    {
        trim.AutoTrimOn();
        computer.b_pitchInput = computer.b_rollInput = computer.b_yawInput = input;
        core.q = core.p = core.r = 1;
        Invoke(trim, "FixedUpdate");
        AssertTrims(10, -10, 9, -9, 18, -9);
    }

    [Test]
    public void PilotPitchInputDoesNotBlockUncommandedRollAndYawTrimming()
    {
        trim.AutoTrimOn();
        computer.b_pitchInput = 1;
        core.q = 1;
        core.p = -1;
        core.r = -1;
        Invoke(trim, "FixedUpdate");
        Assert.That(computer.LongitudinalUpper, Is.EqualTo(10));
        Assert.That(computer.LateralUpper, Is.GreaterThan(9));
        Assert.That(computer.PedalUpper, Is.GreaterThan(18));
    }

    [Test]
    public void DisabledAndRateDeadbandDoNotChangeTrim()
    {
        core.q = core.p = core.r = 1;
        Invoke(trim, "FixedUpdate");
        AssertTrims(10, -10, 9, -9, 18, -9);
        trim.AutoTrimOn();
        core.q = core.p = core.r = 0.0005;
        Invoke(trim, "FixedUpdate");
        AssertTrims(10, -10, 9, -9, 18, -9);
    }

    [Test]
    public void TurningTrimOffRestoresCapturedBaselineAndStopsFutureAdjustments()
    {
        trim.AutoTrimOn();
        core.q = 1;
        core.p = -1;
        core.r = -1;
        Invoke(trim, "FixedUpdate");
        trim.AutoTrimOff();
        Assert.That(trim._autoTrim, Is.False);
        Invoke(trim, "FixedUpdate");
        AssertTrims(10, -10, 9, -9, 18, -9);
    }

    [TestCase(true, true)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(false, false)]
    public void CyclicLimitTransferPreservesRequestedMeanTrimStep(bool pitch, bool increase)
    {
        SetTrims(10.295, -10.295, 10.295, -10.295, 18, -9);
        trim.AutoTrimOn();
        if (pitch) core.q = increase ? 0.5 : -0.5;
        else core.p = increase ? -0.5 : 0.5;
        double before = pitch ? computer.LongitudinalUpper + computer.LongitudinalLower : computer.LateralUpper + computer.LateralLower;
        Invoke(trim, "FixedUpdate");
        double upper = pitch ? computer.LongitudinalUpper : computer.LateralUpper;
        double lower = pitch ? computer.LongitudinalLower : computer.LateralLower;
        Assert.That((upper + lower - before) / 2, Is.EqualTo(increase ? 0.015 : -0.015).Within(1e-10));
        Assert.That(increase ? upper : lower, Is.EqualTo(increase ? 10.3 : -10.3).Within(1e-10));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void PedalTrimStopsExactlyAtConfiguredBoundaries(bool increase)
    {
        computer.PedalUpper = increase ? 19.499 : 14.001;
        computer.PedalLower = increase ? -5.001 : -10.499;
        trim.AutoTrimOn();
        core.r = increase ? -0.5 : 0.5;
        Invoke(trim, "FixedUpdate");
        Assert.That(computer.PedalUpper, Is.EqualTo(increase ? 19.5 : 14).Within(1e-10));
        Assert.That(computer.PedalLower, Is.EqualTo(increase ? -5 : -10.5).Within(1e-10));
    }

    private void SetTrims(double longitudinalUpper, double longitudinalLower, double lateralUpper, double lateralLower, double pedalUpper, double pedalLower)
    {
        computer.LongitudinalUpper = longitudinalUpper;
        computer.LongitudinalLower = longitudinalLower;
        computer.LateralUpper = lateralUpper;
        computer.LateralLower = lateralLower;
        computer.PedalUpper = pedalUpper;
        computer.PedalLower = pedalLower;
    }

    private void AssertTrims(double longitudinalUpper, double longitudinalLower, double lateralUpper, double lateralLower, double pedalUpper, double pedalLower)
    {
        Assert.That(computer.LongitudinalUpper, Is.EqualTo(longitudinalUpper));
        Assert.That(computer.LongitudinalLower, Is.EqualTo(longitudinalLower));
        Assert.That(computer.LateralUpper, Is.EqualTo(lateralUpper));
        Assert.That(computer.LateralLower, Is.EqualTo(lateralLower));
        Assert.That(computer.PedalUpper, Is.EqualTo(pedalUpper));
        Assert.That(computer.PedalLower, Is.EqualTo(pedalLower));
    }

    private static void Set(object target, string field, object value)
    {
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }

    private static void Invoke(object target, string method)
    {
        target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
