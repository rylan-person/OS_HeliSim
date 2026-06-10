using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;
using System.Collections;

public class RoleSelectionUI : MonoBehaviour
{
    public static ClientRole SelectedRole = ClientRole.Player;

    // IP entry field for connecting to a server
    [SerializeField] private TMP_InputField ipInputField;

    [SerializeField] private Canvas roleSelectionPanel;

    public void OnClickPlayer()
    {
        SelectedRole = ClientRole.Player;
        CustomConnectionManager.Instance.hostRole = SelectedRole;
        Debug.Log("Player role selected");
    }

    public void OnClickSpectator()
    {
        SelectedRole = ClientRole.Spectator;
        CustomConnectionManager.Instance.hostRole = SelectedRole;
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
        Debug.Log($"playerpref ServerIP: {PlayerPrefs.GetString("ServerIP")}");
        transport.ConnectionData.Port = 7777;
        NetworkManager.Singleton.StartClient();

        roleSelectionPanel.enabled = false;

    }

    public void StartAsHost()
    {
        Debug.Log($"Starting as host with role: {SelectedRole}");
        // Host doesn't use ConnectionData (it’s both server + client),
        // but you can still set it if you want a default.
        roleSelectionPanel.enabled = false;
        
        NetworkManager.Singleton.StartHost();
    }
    //[ContextMenu("Run Start")]
    public void Start()
    {
        StartCoroutine(RunStartWhenReady());
    }
    
    private System.Collections.IEnumerator RunStartWhenReady()
    {
        // wait 2 seconds to ensure any other startup logic (like CustomConnectionManager's Start) has run and set PlayerPrefs
        yield return new WaitForSeconds(0.5f);
        
        
        if (PlayerPrefs.GetInt("Player", 1) == 1)
        {
            Debug.Log("Player role selected in PlayerPrefs");
            OnClickPlayer();
        }
        else
        {
            Debug.Log("Spectator role selected in PlayerPrefs");
            OnClickSpectator();
        }

        if (PlayerPrefs.GetInt("Host", 1) == 1)
        {
            Debug.Log("Host role selected in PlayerPrefs");
            StartAsHost();
        }
        else
        {
            Debug.Log("Join role selected in PlayerPrefs");
            StartAsClient();
        }
    }
    
}
