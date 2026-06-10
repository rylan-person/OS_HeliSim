using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HorrizonIndicator : MonoBehaviour
{
    Transform helitransform;
    // Start is called before the first frame update
    void Start()
    {
        helitransform = HelicopterComponents.Instance.helicopterTransform;
    }

    // Update is called once per frame
    void Update()
    {
        transform.rotation = Quaternion.Euler(-90f, 180f, 0f);
        
    }
}
