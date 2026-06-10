using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using Oyedoyin.RotaryWing;
using UnityEngine;
using System.IO; 
using System;
using Code.Scripts.UI;

public enum ControlScheme
{
    KBM,
    Warthog
}

public class PrefSettings : MonoBehaviour
{
    #region Cesium Settings
    [SerializeField] private Cesium3DTileset cesiumAsset;

    [Header("Cesium Settings")]
    [Space(5)]

    // Level of detail
    [Header("Level of Detail")]
    [Range(10, 256)]
    public int screenSpaceError = 64;
    [Space(5)]

    // Tile Loading
    [Header("Tile Loading")]
    public bool preloadAncestors = true;
    public bool preloadSiblings = true;
    public bool forbidHoles = true;

    [Range(1, 1000)]
    public uint MaximumSimultaneousTileLoads = 28;

    public int MaximumCachedBytes = 1684354560;

    [Range(0, 1000)]
    public uint LoadingDescendantLimit = 10;
    [Space(5)]

    // Tile Culling
    [Header("Tile Culling")]
    public bool enableFrustumCulling = false;
    public bool enableFogCulling = false;
    public bool enableForceScreenSpaceError = false;

    // Range: ???0 - 100???
    public int culledScreenSpaceError = 0;
    [Space(5)]

    #endregion

    public ControlScheme controlScheme = ControlScheme.Warthog;
    public bool enableNovaPopup = true;
    public int volume = 10;

    [SerializeField] private SettingsMenu settingsMenu;

    public void VariablesToObjects()
    {
        // Set the cesium asset settings
        cesiumAsset.maximumScreenSpaceError = screenSpaceError;//screenSpaceError
        cesiumAsset.preloadAncestors = preloadAncestors;
        cesiumAsset.preloadSiblings = preloadSiblings;
        cesiumAsset.forbidHoles = forbidHoles;
        cesiumAsset.maximumSimultaneousTileLoads = MaximumSimultaneousTileLoads;
        cesiumAsset.maximumCachedBytes = MaximumCachedBytes;
        cesiumAsset.loadingDescendantLimit = LoadingDescendantLimit;
        cesiumAsset.enableFrustumCulling = enableFrustumCulling;
        cesiumAsset.enableFogCulling = enableFogCulling;
        cesiumAsset.enforceCulledScreenSpaceError = enableForceScreenSpaceError;
        cesiumAsset.culledScreenSpaceError = culledScreenSpaceError;
    
        // Get the helicopter controller
        RotaryController helicopterController = FindObjectOfType<RotaryController>();
        if (controlScheme == ControlScheme.KBM)
        {
            //helicopterController.m_inputLogic = Oyedoyin.Common.Controller.InputLogic.Legacy;
        }
        if (controlScheme == ControlScheme.Warthog)
        {
            //helicopterController.m_inputLogic = Oyedoyin.Common.Controller.InputLogic.InputSystem;
        }

        // Get the Menu
        MainMenu menu = FindFirstObjectByType<MainMenu>();
        if (menu != null)
        {
            menu.isBallSim = enableNovaPopup; 
        }

        AudioListener.volume = volume / 200.0f;

        VariablesToJSON();
        VariablesToSettings();
    }

    public void SettingsToVariables()
    {
        // Get from the playerPrefs
        Debug.Log("Settings to Variables");
        screenSpaceError = PlayerPrefs.GetInt("screenSpaceError", screenSpaceError);
        preloadAncestors = PlayerPrefs.GetInt("preloadAncestors", preloadAncestors ? 1 : 0) == 1;
        preloadSiblings = PlayerPrefs.GetInt("preloadSiblings", preloadSiblings ? 1 : 0) == 1;
        forbidHoles = PlayerPrefs.GetInt("forbidHoles", forbidHoles ? 1 : 0) == 1;
        MaximumSimultaneousTileLoads = (uint)PlayerPrefs.GetInt("MaximumSimultaneousTileLoads", (int)MaximumSimultaneousTileLoads);
        MaximumCachedBytes = PlayerPrefs.GetInt("MaximumCachedBytes", MaximumCachedBytes);
        LoadingDescendantLimit = (uint)PlayerPrefs.GetInt("LoadingDescendantLimit", (int)LoadingDescendantLimit);
        enableFrustumCulling = PlayerPrefs.GetInt("enableFrustumCulling", enableFrustumCulling ? 1 : 0) == 1;
        enableFogCulling = PlayerPrefs.GetInt("enableFogCulling", enableFogCulling ? 1 : 0) == 1;
        enableForceScreenSpaceError = PlayerPrefs.GetInt("enableForceScreenSpaceError", enableForceScreenSpaceError ? 1 : 0) == 1;
        culledScreenSpaceError = PlayerPrefs.GetInt("culledScreenSpaceError", culledScreenSpaceError);

        controlScheme = (ControlScheme)PlayerPrefs.GetInt("controlScheme", (int)controlScheme);
        enableNovaPopup = PlayerPrefs.GetInt("enableNovaPopup", enableNovaPopup ? 1 : 0) == 1;
        volume = PlayerPrefs.GetInt("volume", volume);
    }

