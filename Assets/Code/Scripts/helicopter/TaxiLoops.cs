using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class TaxiLoops : MonoBehaviour
{
    [SerializeField] private List<TaxiComponenets> helicopters;
    [SerializeField] private List<int> helicopterFrames;
    [SerializeField] private List<float> helictoperStartTimes;
    [SerializeField] private List<float> currentHelicopterTimes;
    [SerializeField] private List<int> helictoperLoops;
    [SerializeField] private float _elapsedTime = 0.0f;
    public string fileName = "TaxiJourney.txt";

    public float playbackSpeed = 1.0f; // You can adjust playback speed if needed

    private List<TransformData> journeyData = new List<TransformData>();
    private float playbackStartTime;
    private bool isPlayingBack = false;

    void Start()
    {
        LoadJourneyFromFile();
    }

    void Update()
    {
        // Check for keyboard input to start/stop playback (M key), pause (space), and skip (left/right arrow)
        if (Input.GetKeyDown(KeyCode.M))
        {
            Debug.Log("Playback toggled");
            if (!isPlayingBack)
            {
                Debug.Log("Playback started");
                StartPlayback();
            }
        }

        if (isPlayingBack)
        {
            PlayJourney(Time.time - playbackStartTime);
        }
    }

    public void StartPlayback()
    {
        // set the size of the helicopterFrames list to the number of helicopters
        helicopterFrames = new List<int>(new int[helicopters.Count]);
        helictoperLoops = new List<int>(new int[helicopters.Count]);
        helictoperStartTimes = new List<float>(new float[helicopters.Count]);
        currentHelicopterTimes = new List<float>(new float[helicopters.Count]);
        if (journeyData.Count > 0)
        {
            for (int i = 0; i < helicopters.Count; i++)
            {
                helicopters[i].helicopterRb.useGravity = false;
                helicopters[i].helicopterRb.isKinematic = true;
                // equally distribute helicopter frames based on the number of helicopters and the length of the journey data
                helicopterFrames[i] = journeyData.Count / helicopters.Count * i;
                // set the start time of that helictoper to be equal to the time of that journey data frame
                helictoperStartTimes[i] = journeyData[helicopterFrames[i]].time;
            }
            playbackStartTime = Time.time;
            isPlayingBack = true;
            Debug.Log("Playback started");
        }
    }

    private void PlayJourney(float elapsedTime)
    {
        _elapsedTime = elapsedTime;
        for (int i = 0; i < helicopters.Count; i++)
        {
            currentHelicopterTimes[i] = journeyData[helicopterFrames[i] + 1].time-helictoperStartTimes[i];
            while (helicopterFrames[i] < journeyData.Count - 1 && journeyData[helicopterFrames[i] + 1].time-helictoperStartTimes[i] <= elapsedTime - helictoperLoops[i]*journeyData[journeyData.Count-1].time)
            {
                helicopterFrames[i]++;
            }

            if (helicopterFrames[i] < journeyData.Count - 1)
            {
                // Interpolate between the current frame and the next one
                TransformData startData = journeyData[helicopterFrames[i]];
                TransformData endData = journeyData[helicopterFrames[i] + 1];

                float lerpFactor = (elapsedTime - startData.time) / (endData.time - startData.time);

                helicopters[i].helicopterTransform.position = Vector3.Lerp(startData.position, endData.position, lerpFactor);
                helicopters[i].helicopterTransform.rotation = Quaternion.Slerp(startData.rotation, endData.rotation, lerpFactor);
            }    
            else 
            {
                helicopterFrames[i] = 0;
                //helictoperStartTimes[i] = 0.0f;
                helictoperLoops[i]+=1;
            }     
        }

    }


    private void LoadJourneyFromFile()
    {
        if (File.Exists(Application.persistentDataPath + "/" + fileName))
        {
            using (StreamReader reader = new StreamReader(Application.persistentDataPath + "/" + fileName))
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
            Debug.Log("Journey loaded from " + fileName);
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
