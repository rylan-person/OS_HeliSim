using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;
#endif

public class CameraMovement : MonoBehaviour
{
    public float movementSpeed = 10.0f;
    public float lookSpeed = 3.0f;
    public float zoomSpeed = 2.0f;
    public float minZoom = 2.0f;
    public float maxZoom = 50.0f;
    public float minDistance = 20.0f;
    public float maxDistance = 100.0f;
    public float scrollSpeedMultiplier = 0.1f; // Multiplier for camera speed adjustment with shift

    public float newZoom;
    public float changeDuration;

    private float targetZoom;   // Target zoom we want to reach
    private float zoomDuration; // Duration in frames for the zoom
    private int framesElapsed;  // Time elapsed during zoom transition

    private bool changingMax = true;

    public float _currentFov;

    public GameObject cameraTarget;

    private Vector3 movement;
    [SerializeField] private CinemachineCamera vcam;
    private bool isAimingWithComposer = false;

    void Update()
    {
        if (IsVcamLive())
        {
            HandleMovement();
            HandleRotation();
            HandleZoom();
            HandleCameraSpeed();

            // Check if the middle mouse button is pressed
            if (Input.GetMouseButtonDown(2)) // Middle mouse button is button 2
            {
                ToggleAim();
            }

            if (!isAimingWithComposer)
            {
                // Ensure the roll (Z-axis rotation) stays at 0 when not locked on
                //transform.rotation = Quaternion.Euler(transform.rotation.eulerAngles.x, transform.rotation.eulerAngles.y, 0);
            }
        }

        if (cameraTarget == null)
        {
            cameraTarget = TimeTrialCameraManager.Instance.GetTarget()?.gameObject;
            vcam.Follow = cameraTarget?.transform;
        }

        _currentFov = vcam.Lens.FieldOfView;

        // If we're in the process of zooming, interpolate over the duration
        if (framesElapsed < zoomDuration)
        {
            framesElapsed += 1;  // Increase the time elapsed (this could be frame-based as well)

            // Interpolate between the current zoom and the target zoom
            float progress = framesElapsed / zoomDuration;

            if (changingMax)
            {
                maxZoom = Mathf.Lerp(maxZoom, targetZoom, progress);
            }
            else
            {
                minZoom = Mathf.Lerp(minZoom, targetZoom, progress);
            }
        }
    }

    void HandleMovement()
    {
        float moveX = Input.GetAxis("Horizontal"); // A and D keys
        float moveZ = Input.GetAxis("Vertical");   // W and S keys

        movement = new Vector3(moveX, 0, moveZ);
        movement = transform.TransformDirection(movement); // Move relative to camera's orientation
        transform.position += movement * movementSpeed * Time.deltaTime;
        
    }

    public void SetMaxZoom(float newMaxZoom, float duration)
    {
        changingMax = true;
        targetZoom = newMaxZoom;       // Set the target zoom level
        zoomDuration = duration;       // Set the duration (in frames)
        framesElapsed = 0;              // Reset the time elapsed
    }

    public void SetMinZoom(float newMinZoom, float duration)
    {
        changingMax = false;
        targetZoom = newMinZoom;
        zoomDuration = duration;
        framesElapsed = 0;
    }

    private void HandleRotation()
    {
        if (Input.GetMouseButton(1)) // Right click held down
        {
            float mouseX = Input.GetAxis("Mouse X") * lookSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * lookSpeed;

            // Rotate the camera around the Y-axis (left/right)
            transform.Rotate(Vector3.up * mouseX, Space.World);

            // Rotate the camera around the X-axis (up/down)
            transform.Rotate(Vector3.left * mouseY);
        }
    }