    public void VariablesToSettings()
    {
        Debug.Log("Variables to Settings");
        // Get the PlayerPrefs from the variables
        PlayerPrefs.SetInt("screenSpaceError", screenSpaceError);
        Debug.Log("screenSpaceError: " + PlayerPrefs.GetInt("screenSpaceError", screenSpaceError));
        PlayerPrefs.SetInt("preloadAncestors", preloadAncestors ? 1 : 0);
        PlayerPrefs.SetInt("preloadSiblings", preloadSiblings ? 1 : 0);
        PlayerPrefs.SetInt("forbidHoles", forbidHoles ? 1 : 0);
        PlayerPrefs.SetInt("MaximumSimultaneousTileLoads", (int)MaximumSimultaneousTileLoads);
        PlayerPrefs.SetInt("MaximumCachedBytes", MaximumCachedBytes);
        PlayerPrefs.SetInt("LoadingDescendantLimit", (int)LoadingDescendantLimit);
        PlayerPrefs.SetInt("enableFrustumCulling", enableFrustumCulling ? 1 : 0);
        PlayerPrefs.SetInt("enableFogCulling", enableFogCulling ? 1 : 0);
        PlayerPrefs.SetInt("enableForceScreenSpaceError", enableForceScreenSpaceError ? 1 : 0);
        PlayerPrefs.SetInt("culledScreenSpaceError", culledScreenSpaceError);

        PlayerPrefs.SetInt("controlScheme", (int)controlScheme);
        PlayerPrefs.SetInt("enableNovaPopup", enableNovaPopup ? 1 : 0);
        PlayerPrefs.SetInt("volume", volume);
    }

    
    public void VariablesToJSON()
    {
        try
        {
            // Serialize the current settings to JSON
            string json = JsonUtility.ToJson(this, true); // 'this' refers to the PrefSettings instance

            // Define the path for the JSON file
            string path = Application.persistentDataPath + "/Settings/settings.json";

            // Write the JSON string to the file
            File.WriteAllText(path, json);

            Debug.Log("Settings saved to JSON at: " + path);
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to save settings to JSON: " + e.Message);
        }
    }


    public void JSONToVariables()
    {
        try
        {
            // Define the path for the JSON file
            string path = Application.persistentDataPath + "/Settings/settings.json";

            // Check if the file exists
            if (File.Exists(path))
            {
                // Read the JSON string from the file
                string json = File.ReadAllText(path);

                Cesium3DTileset oldTileset = cesiumAsset;
                SettingsMenu oldSettingsMenu = settingsMenu;

                // Populate this object with the data from the JSON
                JsonUtility.FromJsonOverwrite(json, this);

                cesiumAsset = oldTileset;
                settingsMenu = oldSettingsMenu;

                Debug.Log("Settings loaded from JSON: " + path);
            }
            else
            {
                Debug.LogWarning("Settings file not found at: " + path);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("Failed to load settings from JSON: " + e.Message);
        }
    }

    
    // On start load settings from player prefs
    private void Start()
    {
        JSONToVariables();
        VariablesToObjects();

        settingsMenu.SetUiVariables();
    }

    // On "backspace" press variables to object
    // On = press player prefs to variables
    // On - press variables to player prefs
    private void Update()
    {
        /*
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            VariablesToObjects();
        }
        if (Input.GetKeyDown(KeyCode.Equals))
        {
            SettingsToVariables();
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            VariablesToSettings();
        }
        if (Input.GetKeyDown(KeyCode.J))
        {
            JSONToVariables();
        }
        if (Input.GetKeyDown(KeyCode.K))
        {
            VariablesToJSON();
        }
        */
    }
    
}
