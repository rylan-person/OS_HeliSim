using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class VRCameraPlayback : MonoBehaviour
{
    public string fileName = "VRJourneyData.txt";
    public float playbackSpeed = 1.0f;
    public float smoothingFactor = 0.1f; // Adjust for smoother motion
    private List<TransformData> journeyData = new List<TransformData>();
    private bool isPlayingBack = false;
    private bool isPaused = false;
    private int currentFrame = 0;
    private float frameRate = 30f; // Assuming 30 FPS
    private float playbackStartTime;

    void Start()
    {
        LoadJourneyFromFile();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            if (isPlayingBack)
                StopPlayback();
            else
                StartPlayback();
        }

        if (Input.GetKeyDown(KeyCode.Space))
            TogglePause();

        if (Input.GetKeyDown(KeyCode.R))
            RestartPlayback();

        if (Input.GetKeyDown(KeyCode.RightArrow))
            SkipFrames(10);

        if (Input.GetKeyDown(KeyCode.LeftArrow))
            SkipFrames(-10);

        if (isPlayingBack && !isPaused)
            PlayJourney(Time.time - playbackStartTime);
    }

    public void StartPlayback()
    {
        if (journeyData.Count > 0)
        {
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
            currentFrame++;

        if (currentFrame < journeyData.Count - 1)
        {
            TransformData startData = journeyData[currentFrame];
            TransformData endData = journeyData[currentFrame + 1];

            float lerpFactor = (elapsedTime - startData.time) / (endData.time - startData.time);
            Vector3 targetPosition = Vector3.Lerp(startData.position, endData.position, lerpFactor);
            Quaternion targetRotation = Quaternion.Slerp(startData.rotation, endData.rotation, lerpFactor);

            // Apply smoothing to the transition
            transform.localPosition = Vector3.Lerp(transform.localPosition, targetPosition, smoothingFactor);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetRotation, smoothingFactor);
        }
    }

    private void LoadJourneyFromFile()
    {
        string path = Application.persistentDataPath + "/" + fileName;
        if (File.Exists(path))
        {
            using (StreamReader reader = new StreamReader(path))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] parts = line.Split(';');
                    string[] posParts = parts[0].Split(',');
                    string[] rotParts = parts[1].Split(',');
                    float time = float.Parse(parts[2]);

                    Vector3 position = new Vector3(float.Parse(posParts[0]), float.Parse(posParts[1]), float.Parse(posParts[2]));
                    Quaternion rotation = new Quaternion(float.Parse(rotParts[0]), float.Parse(rotParts[1]), float.Parse(rotParts[2]), float.Parse(rotParts[3]));

                    journeyData.Add(new TransformData(position, rotation, time));
                }
            }
            Debug.Log("VR Journey loaded from " + fileName);
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
