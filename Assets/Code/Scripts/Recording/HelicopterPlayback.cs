using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class HelicopterPlayback : MonoBehaviour
{
    [SerializeField] private HelicopterComponents helicopterComponents;
    public float playbackSpeed = 1.0f; // You can adjust playback speed if needed
    private List<TransformData> journeyData = new List<TransformData>();
    private bool isPlayingBack = false;
    private bool isPaused = false;
    private int currentFrame = 0;
    private float frameRate = 30f; // Assuming 30 FPS
    private float frameTime;
    private float playbackStartTime;

    public Transform mainRotor;   // Assign the main rotor transform in the inspector
    public Transform tailRotor;   // Assign the tail rotor transform in the inspector
    public float rotorRPM = 600f; // Set the desired rotor RPM
    public float waitTime = 0f;

    void Start()
    {
        frameTime = 1f / frameRate;
        LoadJourneyFromFile();
    }

    void OnDestroy()
    {
        // StartPlayback() kicks off WaitDelay() as a fire-and-forget coroutine; make sure it
        // doesn't keep running (or throw on a destroyed object) if this component is removed.
        StopAllCoroutines();
    }

    void Update()
    {
        // Check for keyboard input to start/stop playback (M key), pause (space), and skip (left/right arrow)
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (isPlayingBack)
            {
                StopPlayback();
            }
            else
            {
                StartPlayback();
            }
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            TogglePause();
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartPlayback();
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            SkipFrames(10); // Skip forward 10 seconds
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            SkipFrames(-10); // Skip back 10 seconds
        }

        if (isPlayingBack && !isPaused)
        {
            PlayJourney(Time.time - playbackStartTime);
        }
    }

    public void StartPlayback()
    {
        StartCoroutine(WaitDelay());

    }

    IEnumerator WaitDelay()
    {
        yield return new WaitForSeconds(waitTime);
        
        if (journeyData.Count > 0)
        {
            if (helicopterComponents == null || helicopterComponents.helicopterRb == null)
            {
                Debug.LogError($"{nameof(HelicopterPlayback)}: helicopterComponents (or its Rigidbody) is not assigned; cannot start playback.");
                yield break;
            }

            helicopterComponents.helicopterRb.useGravity = false;
            helicopterComponents.helicopterRb.isKinematic = true;
            playbackStartTime = Time.time;
            currentFrame = 0;
            isPlayingBack = true;
            isPaused = false;
            Debug.Log("Playback started");
        }
    }

    public void StopPlayback()
    {
        isPlayingBack = false;
        Debug.Log("Playback stopped");
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Debug.Log(isPaused ? "Playback paused" : "Playback resumed");
    }

    public void RestartPlayback()
    {
        if (journeyData.Count > 0)
        {
            currentFrame = 0;
            isPaused = false;
            playbackStartTime = Time.time;
            Debug.Log("Playback restarted");
        }
    }

    public void SkipFrames(float seconds)
    {
        int framesToSkip = Mathf.RoundToInt(seconds * frameRate);
        currentFrame = Mathf.Clamp(currentFrame + framesToSkip, 0, journeyData.Count - 1);
        Debug.Log("Skipped to frame " + currentFrame);
    }

    private void PlayJourney(float elapsedTime)
    {
        while (currentFrame < journeyData.Count - 1 && journeyData[currentFrame + 1].time <= elapsedTime)
        {
            currentFrame++;
        }

        if (currentFrame < journeyData.Count - 1)
        {
            // Interpolate between the current frame and the next one
            TransformData startData = journeyData[currentFrame];
            TransformData endData = journeyData[currentFrame + 1];

            float lerpFactor = (elapsedTime - startData.time) / (endData.time - startData.time);

            transform.position = Vector3.Lerp(startData.position, endData.position, lerpFactor);
            transform.rotation = Quaternion.Slerp(startData.rotation, endData.rotation, lerpFactor);
        }

        // Spin the rotors at a constant RPM
        RotateRotors();
    }

    private void RotateRotors()
    {
        // Calculate the rotation speed in degrees per frame based on the RPM
        float rotationSpeed = rotorRPM * 6f * Time.deltaTime; // 6 degrees per RPM unit to convert to degrees/frame

        // Rotate the main rotor and the tail rotor
        if (mainRotor != null)
        {
            mainRotor.Rotate(Vector3.forward, rotationSpeed); // Spin the main rotor around the up (Y) axis
        }

        if (tailRotor != null)
        {
            tailRotor.Rotate(Vector3.right, rotationSpeed); // Spin the tail rotor around the right (X) axis
        }
    }

    private void LoadJourneyFromFile()
    {
        string settingsPath = Path.Combine(Application.persistentDataPath, "Settings", "playbackName.txt");
        string fileName = "";


        if (File.Exists(settingsPath))
        {
            fileName = File.ReadAllText(settingsPath).Trim();
            Debug.Log("Settings file found: " + settingsPath + ", fileName: " + fileName);
        }
        else
        {
            Debug.LogError("Settings file not found: " + settingsPath);
            return;
        }

        string journeyPath = Path.Combine(Application.persistentDataPath, fileName);
        if (!File.Exists(journeyPath))
        {
            Debug.LogWarning($"{nameof(HelicopterPlayback)}: journey file not found at '{journeyPath}'.");
            return;
        }

        try
        {
            using (StreamReader reader = new StreamReader(journeyPath))
            {
                string line;
                int lineNumber = 0;
                while ((line = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    string[] parts = line.Split(';');
                    if (parts.Length < 3)
                    {
                        Debug.LogWarning($"{nameof(HelicopterPlayback)}: skipping malformed line {lineNumber} in '{journeyPath}'.");
                        continue;
                    }

                    string[] posParts = parts[0].Split(',');
                    string[] rotParts = parts[1].Split(',');

                    if (posParts.Length < 3 || rotParts.Length < 4 ||
                        !float.TryParse(parts[2], out float time) ||
                        !float.TryParse(posParts[0], out float posX) || !float.TryParse(posParts[1], out float posY) || !float.TryParse(posParts[2], out float posZ) ||
                        !float.TryParse(rotParts[0], out float rotX) || !float.TryParse(rotParts[1], out float rotY) || !float.TryParse(rotParts[2], out float rotZ) || !float.TryParse(rotParts[3], out float rotW))
                    {
                        Debug.LogWarning($"{nameof(HelicopterPlayback)}: skipping unparsable line {lineNumber} in '{journeyPath}'.");
                        continue;
                    }

                    Vector3 position = new Vector3(posX, posY, posZ);
                    Quaternion rotation = new Quaternion(rotX, rotY, rotZ, rotW);

                    journeyData.Add(new TransformData(position, rotation, time));
                }
            }
            Debug.Log("Journey loaded from " + fileName);
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"{nameof(HelicopterPlayback)}: failed to load journey from '{journeyPath}': {ex}");
        }
    }

    [System.Serializable]
    private class TransformData
    {
        public Vector3 position;
        public Quaternion rotation;
        public float time;

        public TransformData(Vector3 pos, Quaternion rot, float t)
        {
            position = pos;
            rotation = rot;
            time = t;
        }
    }
}