    void HandleZoom()
    {
        // Zoom control with scroll wheel when unlocked
        if (!isAimingWithComposer)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
            {
                return; // Skip zooming if Shift is held down
            }
            if (scroll != 0f)
            {
                float currentFov = vcam.Lens.FieldOfView;
                currentFov -= scroll * zoomSpeed;
                currentFov = Mathf.Clamp(currentFov, minZoom, maxZoom);
                vcam.Lens.FieldOfView = currentFov;
            }
        }
        else
        {
            float distance = Vector3.Distance(transform.position, vcam.Follow.position);

            // Clamp the distance to be within the min and max distance range
            distance = Mathf.Clamp(distance, minDistance, maxDistance);

            // Normalize the distance within the min and max distance
            float normalizedDistance = (distance - minDistance) / (maxDistance - minDistance);

            // Calculate the new FOV based on the normalized distance
            float newFOV = Mathf.Lerp(minZoom, maxZoom, normalizedDistance);

            // Apply the new FOV to the camera
            vcam.Lens.FieldOfView = newFOV;
            
        }
    }

    void HandleCameraSpeed()
    {
        // Adjust camera movement speed with Shift + Scroll
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                movementSpeed += scroll * movementSpeed * scrollSpeedMultiplier;
                movementSpeed = Mathf.Max(1f, movementSpeed); // Prevent speed from going below 1
            }
        }
    }

    void ToggleAim()
    {
        if (vcam != null)
        {
            // Check if it's Composer or Do Nothing, then toggle
            if (isAimingWithComposer)
            {
                // Switch to "Do Nothing"
                vcam.LookAt = null; // Clear the LookAt target
                Debug.Log("Switched to Do Nothing (No Aim)");
            }
            else
            {
                // Add or switch to Composer
                vcam.LookAt = cameraTarget.transform; // Set the LookAt target
                Debug.Log("Switched to Composer Aim");
            }

            // Toggle the aim state
            isAimingWithComposer = !isAimingWithComposer;
        }
    }

    private bool IsVcamLive()
    {
        CinemachineBrain brain = Camera.main.GetComponent<CinemachineBrain>();
        if (brain != null)
        {
            // Get the current active virtual camera from the brain
            CinemachineCamera liveCamera = brain.ActiveVirtualCamera as CinemachineCamera;

            // Check if this vcam is the live (active) camera
            return liveCamera == vcam;
        }

        return false;
    }

#if UNITY_EDITOR
    [CustomEditor(typeof(CameraMovement))]
    class CameraMovementEditor : Editor {
        CameraMovement cameraMovement;
        SerializedProperty cameraTarget;
        SerializedProperty currentFov;

        SerializedProperty maxZoom;
        SerializedProperty minZoom;

        float newZoom = 5f;
        float duration = 2f;    

        void OnEnable()
        {
            cameraMovement = (CameraMovement)target;
            cameraTarget = serializedObject.FindProperty("cameraTarget");
            currentFov = serializedObject.FindProperty("_currentFov");
            maxZoom = serializedObject.FindProperty("maxZoom");
            minZoom = serializedObject.FindProperty("minZoom");
        }
        public override void OnInspectorGUI() {

            serializedObject.Update();

            GUILayout.Space(8f);
            EditorGUILayout.PropertyField(cameraTarget, new GUIContent("Camera Target"));
            GUILayout.Space(8f);

            EditorGUILayout.LabelField("Current Fov: ", currentFov.floatValue.ToString(""));

            GUILayout.Space(8f);

            float _minZoom = EditorGUILayout.FloatField("Min Zoom", minZoom.floatValue);
            float _maxZoom = EditorGUILayout.FloatField("Max Zoom", maxZoom.floatValue);

            minZoom.floatValue = _minZoom;
            maxZoom.floatValue = _maxZoom;

            GUILayout.Space(8f);

            newZoom = EditorGUILayout.FloatField("New Zoom", newZoom);
            duration = EditorGUILayout.FloatField("Duration", duration);

            if(GUILayout.Button("Set Min Zoom"))
            {
                Debug.Log("Setting Min Zoom");
                cameraMovement.SetMinZoom(newZoom, duration);
            }

            if(GUILayout.Button("Set Max Zoom"))
            {
                Debug.Log("Setting Max Zoom");
                cameraMovement.SetMaxZoom(newZoom, duration);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }

#endif

}

