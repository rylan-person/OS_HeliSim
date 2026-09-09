using TMPro;
using UnityEngine;

public class loadlastip : MonoBehaviour
{
    // textmeshpro-text
    public TMP_Text ipText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        try
        {
            if (System.IO.File.Exists("ServerIP.txt"))
            {
                string serverIP = System.IO.File.ReadAllText("ServerIP.txt").Trim();
                PlayerPrefs.SetString("ServerIP", serverIP);

                if (ipText != null)
                {
                    ipText.text = serverIP;
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"{nameof(loadlastip)}: failed to read ServerIP.txt: {ex.Message}");
        }
    }
}
