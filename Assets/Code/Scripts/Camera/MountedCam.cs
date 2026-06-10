using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MountedCam : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        CameraSwitcher.Instance.mountedCam = this.GetComponent<Unity.Cinemachine.CinemachineCamera>();
    }
}
