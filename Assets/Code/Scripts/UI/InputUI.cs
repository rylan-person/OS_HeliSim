using Oyedoyin.Common;
using Oyedoyin.RotaryWing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class InputUI : MonoBehaviour
{
    [SerializeField] private RotaryController _rotaryController;
    private SilantroInput _silantroInput;

    [SerializeField] private GameObject _joystickDotUI;
    [SerializeField] private GameObject _yawDotUI;
    [SerializeField] private GameObject _collectiveDotUI;
    private float _inputBounds = 65f;

    private void Awake()
    {
        _silantroInput = _rotaryController.m_input;
    }

    private void Update()
    {
        _joystickDotUI.transform.localPosition = new Vector3(_silantroInput.m_rollInput * _inputBounds, _silantroInput.m_pitchInput * -1f * _inputBounds, 0);
        _yawDotUI.transform.localPosition = new Vector3(_silantroInput.m_yawInput * _inputBounds, 0, 0);
        _collectiveDotUI.transform.localPosition = new Vector3(0, -_inputBounds + (_silantroInput._collectiveInput * 2f * _inputBounds), 0);
    }
}
