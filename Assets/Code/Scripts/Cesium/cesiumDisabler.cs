using System.Collections;
using UnityEngine;

public class cesiumDisabler : MonoBehaviour
{
    // Serialized object to enable after disabling the target's children
    [SerializeField] private GameObject objectToEnable;

    // Delay in seconds before disabling the target's children
    [SerializeField] private float delayTime = 10f;

    // Direct reference to the parent object whose children should be disabled.
    // NOTE: this used to be a GameObject.Find() lookup keyed by a string that had
    // accidentally been set to a Cesium tile URL, so the lookup could never succeed
    // and this component silently did nothing. A direct scene reference is both
    // correct and considerably cheaper than a scene-wide name search.
    [SerializeField] private GameObject parentObject;

    void Start()
    {
        // Start the coroutine to wait and then disable the target's children
        StartCoroutine(DisableChildrenAfterDelay());
    }

    private IEnumerator DisableChildrenAfterDelay()
    {
        yield return new WaitForSeconds(delayTime);

        if (parentObject == null)
        {
            Debug.LogWarning($"{nameof(cesiumDisabler)}: no '{nameof(parentObject)}' assigned; nothing to disable.");
            yield break;
        }

        foreach (Transform child in parentObject.transform)
        {
            child.gameObject.SetActive(false);
        }

        if (objectToEnable != null)
        {
            objectToEnable.SetActive(true);
        }
    }
}
