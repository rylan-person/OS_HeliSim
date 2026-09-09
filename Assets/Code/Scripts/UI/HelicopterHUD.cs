using System.Collections;
using System.Collections.Generic;
using System.Text;
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

    [Header("Warning Thresholds")]
    [Tooltip("Climb rate (m/s) below which the climb-rate readout turns red.")]
    [SerializeField] private double climbRateWarningThreshold = -10;
    [Tooltip("Altitude (m) below which the altitude readout turns red.")]
    [SerializeField] private double altitudeWarningThreshold = 25;
    [Tooltip("Vertical offset (m) subtracted from raw altitude to account for the helicopter's origin height above ground.")]
    [SerializeField] private double altitudeDisplayOffset = 6.65;
    [Tooltip("Offset applied to the altitude slider so its 0 value lines up with the ground.")]
    [SerializeField] private double altitudeSliderOffset = 30.4;
    [Tooltip("G-load above which the readout turns red.")]
    [SerializeField] private double gLoadHighWarning = 4;
    [Tooltip("G-load below which the readout turns red.")]
    [SerializeField] private double gLoadLowWarning = -1;
    [Tooltip("Conversion factor from m/s to km/h.")]
    [SerializeField] private float metersPerSecondToKmh = 3.6f;

    // Reused per-field so numeric HUD readouts don't allocate a new string every frame.
    private readonly StringBuilder _climbRateBuilder = new StringBuilder(16);
    private readonly StringBuilder _altitudeBuilder = new StringBuilder(16);
    private readonly StringBuilder _powerLevelBuilder = new StringBuilder(16);
    private readonly StringBuilder _collectiveBuilder = new StringBuilder(16);
    private readonly StringBuilder _gLoadBuilder = new StringBuilder(16);
    private readonly StringBuilder _speedBuilder = new StringBuilder(16);

    UnityEngine.Vector3 TransformToHUDSpace(Vector3 worldSpace)
    {
        var screenSpace = camera.WorldToScreenPoint(worldSpace);
        return screenSpace - new Vector3(camera.pixelWidth / 2, camera.pixelHeight / 2);
    }

    void UpdateHUDCenter()
    {
        // Reserved for future HUD-center reticle placement (currently unused; see UIHelper.PointToUISpace).
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
    }

    // Update is called once per frame
    void LateUpdate()
    {
        UpdateHUDCenter();
        UpdateVelocityMarker();

        // Climb Rate
        _climbRateBuilder.Clear();
        _climbRateBuilder.Append(m_vehicle.m_core.δz.ToString("0.0"));
        _climbRateBuilder.Append("m/s >");
        climbRateText.SetText(_climbRateBuilder);
        climbRateText.color = m_vehicle.m_core.δz < climbRateWarningThreshold ? Color.red : defaultUIColor;
        climbRateSlider.value = (float)m_vehicle.m_core.δz;

        // Altitude
        _altitudeBuilder.Clear();
        _altitudeBuilder.Append("< ");
        _altitudeBuilder.Append((m_vehicle.m_core.z - altitudeDisplayOffset).ToString("0.0"));
        _altitudeBuilder.Append("m");
        altitudeText.SetText(_altitudeBuilder);
        altitudeText.color = m_vehicle.m_core.z < altitudeWarningThreshold ? Color.red : defaultUIColor;
        altitudeSlider.value = (float)(m_vehicle.m_core.z - altitudeSliderOffset);

        // Collective and Power Level
        _powerLevelBuilder.Clear();
        _powerLevelBuilder.Append((m_vehicle.m_powerLevel * 100).ToString("0.0"));
        _powerLevelBuilder.Append("%");
        powerLevelText.SetText(_powerLevelBuilder);

        _collectiveBuilder.Clear();
        _collectiveBuilder.Append((m_vehicle._collectiveInput * 100f).ToString("0"));
        _collectiveBuilder.Append(" %");
        collectiveLevelText.SetText(_collectiveBuilder);

        // G-Load
        _gLoadBuilder.Clear();
        _gLoadBuilder.Append(m_vehicle.m_core.n.ToString("0.00"));
        gLoadText.SetText(_gLoadBuilder);
        gLoadText.color = (m_vehicle.m_core.n > gLoadHighWarning || m_vehicle.m_core.n < gLoadLowWarning) ? Color.red : defaultUIColor;

        // Speed
        double u = m_vehicle.m_core.u;
        double v = m_vehicle.m_core.v;
        float Speed = (float)System.Math.Sqrt((u * u) + (v * v));

        float speedly = Speed * metersPerSecondToKmh;
        _speedBuilder.Clear();
        _speedBuilder.Append(speedly.ToString("0.0"));
        _speedBuilder.Append(" kmh");
        speedText.SetText(_speedBuilder);
    }
}
