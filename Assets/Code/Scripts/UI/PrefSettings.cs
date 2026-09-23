using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using Oyedoyin.RotaryWing;
using UnityEngine;
using System.IO; 
using System;
using System.Globalization;
using System.Text.RegularExpressions;

public enum ControlScheme
{
    KBM,
    Warthog
}

public class PrefSettings : MonoBehaviour
{
    public const uint MaximumCesiumTileLimit = 1000;
    private uint lastValidMaximumSimultaneousTileLoads = 28;
    private uint lastValidLoadingDescendantLimit = 10;

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

    // Cesium 1.23.2 accepts zero in this uint setter.
    [Range(0, 1000)]
    public uint MaximumSimultaneousTileLoads = 28;

    public int MaximumCachedBytes = 1684354560;

    // Cesium documents zero as loading each level of detail successively.
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

    public static bool TryParseCesiumTileLimit(int value, out uint parsed)
    {
        parsed = 0;
        if (value < 0 || value > MaximumCesiumTileLimit)
        {
            return false;
        }

        parsed = (uint)value;
        return true;
    }

    private bool HasValidCesiumTileLimits() =>
        MaximumSimultaneousTileLoads <= MaximumCesiumTileLimit &&
        LoadingDescendantLimit <= MaximumCesiumTileLimit;

    private void Awake()
    {
        if (HasValidCesiumTileLimits())
        {
            RememberValidCesiumTileLimits();
        }
    }

    private void RememberValidCesiumTileLimits()
    {
        lastValidMaximumSimultaneousTileLoads = MaximumSimultaneousTileLoads;
        lastValidLoadingDescendantLimit = LoadingDescendantLimit;
    }

    public void VariablesToObjects()
    {
        // Set the cesium asset settings
        if (HasValidCesiumTileLimits())
        {
            if (cesiumAsset != null)
            {
                cesiumAsset.maximumScreenSpaceError = screenSpaceError;
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
            }
            RememberValidCesiumTileLimits();
        }
        else
        {
            Debug.LogWarning("Cesium tile limits must be between 0 and 1000. Cesium settings were not applied.");
        }
    
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
        if (TryParseCesiumTileLimit(PlayerPrefs.GetInt("MaximumSimultaneousTileLoads", (int)MaximumSimultaneousTileLoads), out uint tileLoads))
        {
            MaximumSimultaneousTileLoads = tileLoads;
        }
        MaximumCachedBytes = PlayerPrefs.GetInt("MaximumCachedBytes", MaximumCachedBytes);
        if (TryParseCesiumTileLimit(PlayerPrefs.GetInt("LoadingDescendantLimit", (int)LoadingDescendantLimit), out uint descendantLimit))
        {
            LoadingDescendantLimit = descendantLimit;
        }
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
        if (HasValidCesiumTileLimits())
        {
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
        }

        PlayerPrefs.SetInt("controlScheme", (int)controlScheme);
        PlayerPrefs.SetInt("enableNovaPopup", enableNovaPopup ? 1 : 0);
        PlayerPrefs.SetInt("volume", volume);
    }

    
    public void VariablesToJSON()
    {
        try
        {
            // Serialize the current settings to JSON
            string json;
            if (HasValidCesiumTileLimits())
            {
                json = JsonUtility.ToJson(this, true);
            }
            else
            {
                uint draftTileLoads = MaximumSimultaneousTileLoads;
                uint draftDescendantLimit = LoadingDescendantLimit;
                try
                {
                    MaximumSimultaneousTileLoads = lastValidMaximumSimultaneousTileLoads;
                    LoadingDescendantLimit = lastValidLoadingDescendantLimit;
                    json = JsonUtility.ToJson(this, true);
                }
                finally
                {
                    MaximumSimultaneousTileLoads = draftTileLoads;
                    LoadingDescendantLimit = draftDescendantLimit;
                }
            }

            // Define the path for the JSON file
            string directory = Path.Combine(Application.persistentDataPath, "Settings");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "settings.json");

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

                if (TryLoadFromJson(json))
                {
                    Debug.Log("Settings loaded from JSON: " + path);
                }
                else
                {
                    Debug.LogWarning("Invalid Cesium tile limits in settings JSON were ignored; other settings were loaded.");
                }
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
    }

    public bool TryLoadFromJson(string json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return false;
        }

        Cesium3DTileset oldTileset = cesiumAsset;
        string previousJson = JsonUtility.ToJson(this);
        bool allTileLimitsValid = true;
        string sanitizedJson = ReplaceInvalidJsonTileLimit(json, nameof(MaximumSimultaneousTileLoads),
            MaximumSimultaneousTileLoads <= MaximumCesiumTileLimit ? MaximumSimultaneousTileLoads : lastValidMaximumSimultaneousTileLoads,
            ref allTileLimitsValid);
        sanitizedJson = ReplaceInvalidJsonTileLimit(sanitizedJson, nameof(LoadingDescendantLimit),
            LoadingDescendantLimit <= MaximumCesiumTileLimit ? LoadingDescendantLimit : lastValidLoadingDescendantLimit,
            ref allTileLimitsValid);
        JsonUtility.FromJsonOverwrite(sanitizedJson, this);
        cesiumAsset = oldTileset;
        if (!HasValidCesiumTileLimits())
        {
            JsonUtility.FromJsonOverwrite(previousJson, this);
            cesiumAsset = oldTileset;
            return false;
        }

        return allTileLimitsValid;
    }

    private static string ReplaceInvalidJsonTileLimit(string json, string fieldName, uint fallback, ref bool allValid)
    {
        string pattern = "(\"" + fieldName + "\"\\s*:\\s*)([^,}\\r\\n]+)";
        bool invalidFound = false;
        string sanitized = Regex.Replace(json, pattern, match =>
        {
            if (!int.TryParse(match.Groups[2].Value.Trim(), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int value) ||
                !TryParseCesiumTileLimit(value, out _))
            {
                invalidFound = true;
                return match.Groups[1].Value + fallback.ToString(CultureInfo.InvariantCulture);
            }

            return match.Value;
        });
        if (invalidFound) allValid = false;
        return sanitized;
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
