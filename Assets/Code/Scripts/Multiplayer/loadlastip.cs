using System;
using System.IO;
using TMPro;
using UnityEngine;

public class loadlastip : MonoBehaviour
{
    // Retained for serialized compatibility with older prefabs.
    public TMP_Text ipText;
    [SerializeField] private TMP_InputField ipInputField;

    private void Start()
    {
        string initialAddress = PlayerPrefs.GetString("ServerIP", "127.0.0.1");
        try
        {
            if (File.Exists("ServerIP.txt") && RoleSelectionUI.TryNormalizeAddress(File.ReadAllText("ServerIP.txt"), out string fileAddress))
            {
                initialAddress = fileAddress;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{nameof(loadlastip)}: failed to read ServerIP.txt: {exception.Message}");
        }
        ApplyInitialAddress(initialAddress);
    }

    private void ApplyInitialAddress(string address)
    {
        if (!RoleSelectionUI.TryNormalizeAddress(address, out string normalized))
        {
            normalized = "127.0.0.1";
        }
        TMP_InputField input = ipInputField;
        if (input == null && ipText != null)
        {
            input = ipText.GetComponentInParent<TMP_InputField>();
        }
        if (input != null)
        {
            if (string.IsNullOrWhiteSpace(input.text))
            {
                input.SetTextWithoutNotify(normalized);
            }
        }
        else if (ipText != null && string.IsNullOrWhiteSpace(ipText.text))
        {
            ipText.text = normalized;
        }
    }
}
