using System.Collections;
using System.Collections.Generic;
using Oyedoyin.RotaryWing;
using UnityEngine;

public class FlightDynamicsUIController : MonoBehaviour
{
    public enum CurrentDisplayPage
    {
        FlightDynamics,
        EngineControl,
        None,
    }
    public FlightDynamicsControl flightDynamicsControl;
    public FlightDynamicsControl.FlightDynamicSelect currentSelectedDynamic = FlightDynamicsControl.FlightDynamicSelect.Collective;

    [SerializeField] private TrimDirectionUIController collectiveTrimDirectionUIController;
    [SerializeField] private TrimDirectionUIController rollTrimDirectionUIController;
    [SerializeField] private TrimDirectionUIController pitchTrimDirectionUIController;
    [SerializeField] private TrimDirectionUIController yawTrimDirectionUIController;

    private TrimDirectionUIController[] trimDirectionUIControllers = new TrimDirectionUIController[4];

    // add a listener to flightDynamicValues in FlightDynamicsControl
    public void Start()
    {
        flightDynamicsControl.OnFlightDynamicValuesChanged += OnFlightDynamicValuesChanged;
        flightDynamicsControl.OnCurrentSelectedDynamicChanged += OnCurrentSelectedDynamicChanged;
        trimDirectionUIControllers[0] = collectiveTrimDirectionUIController;
        trimDirectionUIControllers[1] = rollTrimDirectionUIController;
        trimDirectionUIControllers[2] = pitchTrimDirectionUIController;
        trimDirectionUIControllers[3] = yawTrimDirectionUIController;
    }

    [SerializeField] private RotaryComputer rotaryComputer;


    // Event handler method
    private void OnFlightDynamicValuesChanged(double[][] newValues)
    {
        Debug.Log("Flight dynamics values updated!");

        // Example: Print the new values
        for (int i = 0; i < newValues.Length; i++)
        {
            Debug.Log($"Row {i}: {newValues[i][0]}, {newValues[i][1]}");
        }

        // Update the UI
        collectiveTrimDirectionUIController.UpdateTrimDirectionValues(newValues[0]);
        rollTrimDirectionUIController.UpdateTrimDirectionValues(newValues[1]);
        pitchTrimDirectionUIController.UpdateTrimDirectionValues(newValues[2]);
        yawTrimDirectionUIController.UpdateTrimDirectionValues(newValues[3]);
    }

    public void OnCurrentSelectedDynamicChanged(FlightDynamicsControl.FlightDynamicSelect newSelectedDynamic)
    {
        ToggleBackgroundColour();
        currentSelectedDynamic = newSelectedDynamic;
        ToggleBackgroundColour();
    }

    public void ToggleBackgroundColour()
    {
        trimDirectionUIControllers[(int)currentSelectedDynamic].ToggleBackgroundColour();
    }

}
