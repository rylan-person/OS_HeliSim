using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Oyedoyin.RotaryWing.SilantroStabilizer;

public class HelicopterHUD : MonoBehaviour
{
    [SerializeField]
    private Canvas canvas;
    RectTransform hudCenterRect;
    GameObject velocityMarkerGO;

    [SerializeField]
    new Camera camera;
    Transform cameraTransform;
    [SerializeField]
    Transform cameraParentTransform;
    [SerializeField]
    Transform hudCenter;
    [SerializeField]
    Transform velocityMarker;
    [SerializeField]
    Rigidbody rb;
    [SerializeField]
    RectTransform velocityRoot;

    public Oyedoyin.Common.Controller m_vehicle;
    private Color defaultUIColor = new(0, 1, 0.0156f, 1);

    public TextMeshProUGUI climbRateText;
    public Slider climbRateSlider;

    public TextMeshProUGUI altitudeText;
    public Slider altitudeSlider;

    public TextMeshProUGUI powerLevelText;
    public TextMeshProUGUI collectiveLevelText;

    public TextMeshProUGUI gLoadText;
    public TextMeshProUGUI speedText;

    UnityEngine.Vector3 TransformToHUDSpace(Vector3 worldSpace)
    {
        var screenSpace = camera.WorldToScreenPoint(worldSpace);
        return screenSpace - new Vector3(camera.pixelWidth / 2, camera.pixelHeight / 2);
    }

    void UpdateHUDCenter()
    {
        /*
        var rotation = cameraTransform.localEulerAngles;
        var hudPos = TransformToHUDSpace(cameraTransform.position + rb.transform.forward); //


        hudCenterGO.SetActive(true);
        //hudCenter.localPosition = new Vector3(hudPos.x, hudPos.y, 0);
        hudCenter.localEulerAngles = new Vector3(0, 0, -rotation.z);
        */
        //UIHelper.UIHelper.PointToUISpace(canvas, hudCenterRect, rb.transform.position + rb.transform.forward*1000 + cameraParentTransform.localPosition, cameraTransform);

    }

    private void Start()
    {
        cameraTransform = camera.transform;
        hudCenterRect = hudCenter.GetComponent<RectTransform>();
        velocityMarkerGO = velocityMarker.gameObject;
    }


    void UpdateVelocityMarker()
    {
        var velocity = rb.transform.forward;

        if (rb.linearVelocity.sqrMagnitude > 1)
        {
            velocity = rb.linearVelocity;
        }

        //Get the direction of the velocity as a rotation
        var velocityDirection = Quaternion.LookRotation(velocity, Vector3.up);

        velocityRoot.rotation = velocityDirection;

        //UIHelper.UIHelper.PointToUISpace(canvas, velocityRoot, rb.transform.position + velocity*1000f, cameraTransform);


    }

    // Update is called once per frame
    void LateUpdate()
    {
        UpdateHUDCenter();
        UpdateVelocityMarker();

        // Climb Rate
        climbRateText.text = (m_vehicle.m_core.δz).ToString("0.0") + "m/s >";
        if (m_vehicle.m_core.δz < -10)
        {
            climbRateText.color = Color.red;
        }
        else
        {
            climbRateText.color = defaultUIColor;
        }
        climbRateSlider.value = (float)m_vehicle.m_core.δz;

        // Altitude
        altitudeText.text =  "< " + (m_vehicle.m_core.z - 6.65).ToString("0.0") + "m";
        if (m_vehicle.m_core.z < 25)
        {
            altitudeText.color = Color.red;
        }
        else
        {
            altitudeText.color = defaultUIColor;
        }
        altitudeSlider.value = (float)(m_vehicle.m_core.z - 30.4);

        // Collective and Power Level
        powerLevelText.text = (m_vehicle.m_powerLevel * 100).ToString("0.0") + "%";
        collectiveLevelText.text = (m_vehicle._collectiveInput * 100f).ToString("0") + " %";

        // G-Load
        gLoadText.text = m_vehicle.m_core.n.ToString("0.00");
        if (m_vehicle.m_core.n > 4 || m_vehicle.m_core.n < -1)
        {
            gLoadText.color = Color.red;
        }
        else
        {
            gLoadText.color = defaultUIColor;
        }

        // Speed
        double u = m_vehicle.m_core.u;
        double v = m_vehicle.m_core.v;
        float Speed = (float)System.Math.Sqrt((u * u) + (v * v));

        float speedly = Speed * 3.6f;
        speedText.text = speedly.ToString("0.0") + " kmh";


    }
}
