using System;
using System.Collections;
using System.Collections.Generic;
using Oyedoyin.RotaryWing;
using UnityEngine;

public class FlightDynamicsControl : MonoBehaviour
{
    [SerializeField] private RotaryComputer rotaryComputer;

    // current flight dynamic enum
    public enum FlightDynamicSelect
    {
        Collective,
        Roll,
        Pitch,
        Yaw
    }

    public event Action<double[][]> OnFlightDynamicValuesChanged;

    // 4x2 float array to store flight dynamic values
    private double[][] flightDynamicValues = new double[4][]
    {
        new double[2] { 0, 0 },
        new double[2] { 0, 0 },
        new double[2] { 0, 0 },
        new double[2] { 0, 0 }
    };

    public bool IsFlightDynamicMenuActive = true;

    public event Action<FlightDynamicSelect> OnCurrentSelectedDynamicChanged;
    public FlightDynamicSelect currentSelectedDynamic = FlightDynamicSelect.Collective;

    public void Start()
    {
        // Initialize flight dynamic values
        InitializeFlightDynamicValues();
    }

    public void InitializeFlightDynamicValues()
    {
        // Initialize flight dynamic values
        flightDynamicValues[0][0] = rotaryComputer.collectiveLower;
        flightDynamicValues[0][1] = rotaryComputer.collectiveUpper;
        flightDynamicValues[1][0] = rotaryComputer.LateralLower;
        flightDynamicValues[1][1] = rotaryComputer.LateralUpper;
        flightDynamicValues[2][0] = rotaryComputer.LongitudinalLower;
        flightDynamicValues[2][1] = rotaryComputer.LongitudinalUpper;
        flightDynamicValues[3][0] = rotaryComputer.PedalLower;
        flightDynamicValues[3][1] = rotaryComputer.PedalUpper;

        OnFlightDynamicValuesChanged?.Invoke(flightDynamicValues);
    }

    #region Flight Dynamic Value Manipulation
    public void OnIncreaseFlightDynamic()
    {
        Debug.Log("Increase flight dynamic");
        if (!IsFlightDynamicMenuActive) return;

        // Increase flight dynamic value
        flightDynamicValues[(int)currentSelectedDynamic][0] += 0.5;
        flightDynamicValues[(int)currentSelectedDynamic][1] += 0.5;
        ApplyFlightDynamics();
    }

    public void OnDecreaseFlightDynamic()
    {
        if (!IsFlightDynamicMenuActive) return;
        
        // Decrease flight dynamic value
        flightDynamicValues[(int)currentSelectedDynamic][0] -= 0.5;
        flightDynamicValues[(int)currentSelectedDynamic][1] -= 0.5;
        ApplyFlightDynamics();
    }

    public void OnWidenFlightDynamic()
    {
        if (!IsFlightDynamicMenuActive) return;
        
        // Widen flight dynamic value
        flightDynamicValues[(int)currentSelectedDynamic][0] += 0.5;
        flightDynamicValues[(int)currentSelectedDynamic][1] -= 0.5;
        ApplyFlightDynamics();
    }

    public void OnNarrowFlightDynamic()
    {
        if (!IsFlightDynamicMenuActive) return;
        
        // Narrow flight dynamic value
        flightDynamicValues[(int)currentSelectedDynamic][0] -= 0.5;
        flightDynamicValues[(int)currentSelectedDynamic][1] += 0.5;
        ApplyFlightDynamics();
    }
    #endregion

    public void ApplyFlightDynamics()
    {
        // Apply flight dynamics
        rotaryComputer.collectiveLower = flightDynamicValues[0][0];
        rotaryComputer.collectiveUpper = flightDynamicValues[0][1];
        rotaryComputer.LateralLower = flightDynamicValues[1][0];
        rotaryComputer.LateralUpper = flightDynamicValues[1][1];
        rotaryComputer.LongitudinalLower = flightDynamicValues[2][0];
        rotaryComputer.LongitudinalUpper = flightDynamicValues[2][1];
        rotaryComputer.PedalLower = flightDynamicValues[3][0];
        rotaryComputer.PedalUpper = flightDynamicValues[3][1];

        OnFlightDynamicValuesChanged?.Invoke(flightDynamicValues);
    }


    #region Menu Controls
    public void ToggleFlightDynamicMenu()
    {
        // Toggle flight dynamic menu
        IsFlightDynamicMenuActive = !IsFlightDynamicMenuActive;
    }

    public void OnCycleFlightDynamic()
    {
        if (!IsFlightDynamicMenuActive) return;
        
        // Increment current flight dynamic enum
        currentSelectedDynamic = (FlightDynamicSelect)(((int)currentSelectedDynamic + 1) % 4);
        OnCurrentSelectedDynamicChanged?.Invoke(currentSelectedDynamic);
        
    }
    #endregion

}
