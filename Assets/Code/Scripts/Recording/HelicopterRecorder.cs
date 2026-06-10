using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

public class HelicopterRecorder : MonoBehaviour
{
    public string fileName = "HelicopterJourney.txt";
    private List<TransformData> journeyData = new List<TransformData>();
    private bool isRecording = false;
    private float startTime;

    void Update()
    {
        // Check for keyboard input to start/stop recording (R key)
        if (Input.GetKeyDown(KeyCode.N))
        {
            if (isRecording)
            {
                StopRecording();
            }
            else
            {
                StartRecording();
            }
        }

        if (isRecording)
        {
            // Record position, rotation, and time since start
            float elapsedTime = Time.time - startTime;
            TransformData data = new TransformData(transform.position, transform.rotation, elapsedTime);
            journeyData.Add(data);
        }
    }

    public void StartRecording()
    {
        isRecording = true;
        journeyData.Clear();  // Clear previous journey data
        startTime = Time.time; // Record the starting time
        Debug.Log("Recording started");
    }

    public void StopRecording()
    {
        isRecording = false;
        SaveJourneyToFile();
        Debug.Log("Recording stopped");
    }

    private void SaveJourneyToFile()
    {
        using (StreamWriter writer = new StreamWriter(Application.persistentDataPath + "/" + fileName, false))
        {
            foreach (TransformData data in journeyData)
            {
                writer.WriteLine($"{data.position.x},{data.position.y},{data.position.z};{data.rotation.x},{data.rotation.y},{data.rotation.z},{data.rotation.w};{data.time}");
            }
        }
        Debug.Log("Journey saved to " + Application.persistentDataPath + "/" + fileName);
    }

    [System.Serializable]
    private class TransformData
    {
        public Vector3 position;
        public Quaternion rotation;
        public float time; // Time since recording started

        public TransformData(Vector3 pos, Quaternion rot, float t)
        {
            position = pos;
            rotation = rot;
            time = t;
        }
    }
}
