using System;
using System.Globalization;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.UI;

public class RoleSelectionUI : MonoBehaviour
{
    public static ClientRole SelectedRole = ClientRole.Player;

    [Tooltip("Port shared by host and clients.")]
    [SerializeField] private ushort serverPort = 7777;
    [SerializeField] private TMP_InputField ipInputField;
    [SerializeField] private Canvas roleSelectionPanel;
    [SerializeField] private TMP_Text connectionStatusText;
    [SerializeField] private Selectable[] connectionControls;

    private NetworkManager subscribedManager;
    private bool connecting;
    private string failureMessage;
    private string fallbackAddress = "127.0.0.1";
    private string hostListenAddress = "0.0.0.0";
    private bool menuOwnsPresentation;
    private bool panelVisible = true;
    public bool IsConnecting => connecting;
    public string StatusMessage { get; private set; } = string.Empty;

    // Menu launches keep their loading/retry UI authoritative; direct scene entry
    // retains the legacy connection panel and its normal retry behaviour.
    public void SetMenuOwnsPresentation(bool owned)
    {
        menuOwnsPresentation = owned;
        SetPanelVisible(panelVisible);
    }

    private void SetPanelVisible(bool visible)
    {
        panelVisible = visible;
        if (roleSelectionPanel != null)
        {
            roleSelectionPanel.enabled = visible && !menuOwnsPresentation;
        }
    }

    // The front end configures the same startup path used by the scene's legacy UI.
    public bool ConfigureEndpoint(string address, ushort port, bool localHostOnly)
    {
        if (IsStartBlocked() || port == 0 || !TryNormalizeAddress(address, out string normalized))
            return false;
        serverPort = port;
        hostListenAddress = localHostOnly ? "127.0.0.1" : "0.0.0.0";
        SetServerAddress(normalized);
        return true;
    }

    public void OnClickPlayer()
    {
        SelectRole(ClientRole.Player);
    }

    public void OnClickSpectator()
    {
        SelectRole(ClientRole.Spectator);
    }

    private void SelectRole(ClientRole role)
    {
        if (IsStartBlocked())
        {
            return;
        }
        SelectedRole = role;
        if (CustomConnectionManager.Instance != null)
        {
            CustomConnectionManager.Instance.hostRole = role;
        }
    }

    public void StartAsClient()
    {
        StartConnection(false);
    }

    public void StartAsHost()
    {
        StartConnection(true);
    }

    // Compatibility for older menu/input bindings. An editable field remains authoritative.
    public void SetServerAddress(string address)
    {
        fallbackAddress = address;
        if (ipInputField != null && !IsStartBlocked())
        {
            ipInputField.text = address;
        }
    }

    private bool IsStartBlocked()
    {
        NetworkManager manager = NetworkManager.Singleton;
        return connecting || (manager != null && (manager.IsListening || manager.ShutdownInProgress));
    }

    private void StartConnection(bool host)
    {
        if (IsStartBlocked())
        {
            return;
        }

        NetworkManager manager = NetworkManager.Singleton;
        if (manager == null || manager.NetworkConfig == null)
        {
            RestoreConnectionControls("Network manager is unavailable. Please retry.");
            return;
        }
        if (!TryConfigureEndpoint(manager, host))
        {
            return;
        }
        if (host && CustomConnectionManager.Instance != null &&
            !CustomConnectionManager.Instance.TryValidateSpawnConfiguration(SelectedRole, out string reason))
        {
            RestoreConnectionControls(reason);
            return;
        }

        Subscribe(manager);
        failureMessage = null;
        if (CustomConnectionManager.Instance != null)
        {
            CustomConnectionManager.Instance.hostRole = SelectedRole;
        }
        SetConnecting(true);
        UnityTransport transport = (UnityTransport)manager.NetworkConfig.NetworkTransport;
        SetStatus(host ? $"Starting host on {hostListenAddress}:{serverPort}..." : $"Connecting to {transport.ConnectionData.Address}:{serverPort}...");
        try
        {
            bool started = host ? manager.StartHost() : manager.StartClient();
            if (!started)
            {
                RestoreConnectionControls("Network startup failed. Please retry.");
            }
            else if (!host)
            {
                PlayerPrefs.SetString("ServerIP", transport.ConnectionData.Address);
            }
        }
        catch (Exception exception)
        {
            failureMessage = $"Network startup failed: {exception.Message}";
            if (manager.IsListening && !manager.ShutdownInProgress)
            {
                manager.Shutdown();
            }
            RestoreConnectionControls(failureMessage);
        }
    }

