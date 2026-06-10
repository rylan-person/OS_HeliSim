using UnityEngine;
using CesiumForUnity;
using Unity.Cinemachine;

public class ViewManager : MonoBehaviour
{
    public static ViewManager Instance { get; private set; }
    public CesiumCameraManager cesiumCameraManager;

    public enum ViewType
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        FullScreen
    }

    public enum CameraMode
    {
        Follow,
        FirstPerson,
        Dynamic,
        Orbit,
        TopDown,
        SideOn
    }

    public struct CameraView
    {
        public bool CameraNeeded;
        public Camera camera;
        public GameObject CameraManager;

        public CameraView(bool cameraNeeded = false, Camera camera = null, GameObject cameraManager = null)
        {
            this.CameraNeeded = cameraNeeded;
            this.camera = camera;
            this.CameraManager = cameraManager;
        }
    }

    public CameraView[] cameraViews = new CameraView[5];
    public Camera[] cameras = new Camera[5];
    public CinemachineCamera[] cameraManagers = new CinemachineCamera[6];

    public bool otherMenuOpen = false;
    public Camera InputCamera;
    public Canvas InputUICanvas;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this.gameObject);
        }
        else
        {
            Instance = this;
        }
    }

    void Start()
    {
        // Initialize the camera views
        cameraViews[(int)ViewType.TopLeft]     = new CameraView(true, cameras[(int)ViewType.TopLeft],     null);
        cameraViews[(int)ViewType.TopRight]    = new CameraView(true, cameras[(int)ViewType.TopRight],    null);
        cameraViews[(int)ViewType.BottomLeft]  = new CameraView(true, cameras[(int)ViewType.BottomLeft],  null);
        cameraViews[(int)ViewType.BottomRight] = new CameraView(true, cameras[(int)ViewType.BottomRight], null);
        cameraViews[(int)ViewType.FullScreen]  = new CameraView(true, cameras[(int)ViewType.FullScreen],  null);
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
