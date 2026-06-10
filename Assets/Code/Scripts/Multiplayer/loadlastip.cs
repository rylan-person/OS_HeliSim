using TMPro;
using UnityEngine;

public class loadlastip : MonoBehaviour
{
    // textmeshpro-text
    public TMP_Text ipText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (System.IO.File.Exists("ServerIP.txt"))
        {
            string serverIP = System.IO.File.ReadAllText("ServerIP.txt");
            PlayerPrefs.SetString("ServerIP", serverIP);

            if (ipText != null)
            {
                ipText.text = serverIP;
            }
        }
    }
}
