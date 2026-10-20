using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;

public sealed class MultiplayerConnectionUITests
{
    private readonly List<GameObject> objects = new List<GameObject>();

    [TearDown]
    public void TearDown()
    {
        for (int i = objects.Count - 1; i >= 0; i--)
        {
            Object.DestroyImmediate(objects[i]);
        }
        objects.Clear();
        RoleSelectionUI.SelectedRole = ClientRole.Player;
    }

    [TestCase(" 192.168.001.010 ", "192.168.1.10")]
    [TestCase("127.0.0.1", "127.0.0.1")]
    [TestCase("000.000.000.000", "0.0.0.0")]
    public void AddressUsesFourDecimalOctets(string entered, string expected)
    {
        object[] arguments = { entered, null };
        MethodInfo method = typeof(RoleSelectionUI).GetMethod("TryNormalizeAddress", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "The shared address validator must exist.");
        Assert.That(method.Invoke(null, arguments), Is.True);
        Assert.That(arguments[1], Is.EqualTo(expected));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("127.1")]
    [TestCase("0x7f.0.0.1")]
    [TestCase("256.0.0.1")]
    [TestCase("1.2.3.-1")]
    [TestCase("localhost")]
    [TestCase("1.2.3.4:7777")]
    [TestCase("1.2. 3.4")]
    public void InvalidAddressIsRejected(string entered)
    {
        object[] arguments = { entered, null };
        MethodInfo method = typeof(RoleSelectionUI).GetMethod("TryNormalizeAddress", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null);
        Assert.That(method.Invoke(null, arguments), Is.False);
    }

    [Test]
    public void ClientUsesEditableAddressAndRoleWithConfiguredPort()
    {
        RoleSelectionUI ui = Create<RoleSelectionUI>();
        TMP_InputField input = Create<TMP_InputField>();
        input.text = " 010.020.030.040 ";
        Set(ui, "ipInputField", input);
        Set(ui, "serverPort", (ushort)7888);
        RoleSelectionUI.SelectedRole = ClientRole.Spectator;
        NetworkManager manager = Create<NetworkManager>();
        UnityTransport transport = manager.gameObject.AddComponent<UnityTransport>();
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

        Assert.That(Invoke(ui, "TryConfigureEndpoint", manager, false), Is.True);

        Assert.That(transport.ConnectionData.Address, Is.EqualTo("10.20.30.40"));
        Assert.That(transport.ConnectionData.Port, Is.EqualTo(7888));
        Assert.That(manager.NetworkConfig.ConnectionData, Is.EqualTo(new byte[] { (byte)ClientRole.Spectator }));
        Assert.That(input.text, Is.EqualTo("10.20.30.40"));
    }

    [Test]
    public void HostUsesDefaultPortAndWildcardListenerEvenWithInvalidClientInput()
    {
        RoleSelectionUI ui = Create<RoleSelectionUI>();
        TMP_InputField input = Create<TMP_InputField>();
        input.text = "not a client address";
        Set(ui, "ipInputField", input);
        NetworkManager manager = Create<NetworkManager>();
        UnityTransport transport = manager.gameObject.AddComponent<UnityTransport>();
        manager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };

        Assert.That(Invoke(ui, "TryConfigureEndpoint", manager, true), Is.True);

        Assert.That(transport.ConnectionData.Address, Is.EqualTo("127.0.0.1"));
        Assert.That(transport.ConnectionData.ServerListenAddress, Is.EqualTo("0.0.0.0"));
        Assert.That(transport.ConnectionData.Port, Is.EqualTo(7777));
        Assert.That(input.text, Is.EqualTo("not a client address"));
    }

    [Test]
    public void FailureRestoresControlsPanelAndStatusWithoutChangingTypedAddress()
    {
        RoleSelectionUI ui = Create<RoleSelectionUI>();
        TMP_InputField input = Create<TMP_InputField>();
        Button button = Create<Button>();
        Canvas panel = Create<Canvas>();
        TMP_Text status = Create<TextMeshProUGUI>();
        input.text = "192.168.1.42";
        Set(ui, "ipInputField", input);
        Set(ui, "connectionControls", new Selectable[] { input, button });
        Set(ui, "roleSelectionPanel", panel);
        Set(ui, "connectionStatusText", status);
        Invoke(ui, "SetConnecting", true);
        panel.enabled = false;

        Invoke(ui, "RestoreConnectionControls", "Connection failed. Retry.");

        Assert.That(button.interactable, Is.True);
        Assert.That(input.interactable, Is.True);
        Assert.That(panel.enabled, Is.True);
        Assert.That(status.text, Does.Contain("failed"));
        Assert.That(input.text, Is.EqualTo("192.168.1.42"));
        Assert.That(Invoke(ui, "IsStartBlocked"), Is.False);
    }

    [Test]
    public void PendingAttemptBlocksSecondStartAndRoleChanges()
    {
        RoleSelectionUI ui = Create<RoleSelectionUI>();
        RoleSelectionUI.SelectedRole = ClientRole.Player;
        Invoke(ui, "SetConnecting", true);
        ui.OnClickSpectator();
        Assert.That(Invoke(ui, "IsStartBlocked"), Is.True);
        Assert.That(RoleSelectionUI.SelectedRole, Is.EqualTo(ClientRole.Player));
    }

    [Test]
    public void SavedAddressDoesNotOverwriteExistingInput()
    {
        loadlastip loader = Create<loadlastip>();
        TMP_InputField input = Create<TMP_InputField>();
        input.text = "192.168.1.25";
        Set(loader, "ipInputField", input);
        Invoke(loader, "ApplyInitialAddress", "10.0.0.1");
        Assert.That(input.text, Is.EqualTo("192.168.1.25"));
        input.text = "";
        Invoke(loader, "ApplyInitialAddress", "010.000.000.001");
        Assert.That(input.text, Is.EqualTo("10.0.0.1"));
    }

    [Test]
    public void LegacyMenuDoesNotAddDuplicateToPersistentlyWiredRoleButton()
    {
        RoleSelectionUI ui = Create<RoleSelectionUI>();
        MenuUI menu = Create<MenuUI>();
        Button button = Create<Button>();
        UnityEventTools.AddPersistentListener(button.onClick, ui.StartAsHost);
        Assert.That(Invoke(menu, "HasSharedStartupListener", button, "StartAsHost"), Is.True);
    }

    private T Create<T>() where T : Component
    {
        GameObject gameObject = new GameObject(typeof(T).Name);
        gameObject.SetActive(false);
        objects.Add(gameObject);
        return gameObject.AddComponent<T>();
    }

    private static void Set(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.That(field, Is.Not.Null, name);
        field.SetValue(target, value);
    }

    private static object Invoke(object target, string name, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, name);
        return method.Invoke(target, arguments);
    }
}
