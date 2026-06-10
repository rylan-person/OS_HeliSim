using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class VRCameraRecording : MonoBehaviour
{

    public string fileName = "VRJourneyData.txt";
    private List<TransformData> journeyData = new List<TransformData>();
    private bool isRecording = false;
    private float startTime;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    void Update()
    {
        // Check for keyboard input to start/stop recording (K key)
        if (Input.GetKeyDown(KeyCode.K))
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
            TransformData data = new TransformData(transform.localPosition, transform.localRotation, elapsedTime);
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
        Debug.Log("VR Journey saved to " + Application.persistentDataPath + "/" + fileName);
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
