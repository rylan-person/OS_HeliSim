using System;
using NUnit.Framework;
using HeliSim.FrontEnd;
using UnityEngine;

public sealed class MenuSessionOptionsTests
{
    private bool hadPreference;
    private string originalPreference;

    [SetUp]
    public void SetUp()
    {
        hadPreference = PlayerPrefs.HasKey(MenuSessionOptionsStore.PreferenceKey);
        originalPreference = PlayerPrefs.GetString(MenuSessionOptionsStore.PreferenceKey);
        PlayerPrefs.DeleteKey(MenuSessionOptionsStore.PreferenceKey);
    }

    [TearDown]
    public void TearDown()
    {
        if (hadPreference)
        {
            PlayerPrefs.SetString(MenuSessionOptionsStore.PreferenceKey, originalPreference);
        }
        else
        {
            PlayerPrefs.DeleteKey(MenuSessionOptionsStore.PreferenceKey);
        }
        PlayerPrefs.Save();
    }

    [Test]
    public void SessionOptionsContractExistsBeforeMenuCanLaunch()
    {
        Type type = typeof(RoleSelectionUI).Assembly.GetType("HeliSim.FrontEnd.MenuSessionOptions");
        Assert.That(type, Is.Not.Null, "Menu launch options must exist before a session can be configured.");
    }

