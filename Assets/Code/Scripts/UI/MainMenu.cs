using Oyedoyin.RotaryWing;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public struct assistValues
{
    public string name;
    public bool autoTrimEnabled;
    public float stabilizationValue;
    public float rateLimitValue;
    public float mainRotorTorqueValue;
    public float tailRotorTorqueValue;
}

public class MainMenu : MonoBehaviour
{
    public List<assistValues> AssistValuesList = new List<assistValues>(){
        new assistValues{
            name = "Manual Torque",
            autoTrimEnabled = false, 
            stabilizationValue = 0, 
            rateLimitValue = 0, 
            mainRotorTorqueValue = 1, 
            tailRotorTorqueValue = 1
        },
        new assistValues{
            name = "Manual",
            autoTrimEnabled = false,
            stabilizationValue = 0,
            rateLimitValue = 0,
            mainRotorTorqueValue = 0,
            tailRotorTorqueValue = 0
        },
        new assistValues{
            name = "Low",
            autoTrimEnabled = true,
            stabilizationValue = 0,
            rateLimitValue = 0.35f,
            mainRotorTorqueValue = 0,
            tailRotorTorqueValue = 0
        },
        new assistValues{
            name = "Medium",
            autoTrimEnabled = false,
            stabilizationValue = 0.1f,
            rateLimitValue = 0.45f,
            mainRotorTorqueValue = 0,
            tailRotorTorqueValue = 0
        },
        new assistValues{
            name = "High",
            autoTrimEnabled = false,
            stabilizationValue = 0.20f,
            rateLimitValue = 0.50f,
            mainRotorTorqueValue = 0,
            tailRotorTorqueValue = 0
        },
        new assistValues{
            name = "Very High",
            autoTrimEnabled = false,
            stabilizationValue = 0.25f,
            rateLimitValue = 0.60f,
            mainRotorTorqueValue = 0,
            tailRotorTorqueValue = 0
        }
    };

    [Header("UI Elements")]
    [SerializeField] private GameObject unlockCollectivePopup;
    [SerializeField] private GameObject OfflineMap; 
    [SerializeField] private GameObject OnlineMap;

    [SerializeField] private GameObject CinematicCams;
    [SerializeField] private GameObject ThirdPersonCam;

    // NEW VARIABLES
    [SerializeField] private Toggle CinemaCamToggle;
    [SerializeField] private Toggle OfflineMapToggle;
    [SerializeField] private Slider AssistPresets;
    [SerializeField] private TMPro.TextMeshProUGUI AssistPresetName;

    public bool isBallSim = true;

    public bool isTimePaused = false;
    public float timeLimitSeconds = 180;
    private float timeRemainingSeconds = 180;

    // Link to a ui TMP Text object to display the time remaining
    public TMPro.TextMeshProUGUI timeRemainingText;
    public TMPro.TextMeshProUGUI timeRemainingTextMenu;
    private Color defaultHelicopterUIColor = new(0, 1, 0.0156f, 1);

    public bool cinematicCams = true;
    public float rollAsisstValue;
    public float stabilizationAsisstValue;

    private bool helicopterLinkInitialized = false;
    private bool timeTrialLinkInitialized = false;
    private bool cinemaCamLinkInitialized = false;

    [SerializeField] private double collectiveUpperSaveValue;

    [SerializeField] private WaypointManager waypointManager;

    // Open Menu on escape press
    private void Update()
    {
        if (!helicopterLinkInitialized)
        {
            if (HelicopterComponents.Instance != null)
            {
                helicopterLinkInitialized = true;
                collectiveUpperSaveValue = HelicopterComponents.Instance.rotaryComputer.collectiveUpper;
            }
        }
        // on r reset scene
        if (Input.GetKeyDown(KeyCode.R))
        {
            NewSession();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (Time.timeScale == 0)
            {
                Time.timeScale = 1;
            }
        }
        if (!isTimePaused)
        {
            // Update the time remaining
            timeRemainingSeconds -= Time.deltaTime;
            if (timeRemainingText != null)
                timeRemainingText.text = timeRemainingSeconds.ToString("F0") + " Seconds";
            timeRemainingTextMenu.text = timeRemainingSeconds.ToString("F0");
            HelicopterComponents.Instance.timeRemainingTextInternal.text = timeRemainingSeconds.ToString("F0") + " Seconds";
            if (timeRemainingSeconds <= 30)
            {
                HelicopterComponents.Instance.timeRemainingTextInternal.color = Color.red;
            }
            else
            {
                HelicopterComponents.Instance.timeRemainingTextInternal.color = defaultHelicopterUIColor;
            }
        }
    }

