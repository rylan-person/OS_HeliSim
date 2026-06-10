using System.Collections;
using System.Collections.Generic;
using CesiumForUnity;
using UnityEngine;

public class RecreateTileset : MonoBehaviour
{
    [SerializeField] private Cesium3DTileset cesiumAsset;
    

    // Update is called once per frame
    void Update()
    {
        // On k press
        if (Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("Audio Volume: " + AudioListener.volume);
        }
        
    }
}
