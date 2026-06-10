using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    public TMP_Dropdown sceneDropdown;


     void Start()
    {
        // set the current scene as the active member of the dropdown
        string currentScene = SceneManager.GetActiveScene().name;
        string[] sceneNames = { "Melbourne", "Sydney", "Monaco", "Barcelona", "NewYork" };
        int currentSceneIndex = Array.IndexOf(sceneNames, currentScene);
        sceneDropdown.value = currentSceneIndex;
        
        // Add listener to dropdown
        sceneDropdown.onValueChanged.AddListener(OnSceneSelected);
    }


    // This is called when the dropdown value changes
    public void OnSceneSelected(int index)
    {
        // Get the name of the currently active scene
        string currentScene = SceneManager.GetActiveScene().name;

        // Scene names corresponding to dropdown index
        // "Melbourne", "Sydney", "Monaco", "Barcelona", "NewYork"
        string[] sceneNames = { "Melbourne", "Sydney", "Monaco", "Barcelona", "NewYork" };

        if (index >= 0 && index < sceneNames.Length)
        {
            string selectedScene = sceneNames[index];

            // If the selected scene is not the current scene, load it
            if (selectedScene != currentScene)
            {
                SceneManager.LoadScene(selectedScene);
            }
        }
        else
        {
            Debug.LogError("Unknown scene selection!");
        }
    }
}