    private bool TryConfigureEndpoint(NetworkManager manager, bool host)
    {
        if (manager.IsListening || manager.ShutdownInProgress)
        {
            return false;
        }
        UnityTransport transport = manager.NetworkConfig.NetworkTransport as UnityTransport;
        if (transport == null || serverPort == 0)
        {
            RestoreConnectionControls("A Unity Transport and a nonzero server port are required.");
            return false;
        }

        string address = "127.0.0.1";
        if (!host)
        {
            string entered = ipInputField != null ? ipInputField.text : fallbackAddress;
            if (!TryNormalizeAddress(entered, out address))
            {
                RestoreConnectionControls("Enter a valid IPv4 address (for example 192.168.1.10).");
                return false;
            }
            if (ipInputField != null)
            {
                ipInputField.SetTextWithoutNotify(address);
            }
        }
        transport.SetConnectionData(address, serverPort, host ? hostListenAddress : null);
        manager.NetworkConfig.ConnectionData = new[] { (byte)SelectedRole };
        return true;
    }

    public static bool TryNormalizeAddress(string entered, out string address)
    {
        address = null;
        if (string.IsNullOrWhiteSpace(entered))
        {
            return false;
        }
        string[] octets = entered.Trim().Split('.');
        if (octets.Length != 4)
        {
            return false;
        }
        for (int i = 0; i < octets.Length; i++)
        {
            if (octets[i].Length == 0 || octets[i].Length > 3)
            {
                return false;
            }
            for (int j = 0; j < octets[i].Length; j++)
            {
                if (octets[i][j] < '0' || octets[i][j] > '9')
                {
                    return false;
                }
            }
            if (!byte.TryParse(octets[i], NumberStyles.None, CultureInfo.InvariantCulture, out byte value))
            {
                return false;
            }
            octets[i] = value.ToString(CultureInfo.InvariantCulture);
        }
        address = string.Join(".", octets);
        return true;
    }

    private void Subscribe(NetworkManager manager)
    {
        if (subscribedManager == manager)
        {
            return;
        }
        Unsubscribe();
        subscribedManager = manager;
        manager.OnClientConnectedCallback += OnClientConnected;
        manager.OnClientDisconnectCallback += OnClientDisconnected;
        manager.OnTransportFailure += OnTransportFailure;
        manager.OnClientStopped += OnNetworkStopped;
        manager.OnServerStopped += OnNetworkStopped;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (subscribedManager == null || clientId != subscribedManager.LocalClientId)
        {
            return;
        }
        connecting = false;
        SetStatus("Connected.");
        SetPanelVisible(false);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (subscribedManager == null || clientId != subscribedManager.LocalClientId)
        {
            return;
        }
        string reason = subscribedManager.DisconnectReason;
        failureMessage = string.IsNullOrWhiteSpace(reason) ? "Disconnected. Please retry." : $"Disconnected: {reason} You can try again.";
        RestoreConnectionControls(failureMessage);
    }

    private void OnTransportFailure()
    {
        failureMessage = "Transport failed. Check the address and network, then retry.";
        RestoreConnectionControls(failureMessage);
    }

    private void OnNetworkStopped(bool wasHost)
    {
        RestoreConnectionControls(failureMessage ?? "Network stopped. You can connect again.");
    }

    private void SetConnecting(bool value)
    {
        connecting = value;
        if (connectionControls != null)
        {
            foreach (Selectable control in connectionControls)
            {
                if (control != null)
                {
                    control.interactable = !value;
                }
            }
        }
        if (ipInputField != null)
        {
            ipInputField.interactable = !value;
        }
    }

    private void RestoreConnectionControls(string message)
    {
        SetConnecting(false);
        SetPanelVisible(true);
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        StatusMessage = message ?? string.Empty;
        if (connectionStatusText != null)
        {
            connectionStatusText.text = message;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedManager == null)
        {
            return;
        }
        subscribedManager.OnClientConnectedCallback -= OnClientConnected;
        subscribedManager.OnClientDisconnectCallback -= OnClientDisconnected;
        subscribedManager.OnTransportFailure -= OnTransportFailure;
        subscribedManager.OnClientStopped -= OnNetworkStopped;
        subscribedManager.OnServerStopped -= OnNetworkStopped;
        subscribedManager = null;
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }
}