    private void Start()
    {
        if (HelicopterComponents.Instance != null)
        {
            collectiveUpperSaveValue = HelicopterComponents.Instance.rotaryComputer.collectiveUpper;

            helicopterLinkInitialized = true;
        }

        if (CinematicCams != null) { cinemaCamLinkInitialized = true; }

        timeRemainingSeconds = timeLimitSeconds;

        PauseTime();

        LockCollective();

        OfflineMap.SetActive(false);
        OnlineMap.SetActive(true);
    }



    #region Helicopter Controls

    public void AssistPresetUpdate(Single sin)
    {
        AssistPresetName.text = AssistValuesList[(int)AssistPresets.value].name;

        if (!AssistValuesList[(int)AssistPresets.value].autoTrimEnabled)
        {
            HelicopterComponents.Instance.autoTrim.AutoTrimOff();
        }
        else
        {
            HelicopterComponents.Instance.autoTrim.AutoTrimOn();
        }

        if (AssistValuesList[(int)AssistPresets.value].stabilizationValue == 0f && AssistValuesList[(int)AssistPresets.value].rateLimitValue == 0f)
        {
            HelicopterComponents.Instance.rotaryComputer.m_mode = Oyedoyin.Common.Computer.Mode.Manual;

            if (AssistValuesList[(int)AssistPresets.value].mainRotorTorqueValue != 0.0f)
            {
                HelicopterComponents.Instance.rotaryController.m_torqueMode = RotaryController.TorqueMode.Conventional;
            }
            else
            {
                HelicopterComponents.Instance.rotaryController.m_torqueMode = RotaryController.TorqueMode.Corrected;
            }
        }
        else
        {
            HelicopterComponents.Instance.rotaryComputer.m_mode = Oyedoyin.Common.Computer.Mode.Augmented;
            HelicopterComponents.Instance.rotaryController.m_torqueMode = RotaryController.TorqueMode.Corrected;

            HelicopterComponents.Instance.rotaryComputer.m_mode = Oyedoyin.Common.Computer.Mode.Augmented;
            HelicopterComponents.Instance.rotaryComputer.Gaф = (-0.175 * AssistValuesList[(int)AssistPresets.value].stabilizationValue);
            HelicopterComponents.Instance.rotaryComputer.Gap = (-0.12 * AssistValuesList[(int)AssistPresets.value].rateLimitValue);
            HelicopterComponents.Instance.rotaryComputer.Gaxlat = (2.0 * AssistValuesList[(int)AssistPresets.value].stabilizationValue);

            HelicopterComponents.Instance.rotaryComputer.Gbθ = (0.15 * AssistValuesList[(int)AssistPresets.value].stabilizationValue);
            HelicopterComponents.Instance.rotaryComputer.Gbq = (0.12 * AssistValuesList[(int)AssistPresets.value].rateLimitValue);

            HelicopterComponents.Instance.rotaryComputer.Gθtr = (0.33 * AssistValuesList[(int)AssistPresets.value].stabilizationValue);
        }
    }

    public void ResetHelicopter()
    {
        if (!helicopterLinkInitialized) return;

        if (waypointManager != null)
        {
            waypointManager.resetLap();
        }

        StartCoroutine(RestoreRotationAndLockPosition());
    }

    public void PlayerResetHelicopter()
    {
        if (!helicopterLinkInitialized) return;

        if (waypointManager != null)
        {
            waypointManager.resetLap();
        }
        StartCoroutine(RestoreRotationAndLockPosition(false));
    }

    public void NewSession()
    {
        if (!helicopterLinkInitialized) return;

        if (waypointManager != null)
        {
            waypointManager.resetLap();
            RestartTime();
        }
        // Reset and Start the time
        RestartTime();
        PauseTime();
        StartCoroutine(RestoreRotationAndLockPosition());
    }

    public void ResetVROrientation()
    {
        // TODO - OpenXR Reset viewer pose
    }

    public void startEngine()
    {
        if (!helicopterLinkInitialized) return;

        HelicopterComponents.Instance.rotaryController.TurnOnEngines();
        HelicopterComponents.Instance.rotaryController._throttleInput = 1f;
    }

