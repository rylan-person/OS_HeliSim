using Unity.Cinemachine;
using UnityEngine;

public class fpv_camLock : MonoBehaviour
{
    public GameObject cameraTarget;
    public GameObject fpv_view = null;
    public bool validTarget = false;
    public CinemachineCamera cinemachineCamera;

    private Transform currentParent;

    // Update is called once per frame
    void Update()
    {
        Transform nextParent = cameraTarget != null ? cameraTarget.transform.parent : null;
        bool parentChanged = nextParent != currentParent;
        bool fpvNoLongerSibling = fpv_view != null && fpv_view.transform.parent != nextParent;

        if (!validTarget || fpv_view == null || parentChanged || fpvNoLongerSibling)
        {
            if (cameraTarget == null || nextParent == null)
            {
                validTarget = false;
                fpv_view = null;
                currentParent = null;

                if (cinemachineCamera != null)
                {
                    cinemachineCamera.Follow = null;
                }

                return;
            }

            currentParent = nextParent;
            fpv_view = null;

            for (int i = 0; i < currentParent.childCount; i++)
            {
                Transform child = currentParent.GetChild(i);
                if (child.gameObject != cameraTarget && child.CompareTag("fpv_view"))
                {
                    fpv_view = child.gameObject;
                    break;
                }
            }

            validTarget = fpv_view != null;
            if (validTarget && cinemachineCamera != null)
            {
                cinemachineCamera.Follow = fpv_view.transform;
            }
        }
    }
}
