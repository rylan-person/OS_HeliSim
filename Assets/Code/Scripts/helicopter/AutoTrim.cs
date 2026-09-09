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

    [Header("Trim Rate Thresholds")]
    [Tooltip("Body rate (rad/s) above which auto-trim uses the high trim-change rate (changeValueHigh).")]
    [SerializeField] private float highRateThreshold = 0.4f;
    [Tooltip("Body rate (rad/s) above which auto-trim uses the medium trim-change rate (changeValueMed), below highRateThreshold.")]
    [SerializeField] private float mediumRateThreshold = 0.25f;
    [Tooltip("Deadband (rad/s) within which auto-trim stops nudging trim for that axis.")]
    [SerializeField] private float rateDeadband = 0.001f;
    [Tooltip("Input deadband auto-trim requires on the pilot's raw stick/pedal input before it will adjust trim for that axis (prevents fighting active pilot input).")]
    [SerializeField] private float pilotInputDeadband = 0.1f;

    [Header("Cyclic Trim Limits")]
    [Tooltip("Magnitude (deg) the longitudinal/lateral upper or lower trim actuator can reach on its own before the opposite actuator absorbs the remainder.")]
    [SerializeField] private double cyclicTrimLimit = 10.3;

    [Header("Pedal Trim Limits")]
    [Tooltip("Pedal-upper trim (deg) auto-trim will not increase past while correcting positive yaw rate.")]
    [SerializeField] private double pedalUpperCeiling = 19.5;
    [Tooltip("Pedal-lower trim (deg) auto-trim will not increase past while correcting positive yaw rate.")]
    [SerializeField] private double pedalLowerCeiling = -5.0;
    [Tooltip("Pedal-upper trim (deg) auto-trim will not decrease below while correcting negative yaw rate.")]
    [SerializeField] private double pedalUpperFloor = 14.0;
    [Tooltip("Pedal-lower trim (deg) auto-trim will not decrease below while correcting negative yaw rate.")]
    [SerializeField] private double pedalLowerFloor = -10.5;

    [Header("Base Trims")]
    [SerializeField] double[] _longBaseTrim = new double[2];
    [SerializeField] double[] _latBaseTrim = new double[2];
    [SerializeField] double[] _pedalBaseTrim = new double[2];

    public bool _autoTrim = false;

    [Header("Trim Source")]
    [Tooltip("When enabled, auto-trim reads rotor moments (m_mainRotor/m_tailRotor) as its error signal instead of body rates (SilantroCore.p/q/r).")]
    [SerializeField] private bool useMoments = false;

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
        if (_flcs == null)
        {
            Debug.LogError($"{nameof(AutoTrim)}: RotaryComputer (_flcs) reference is not assigned; auto-trim will be disabled.", this);
            enabled = false;
            return;
        }

        _longBaseTrim[0] = _flcs.LongitudinalUpper;
        _longBaseTrim[1] = _flcs.LongitudinalLower;

        _latBaseTrim[0] = _flcs.LateralUpper;
        _latBaseTrim[1] = _flcs.LateralLower;

        _pedalBaseTrim[0] = _flcs.PedalUpper;
        _pedalBaseTrim[1] = _flcs.PedalLower;

    }

    private void FixedUpdate()
    {
        if (_flcs == null || _core == null) return;

        if (useMoments)
        {
            if (_mainRotor == null || _tailRotor == null) return;
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
            if (_flcs.b_pitchInput < pilotInputDeadband && _flcs.b_pitchInput > -pilotInputDeadband)
            {
                UpdatePitchTrim();
            }
            if (_flcs.b_rollInput < pilotInputDeadband && _flcs.b_rollInput > -pilotInputDeadband)
            {
                UpdateRollTrim();
            }
            if (_flcs.b_yawInput < pilotInputDeadband && _flcs.b_yawInput > -pilotInputDeadband)
            {
                UpdateYawTrim();
            }
        }
    }

    // Update Pitch Trim
    public void UpdatePitchTrim()
    {
        double changeValue = changeValueLow;
        // if the magnitude of the pitch rate is greater than the medium/high rate thresholds, trim faster
        if (_pitchDiff > highRateThreshold || _pitchDiff < -highRateThreshold)
        {
            changeValue = changeValueHigh;
        }
        else if (_pitchDiff > mediumRateThreshold || _pitchDiff < -mediumRateThreshold)
        {
            changeValue = changeValueMed;
        }

        if (_pitchDiff > rateDeadband)
        {
            IncreaseLongTrim(changeValue);
        }
        else if (_pitchDiff < -rateDeadband)
        {
            DecreaseLongTrim(changeValue);
        }
    }

    private void IncreaseLongTrim(double amount)
    {
        if (_flcs.LongitudinalUpper >= cyclicTrimLimit)
        {
            _flcs.LongitudinalLower += amount * 2;
            return;
        }
        if (_flcs.LongitudinalUpper + amount * 2 < cyclicTrimLimit)
        {
            _flcs.LongitudinalUpper += amount * 2;
            return;
        }
        if (_flcs.LongitudinalUpper < cyclicTrimLimit)
        {
            _flcs.LongitudinalLower += (amount - (cyclicTrimLimit - _flcs.LongitudinalUpper));
            _flcs.LongitudinalUpper = cyclicTrimLimit;
            return;
        }
    }

    private void DecreaseLongTrim(double amount)
    {
        if (_flcs.LongitudinalLower <= -cyclicTrimLimit)
        {
            _flcs.LongitudinalUpper -= amount * 2;
            return;
        }
        if (_flcs.LongitudinalLower - amount * 2 > -cyclicTrimLimit)
        {
            _flcs.LongitudinalLower -= amount * 2;
            return;
        }
        if (_flcs.LongitudinalLower > -cyclicTrimLimit)
        {
            _flcs.LongitudinalUpper -= (amount - (-cyclicTrimLimit - _flcs.LongitudinalLower));
            _flcs.LongitudinalLower = -cyclicTrimLimit;
            return;
        }
    }



    private void UpdateRollTrim()
    {
        double changeValue = changeValueLow;
        // if the magnitude of the roll rate is greater than the medium/high rate thresholds, trim faster
        if (_rollDiff > highRateThreshold || _rollDiff < -highRateThreshold)
        {
            changeValue = changeValueHigh;
        }
        else if (_rollDiff > mediumRateThreshold || _rollDiff < -mediumRateThreshold)
        {
            changeValue = changeValueMed;
        }

        if (_rollDiff < -rateDeadband)
        {
            IncreaseLateralTrim(changeValue);
        }
        else if (_rollDiff > rateDeadband)
        {
            DecreaseLateralTrim(changeValue);
        }
    }

    private void IncreaseLateralTrim(double amount)
    {
        if (_flcs.LateralUpper >= cyclicTrimLimit)
        {
            _flcs.LateralLower += amount*2;
            return;
        }
        if (_flcs.LateralUpper + amount*2 < cyclicTrimLimit)
        {
            _flcs.LateralUpper += amount*2;
            return;
        } 
        if (_flcs.LateralUpper < cyclicTrimLimit)
        {
            _flcs.LateralLower += (amount - (cyclicTrimLimit - _flcs.LateralUpper));
            _flcs.LateralUpper = cyclicTrimLimit;
            return;
        }
    }

    private void DecreaseLateralTrim(double amount)
    {
        if (_flcs.LateralLower <= -cyclicTrimLimit)
        {
            _flcs.LateralUpper -= amount*2;
            return;
        }
        if (_flcs.LateralLower - amount*2 > -cyclicTrimLimit)
        {
            _flcs.LateralLower -= amount*2;
            return;
        }
        if (_flcs.LateralLower > -cyclicTrimLimit)
        {
            _flcs.LateralUpper -= (amount - (-cyclicTrimLimit - _flcs.LateralLower));
            _flcs.LateralLower = -cyclicTrimLimit;
            return;
        }
    }

    private void UpdateYawTrim()
    {
        double changeValue = changeValueLow;
        // if the magnitude of the yaw rate is greater than the medium/high rate thresholds, trim faster
        if (_yawDiff > highRateThreshold || _yawDiff < -highRateThreshold)
        {
            changeValue = changeValueHigh;
        } else if (_yawDiff > mediumRateThreshold || _yawDiff < -mediumRateThreshold)
        {
            changeValue = changeValueMed;
        }

        if (_yawDiff < -rateDeadband)
        {
            if (_flcs.PedalUpper < pedalUpperCeiling)
            {
                _flcs.PedalUpper += changeValue;
            }
            if (_flcs.PedalLower < pedalLowerCeiling)
            {
                _flcs.PedalLower += changeValue;
            }
        }
        else if (_yawDiff > rateDeadband)
        {
            if (_flcs.PedalUpper > pedalUpperFloor)
            {
                _flcs.PedalUpper -= changeValue;
            }
            if (_flcs.PedalLower > pedalLowerFloor)
            {
                _flcs.PedalLower -= changeValue;
            }
        }
    }

}