    private IEnumerator RestoreRotationAndLockPosition(bool lockCollective = true)
    {
        if (!helicopterLinkInitialized) yield return null;

        PauseTime();

        Quaternion startRotation = HelicopterComponents.Instance.helicopterRb.transform.rotation;
        Quaternion endRotation = HelicopterComponents.Instance.rotaryController.baseRotation;
        float duration = 2f; // Duration over which to reset rotation
        float elapsedTime = 0f;

        // Ensure the rigidbody doesn't continue moving or rotating due to other forces
        HelicopterComponents.Instance.helicopterRb.linearVelocity = Vector3.zero;
        HelicopterComponents.Instance.helicopterRb.angularVelocity = Vector3.zero;
        HelicopterComponents.Instance.helicopterRb.isKinematic = true; // Temporarily make the rigidbody kinematic to ignore physics

        while (elapsedTime < duration)
        {
            HelicopterComponents.Instance.helicopterRb.transform.position = HelicopterComponents.Instance.rotaryController.basePosition; // Lock position by setting it each frame
            HelicopterComponents.Instance.helicopterRb.transform.rotation = Quaternion.Lerp(startRotation, endRotation, elapsedTime / duration);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Ensure the rotation is exactly the target rotation after the loop
        HelicopterComponents.Instance.helicopterRb.transform.rotation = endRotation;

        // Optionally, reset the rigidbody to be non-kinematic after the operation
        HelicopterComponents.Instance.helicopterRb.isKinematic = false;

        waypointManager.goLock = false;

        if (lockCollective) LockCollective();
    }

    private void LockCollective()
    {
        Debug.Log(helicopterLinkInitialized);
        if (!helicopterLinkInitialized) return;

        if (isBallSim)
        {
            Debug.Log("Locking Collective");
            HelicopterComponents.Instance.rotaryComputer.collectiveUpper = HelicopterComponents.Instance.rotaryComputer.collectiveLower;

            Debug.Log("Popping up");
            unlockCollectivePopup.SetActive(true);
        }

    }

    public void UnlockCollective()
    {
        if (!helicopterLinkInitialized) return;

        if (isBallSim)
        {
            HelicopterComponents.Instance.rotaryComputer.collectiveUpper = collectiveUpperSaveValue;
            unlockCollectivePopup.SetActive(false);
        }
        ResumeTime();
    }
    #endregion

    #region TimeControls
    public void PauseTime()
    {
        isTimePaused = true;
    }

    public void ResumeTime()
    {
        Debug.Log("Resuming Time");
        isTimePaused = false;
    }

    public void RestartTime()
    {
        timeRemainingSeconds = timeLimitSeconds;
        if (timeRemainingText != null)
            timeRemainingText.text = timeRemainingSeconds.ToString("F0") + " Seconds";
        timeRemainingTextMenu.text = timeRemainingSeconds.ToString("F0");
        HelicopterComponents.Instance.timeRemainingTextInternal.text = timeRemainingSeconds.ToString("F0") + " Seconds";
        PauseTime();
    }

    public void Add30Seconds()
    {
        timeRemainingSeconds += 30;
    }

    public void Remove30Seconds()
    {
        timeRemainingSeconds -= 30;
    }

    #endregion

    #region MenuControls

    public void QuitGame()
    {
        Application.Quit();
    }

    #endregion

    #region SceneControls

    public void CinematicCameraToggleCheck(bool isOn)
    {
        Debug.Log("Cinema Enabled" + cinemaCamLinkInitialized);
        Debug.Log("isOn" + isOn);

        if (!cinemaCamLinkInitialized) { return; }

        if (CinemaCamToggle.isOn)
        {
            CinematicCams.GetComponent<TimeTrialCameraManager>().EnableFixedCams();
            ThirdPersonCam.SetActive(false);
            cinematicCams = true;
        }
        else
        {
            CinematicCams.GetComponent<TimeTrialCameraManager>().DisableFixedCams();
            ThirdPersonCam.SetActive(true);
            cinematicCams = false;
        }
    }

    public void OfflineMapToggleCheck(bool isOn)
    {
        if (OfflineMapToggle.isOn)
        {
            OfflineMap.SetActive(true);
            OnlineMap.SetActive(false);
        }
        else
        {
            OfflineMap.SetActive(false);
            OnlineMap.SetActive(true);
        }
    }

    #endregion
}
