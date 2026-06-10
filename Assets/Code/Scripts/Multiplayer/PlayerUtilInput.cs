using UnityEngine;
using UnityEngine.InputSystem;
using Oyedoyin.Common;

public class PlayerUtilInput : MonoBehaviour
{

    [SerializeField] private MainMenu _mainMenu;

    public void OnResetVR(InputValue value)
    {
        Debug.Log("ResetVR");
        // TODO - OpenXR Reset viewer pose
    }

    public void OnResetHelicopter()
    {
        Debug.Log("ResetAircraft");
        _mainMenu.PlayerResetHelicopter();
    }
}
