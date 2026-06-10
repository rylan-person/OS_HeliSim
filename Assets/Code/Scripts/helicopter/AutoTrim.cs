using Oyedoyin.Common;
using Oyedoyin.RotaryWing;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

public class AutoTrim : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] SilantroRotor _mainRotor;
    [SerializeField] SilantroRotor _tailRotor;
    [SerializeField] RotaryComputer _flcs;
    [SerializeField] SilantroCore _core;

    [Header("Rates")]
    [SerializeField] float _pitchDiff = 0.0f;
    [SerializeField] float _rollDiff = 0.0f;
    [SerializeField] float _yawDiff = 0.0f;

    [Header("Current Trims")]
    // Show Current Trims as read only
    [SerializeField] double _longTrim = 0.0;
    [SerializeField] double _latTrim = 0.0;
    [SerializeField] double _pedalTrim = 0.0;

    [Header("Auto Trim Change Amounts")]
    [SerializeField] double changeValueLow = 0.005;
    [SerializeField] double changeValueMed = 0.01;
    [SerializeField] double changeValueHigh = 0.015;

    [Header("Base Trims")]
    [SerializeField] double[] _longBaseTrim = new double[2];
    [SerializeField] double[] _latBaseTrim = new double[2];
    [SerializeField] double[] _pedalBaseTrim = new double[2];

    public bool _autoTrim = false;

    // Use Moments to AutoTrim
    private bool useMoments = false;

    public void AutoTrimOn()
    {
        _autoTrim = true;
    }

    public void AutoTrimOff()
    {
        // Reset the trims to the base trims
        _flcs.LongitudinalUpper = _longBaseTrim[0];
        _flcs.LongitudinalLower = _longBaseTrim[1];

        _flcs.LateralUpper = _latBaseTrim[0];
        _flcs.LateralLower = _latBaseTrim[1];

        _flcs.PedalUpper = _pedalBaseTrim[0];
        _flcs.PedalLower = _pedalBaseTrim[1];

        _autoTrim = false;
    }

    // Start is called before the first frame update
    void Start()
    {
        _longBaseTrim[0] = _flcs.LongitudinalUpper;
        _longBaseTrim[1] = _flcs.LongitudinalLower;

        _latBaseTrim[0] = _flcs.LateralUpper;
        _latBaseTrim[1] = _flcs.LateralLower;

        _pedalBaseTrim[0] = _flcs.PedalUpper;
        _pedalBaseTrim[1] = _flcs.PedalLower;

    }

    private void FixedUpdate()
    {
        if (useMoments)
        {
            _pitchDiff = _mainRotor.m_moment.x;
            _rollDiff = _mainRotor.m_moment.z;
            _yawDiff = _tailRotor.m_moment.y;
        } 
        else 
        {
            _pitchDiff = (float)_core.q;
            _rollDiff = (float)_core.p;
            _yawDiff = (float)_core.r;
        }

        _longTrim = (_flcs.LongitudinalUpper + _flcs.LongitudinalLower) / 2;
        _latTrim = (_flcs.LateralUpper + _flcs.LateralLower) / 2;
        _pedalTrim = (_flcs.PedalUpper + _flcs.PedalLower) / 2;


        if (_autoTrim)
        {
            if (_flcs.b_pitchInput < 0.1 && _flcs.b_pitchInput > -0.1)
            {
                UpdatePitchTrim();
            }
            if (_flcs.b_rollInput < 0.1 && _flcs.b_rollInput > -0.1)
            {
                UpdateRollTrim();
            }
            if (_flcs.b_yawInput < 0.1 && _flcs.b_yawInput > -0.1)
            {
                UpdateYawTrim();
            }
        }
    }

    // Update Pitch Trim
    public void UpdatePitchTrim()
    {
        double changeValue = changeValueLow;
        // if the magnitude of the yaw difference is greater than 0.01
        if (_pitchDiff > 0.4 || _pitchDiff < -0.4)
        {
            changeValue = changeValueHigh;
        }
        else if (_pitchDiff > 0.25 || _pitchDiff < -0.25)
        {
            changeValue = changeValueMed;
        }

        if (_pitchDiff > 0.001)
        {
            IncreaseLongTrim(changeValue);
        }
        else if (_pitchDiff < -0.001)
        {
            DecreaseLongTrim(changeValue);
        }
    }

    private void IncreaseLongTrim(double amount)
    {
        if (_flcs.LongitudinalUpper >= 10.3)
        {
            _flcs.LongitudinalLower += amount * 2;
            return;
        }
        if (_flcs.LongitudinalUpper + amount * 2 < 10.3)
        {
            _flcs.LongitudinalUpper += amount * 2;
            return;
        }
        if (_flcs.LongitudinalUpper < 10.3)
        {
            _flcs.LongitudinalLower += (amount - (10.3 - _flcs.LongitudinalUpper));
            _flcs.LongitudinalUpper = 10.3;
            return;
        }
    }

    private void DecreaseLongTrim(double amount)
    {
        if (_flcs.LongitudinalLower <= -10.3)
        {
            _flcs.LongitudinalUpper -= amount * 2;
            return;
        }
        if (_flcs.LongitudinalLower - amount * 2 > -10.3)
        {
            _flcs.LongitudinalLower -= amount * 2;
            return;
        }
        if (_flcs.LongitudinalLower > -10.3)
        {
            _flcs.LongitudinalUpper -= (amount - (-10.3 - _flcs.LongitudinalLower));
            _flcs.LongitudinalLower = -10.3;
            return;
        }
    }



    private void UpdateRollTrim()
    {
        double changeValue = changeValueLow;
        // if the magnitude of the yaw difference is greater than 0.01
        if (_rollDiff > 0.4 || _rollDiff < -0.4)
        {
            changeValue = changeValueHigh;
        }
        else if (_rollDiff > 0.25 || _rollDiff < -0.25)
        {
            changeValue = changeValueMed;
        }

        if (_rollDiff < -0.001)
        {
            IncreaseLateralTrim(changeValue);
        }
        else if (_rollDiff > 0.001)
        {
            DecreaseLateralTrim(changeValue);
        }
    }

    private void IncreaseLateralTrim(double amount)
    {
        if (_flcs.LateralUpper >= 10.3)
        {
            _flcs.LateralLower += amount*2;
            return;
        }
        if (_flcs.LateralUpper + amount*2 < 10.3)
        {
            _flcs.LateralUpper += amount*2;
            return;
        } 
        if (_flcs.LateralUpper < 10.3)
        {
            _flcs.LateralLower += (amount - (10.3 - _flcs.LateralUpper));
            _flcs.LateralUpper = 10.3;
            return;
        }
    }

    private void DecreaseLateralTrim(double amount)
    {
        if (_flcs.LateralLower <= -10.3)
        {
            _flcs.LateralUpper -= amount*2;
            return;
        }
        if (_flcs.LateralLower - amount*2 > -10.3)
        {
            _flcs.LateralLower -= amount*2;
            return;
        }
        if (_flcs.LateralLower > -10.3)
        {
            _flcs.LateralUpper -= (amount - (-10.3 - _flcs.LateralLower));
            _flcs.LateralLower = -10.3;
            return;
        }
    }

    private void UpdateYawTrim()
    {
        double changeValue = changeValueLow;
        // if the magnitude of the yaw difference is greater than 0.01
        if (_yawDiff > 0.4 || _yawDiff < -0.4)
        {
            changeValue = changeValueHigh;
        } else if (_yawDiff > 0.25 || _yawDiff < -0.25)
        {
            changeValue = changeValueMed;
        }

        if (_yawDiff < -0.001)
        {
            if (_flcs.PedalUpper < 19.5)
            {
                _flcs.PedalUpper += changeValue;
            }
            if (_flcs.PedalLower < -5)
            {
                _flcs.PedalLower += changeValue;
            }
        }
        else if (_yawDiff > 0.001)
        {
            if (_flcs.PedalUpper > 14)
            {
                _flcs.PedalUpper -= changeValue;
            }
            if (_flcs.PedalLower > -10.5)
            {
                _flcs.PedalLower -= changeValue;
            }
        }
    }

    private void IncreaseYawTrim(double amount)
    {
        if (_flcs.PedalUpper >= 17.8)
        {
            _flcs.PedalLower += amount * 2;
            return;
        }
        if (_flcs.PedalUpper + amount * 2 < 17.8)
        {
            _flcs.PedalUpper += amount * 2;
            return;
        }
        if (_flcs.PedalUpper < 17.8)
        {
            _flcs.PedalLower += (amount - (17.8 - _flcs.PedalUpper));
            _flcs.PedalUpper = 17.8;
            return;
        }
    }

    private void DecreaseYawTrim(double amount)
    {
        if (_flcs.PedalLower <= -9)
        {
            _flcs.PedalUpper -= amount * 2;
            return;
        }
        if (_flcs.PedalLower - amount * 2 > -9)
        {
            _flcs.PedalLower -= amount * 2;
            return;
        }
        if (_flcs.PedalLower > -9)
        {
            _flcs.PedalUpper -= (amount - (-9 - _flcs.PedalLower));
            _flcs.PedalLower = -9;
            return;
        }
    }

}
