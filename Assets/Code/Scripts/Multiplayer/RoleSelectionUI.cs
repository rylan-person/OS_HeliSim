using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class RoleSelectionUI : MonoBehaviour
{
    public static ClientRole SelectedRole = ClientRole.Player;

    [Tooltip("Default port used when connecting/hosting. Override via inspector if your deployment uses a different one.")]
    [SerializeField] private ushort serverPort = 7777;

    // IP entry field for connecting to a server
    [SerializeField] private TMP_InputField ipInputField;

    [SerializeField] private Canvas roleSelectionPanel;

    public void OnClickPlayer()
    {
        SelectedRole = ClientRole.Player;
        if (CustomConnectionManager.Instance != null)
        {
            CustomConnectionManager.Instance.hostRole = SelectedRole;
        }
        Debug.Log("Player role selected");
    }

    public void OnClickSpectator()
    {
        SelectedRole = ClientRole.Spectator;
        if (CustomConnectionManager.Instance != null)
        {
            CustomConnectionManager.Instance.hostRole = SelectedRole;
        }
        Debug.Log("Spectator role selected");
    }

    public void StartAsClient()
    {
        // 1 byte payload: role
        NetworkManager.Singleton.NetworkConfig.ConnectionData = new byte[]
        {
            (byte)SelectedRole
        };

        var transport = (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;
        transport.ConnectionData.Address = PlayerPrefs.GetString("ServerIP", "127.0.0.1"); // host IP
        transport.ConnectionData.Port = serverPort;
        NetworkManager.Singleton.StartClient();

        if (roleSelectionPanel != null)
        {
            roleSelectionPanel.enabled = false;
        }
    }

    public void StartAsHost()
    {
        Debug.Log($"Starting as host with role: {SelectedRole}");
        // Host doesn't use ConnectionData (it's both server + client),
        // but you can still set it if you want a default.
        if (roleSelectionPanel != null)
        {
            roleSelectionPanel.enabled = false;
        }

        NetworkManager.Singleton.StartHost();
    }
}