    [Test]
    public void DefaultSessionCanLaunchOnlyWhenGameplaySceneIsAvailable()
    {
        var options = new MenuSessionOptions();
        Assert.That(options.TryValidate(path => path == "Assets/Level/Scenes/Multiplayer.unity", out string error), Is.True, error);
        Assert.That(options.TryValidate(path => false, out error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("Multiplayer")]
    [TestCase("Assets/Level/Scenes/HeliTesting.unity")]
    public void UnsupportedScenesAreRejectedEvenIfCallerReportsAvailable(string scenePath)
    {
        var options = new MenuSessionOptions { ScenePath = scenePath };
        Assert.That(options.TryValidate(path => true, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [Test]
    public void MissingSceneAvailabilityCheckDoesNotAllowLaunch()
    {
        Assert.That(new MenuSessionOptions().TryValidate(null, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [TestCase("127.1")]
    [TestCase("localhost")]
    [TestCase("256.0.0.1")]
    [TestCase("1.2.3.4:7777")]
    [TestCase("0x7f.0.0.1")]
    [TestCase(null)]
    public void NetworkSessionRequiresStrictIpv4(string address)
    {
        var options = new MenuSessionOptions { Mode = MenuLaunchMode.Join, Address = address };
        Assert.That(options.TryValidate(path => true, out string error), Is.False);
        Assert.That(error, Is.Not.Empty);
    }

    [TestCase(MenuLaunchMode.Host)]
    [TestCase(MenuLaunchMode.Join)]
    public void NetworkSessionNormalizesIpv4WithoutChangingSpectatorRole(MenuLaunchMode mode)
    {
        var options = new MenuSessionOptions { Mode = mode, Address = " 192.168.001.010 ", Role = ClientRole.Spectator };
        Assert.That(options.TryValidate(path => true, out string error), Is.True, error);
        Assert.That(options.Address, Is.EqualTo("192.168.1.10"));
        Assert.That(options.Role, Is.EqualTo(ClientRole.Spectator));
    }

    [Test]
    public void SoloForcesPlayerRoleAndLoopbackWithoutRequiringSavedRemoteAddress()
    {
        var options = new MenuSessionOptions { Role = ClientRole.Spectator, Address = "invalid" };
        Assert.That(options.TryValidate(path => true, out string error), Is.True, error);
        Assert.That(options.Role, Is.EqualTo(ClientRole.Player));
        Assert.That(options.Address, Is.EqualTo("127.0.0.1"));
    }

    [TestCase(-1)]
    [TestCase(3)]
    public void UndefinedLaunchModesAreRejected(int value)
    {
        var options = new MenuSessionOptions { Mode = (MenuLaunchMode)value };
        Assert.That(options.TryValidate(path => true, out _), Is.False);
    }

    [Test]
    public void UndefinedRoleIsRejectedIncludingSolo()
    {
        var options = new MenuSessionOptions { Role = (ClientRole)255 };
        Assert.That(options.TryValidate(path => true, out _), Is.False);
    }

    [Test]
    public void ZeroPortIsRejected()
    {
        Assert.That(new MenuSessionOptions { Port = 0 }.TryValidate(path => true, out _), Is.False);
    }

    [TestCase(-2, false)]
    [TestCase(-1, true)]
    [TestCase(0, true)]
    [TestCase(4, true)]
    [TestCase(5, true)]
    [TestCase(6, false)]
    public void AssistanceAllowsExistingDefaultOrSupportedPreset(int preset, bool expected)
    {
        Assert.That(new MenuSessionOptions { AssistancePreset = preset }.TryValidate(path => true, out _), Is.EqualTo(expected));
    }

    [Test]
    public void CloneKeepsDraftChangesSeparateFromAcceptedOptions()
    {
        var original = new MenuSessionOptions { Mode = MenuLaunchMode.Host, Role = ClientRole.Spectator, Address = "10.0.0.2", Port = 8888, RecordFlightData = true, AssistancePreset = 3 };
        MenuSessionOptions clone = original.Clone();
        Assert.That(JsonUtility.ToJson(clone), Is.EqualTo(JsonUtility.ToJson(original)));
        clone.Address = "10.0.0.3";
        Assert.That(original.Address, Is.EqualTo("10.0.0.2"));
    }

    [Test]
    public void StoreRoundTripsOptionsWithoutDependingOnBuildSceneAvailabilityOrMutatingCaller()
    {
        var options = new MenuSessionOptions { Mode = MenuLaunchMode.Join, Role = ClientRole.Spectator, Address = " 010.020.030.040 ", Port = 8999, RecordFlightData = true, AssistancePreset = 2 };
        var store = new MenuSessionOptionsStore();
        store.Save(options);
        MenuSessionOptions loaded = store.Load();
        Assert.That(loaded.Mode, Is.EqualTo(MenuLaunchMode.Join));
        Assert.That(loaded.Role, Is.EqualTo(ClientRole.Spectator));
        Assert.That(loaded.Address, Is.EqualTo("10.20.30.40"));
        Assert.That(loaded.Port, Is.EqualTo(8999));
        Assert.That(loaded.RecordFlightData, Is.True);
        Assert.That(loaded.AssistancePreset, Is.EqualTo(2));
        Assert.That(loaded.ScenePath, Is.EqualTo("Assets/Level/Scenes/Multiplayer.unity"));
        Assert.That(options.Address, Is.EqualTo(" 010.020.030.040 "));
        loaded.Mode = MenuLaunchMode.Solo;
        Assert.That(store.Load().Mode, Is.EqualTo(MenuLaunchMode.Join));
    }

    [Test]
    public void RejectedSaveLeavesPreviouslyStoredSessionUntouched()
    {
        var store = new MenuSessionOptionsStore();
        store.Save(new MenuSessionOptions { Mode = MenuLaunchMode.Host });
        string saved = PlayerPrefs.GetString(MenuSessionOptionsStore.PreferenceKey);
        Assert.Throws<ArgumentException>(() => store.Save(new MenuSessionOptions { Port = 0 }));
        Assert.Throws<ArgumentNullException>(() => store.Save(null));
        Assert.That(PlayerPrefs.GetString(MenuSessionOptionsStore.PreferenceKey), Is.EqualTo(saved));
    }

    [TestCase("not json")]
    [TestCase("{}")]
    [TestCase("null")]
    [TestCase("{\"Version\":99,\"Options\":{}}")]
    [TestCase("{\"Version\":1,\"Options\":null}")]
    [TestCase("{\"Version\":1,\"Options\":{\"Mode\":99}}")]
    [TestCase("{\"Version\":1,\"Options\":{\"ScenePath\":\"Assets/Level/Scenes/HeliTesting.unity\"}}")]
    public void CorruptIncompatibleOrInvalidSavedSessionsFallBackWithoutOverwritingEvidence(string saved)
    {
        PlayerPrefs.SetString(MenuSessionOptionsStore.PreferenceKey, saved);
        MenuSessionOptions loaded = new MenuSessionOptionsStore().Load();
        Assert.That(loaded.Mode, Is.EqualTo(MenuLaunchMode.Solo));
        Assert.That(loaded.Role, Is.EqualTo(ClientRole.Player));
        Assert.That(loaded.Port, Is.EqualTo(7777));
        Assert.That(loaded.AssistancePreset, Is.EqualTo(-1));
        Assert.That(loaded.RecordFlightData, Is.False);
        Assert.That(PlayerPrefs.GetString(MenuSessionOptionsStore.PreferenceKey), Is.EqualTo(saved));
    }

    [Test]
    public void AbsentPreferenceReturnsUsableDefaults()
    {
        MenuSessionOptions loaded = new MenuSessionOptionsStore().Load();
        Assert.That(loaded.TryValidate(path => true, out string error), Is.True, error);
        Assert.That(PlayerPrefs.HasKey(MenuSessionOptionsStore.PreferenceKey), Is.False);
    }

    [Test]
    public void HardwareSummaryDoesNotClaimVerifiedNovaConnection()
    {
        string summary = MenuHardwareStatus.BuildSummary();
        Assert.That(summary, Does.Contain("physical connection unverified"));
        Assert.That(summary, Does.Contain("no motion output"));
        Assert.That(summary, Does.Contain("XR"));
        Assert.That(summary, Does.Contain("Input"));
    }
}
