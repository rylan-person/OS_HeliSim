using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class cesiumDisabler : MonoBehaviour
{
    // Serialized object to enable after disabling the hit object
    [SerializeField] private GameObject objectToEnable;

    // Delay in seconds before raycasting
    [SerializeField] private float delayTime = 10f;

    [SerializeField] private string parentObjectName = "https://tile.googleapis.com/v1/3dtiles/datasets/CgA/files/UlRPVEYubm9kZWRhdGEucGxhbmV0b2lkPWVhcnRoLG5vZGVfZGF0YV9lcG9jaD05NzIscGF0aD0xMzcwNDM1MzQwNDM2MzUyNjAsY2FjaGVfdmVyc2lvbj02LGFsaWdubWVudF92ZXJzaW9uPVJPQ0tUUkVFXzk4OF9HT09HTEVfREFUVU1fMjAyNDA3MDRUMDc1M1pfZ2VuZXJhdGVkX2F0XzIwMjQwOTE3VDEyMDha.glb?session=CJ_Xmej7saPK0QEQwrWzuAY&key=AIzaSyDrSNqujmAmhhZtenz6MEofEuITd3z0JM0";

    void Start()
    {
        // Start the coroutine to wait and then perform the raycast
        StartCoroutine(RaycastAndSwitch());
    }

    private IEnumerator RaycastAndSwitch()
    {
        // Wait for the specified delay time
        yield return new WaitForSeconds(delayTime);

        /*

        // Perform the raycast downwards from the object's position
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit))
        {
            // Get the parent of the hit object
            Transform parentTransform = hit.collider.transform.parent;

            if (parentTransform != null)
            {
                // Disable all the siblings (including the hit object itself)
                foreach (Transform sibling in parentTransform)
                {
                    sibling.gameObject.SetActive(false);
                }
            }
            else
            {
                // If no parent, just disable the hit object itself
                hit.collider.gameObject.SetActive(false);
            }

            // Enable the serialized object
            if (objectToEnable != null)
            {
                objectToEnable.SetActive(true);
            }
        }
        */
        
            // The name of the parent object whose children we want to disable

        // Find the parent object by name
        GameObject parentObject = GameObject.Find(parentObjectName);

        if (parentObject != null)
        {
            // Loop through all the children of the parent object and disable them
            foreach (Transform child in parentObject.transform)
            {
                child.gameObject.SetActive(false);
            }

            Debug.Log("All children of the object named '" + parentObjectName + "' have been disabled.");
        }
        else
        {
            Debug.LogWarning("No object found with the name '" + parentObjectName + "'.");
        }

    }
}
