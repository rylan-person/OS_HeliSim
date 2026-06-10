using System.Collections;
using System.Collections.Generic;
using Oyedoyin.RotaryWing;
using UnityEngine;

public class ButtonTestScript : MonoBehaviour
{
    [SerializeField] private RotaryController rotaryController;

    public void Update()
    {
        // on h pressed
        if (Input.GetKeyDown(KeyCode.C))
        {
            rotaryController.TurnOnEngines();
        }
        if (Input.GetKeyDown(KeyCode.V))
        {
            rotaryController.TurnOffEngines();
        }
    }
}
