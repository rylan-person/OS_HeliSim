using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingMenuOpener : MonoBehaviour
{
    public GameObject settingMenu;
    public GameObject bottomMenu;

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            settingMenu.SetActive(!settingMenu.activeSelf);
        }
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            bottomMenu.SetActive(!bottomMenu.activeSelf);
        }
    }
}
