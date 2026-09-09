using System.Collections;
using System.Collections.Generic;
using System.IO;
using Oyedoyin.Common;
using Oyedoyin.RotaryWing;
using UnityEngine;
using UnityEngine.InputSystem;


public class DataExtractor : MonoBehaviour
{
    public struct HeliData
    {
        public float time;
        public float xJoy_input;
        public float yJoy_input;
        public float pedal_input;
        public float collective_input;
        public Vector3 position;
        public Quaternion rotation;
        public float pitch_rate;
        public float roll_rate;
        public float yaw_rate;
        public float turn_rate;
        public Vector3 velocity;
    }

    public RotaryController rotaryController;
    public SilantroCore heli_core;
    public Transform heli_transform;
    public Vector3 start_position;
    public float xJoy_input;
    public float yJoy_input;
    public float pedal_input;
    public float collective_input;

    public bool isRecording = false;

    public List<HeliData> heliDataList = new List<HeliData>();

    private Rigidbody heli_rigidbody;

    public void OnPitchInput(InputValue value)
    {
        yJoy_input = value.Get<float>();
    }

    public void OnRollInput(InputValue value)
    {
        xJoy_input = value.Get<float>();
    }

    public void OnCollectiveLever(InputValue value)
    {
        collective_input = value.Get<float>();
    }

    // Start
    void Start()
    {
        xJoy_input = 0f;
        yJoy_input = 0f;
        pedal_input = 0f;

        if (heli_transform != null)
        {
            start_position = heli_transform.position;
            heli_rigidbody = heli_transform.GetComponent<Rigidbody>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (isRecording && heli_transform != null && heli_core != null && rotaryController != null)
        {
            HeliData heliData = new HeliData();
            heliData.time = Time.time;
            heliData.xJoy_input = xJoy_input;
            heliData.yJoy_input = yJoy_input;
            heliData.pedal_input = pedal_input;
            heliData.collective_input = rotaryController._collectiveInput;
            heliData.position = heli_transform.position - start_position;
            heliData.rotation = heli_transform.rotation;
            heliData.pitch_rate = (float)heli_core.q;
            heliData.roll_rate = (float)heli_core.p;
            heliData.yaw_rate = (float)heli_core.r;
            heliData.turn_rate = (float)heli_core.ωф;
            heliData.velocity = heli_rigidbody != null ? heli_rigidbody.linearVelocity : Vector3.zero;

            heliDataList.Add(heliData);
        }

        if (Input.GetKeyDown(KeyCode.Comma))
        {
            isRecording = true;
            heliDataList.Clear();
            Debug.Log("Data recording started.");
        }

        // on pressing the '.' key, save the data
        if (Input.GetKeyDown(KeyCode.Period) && isRecording)
        {
            SaveData();
            isRecording = false;
            heliDataList.Clear();
            Debug.Log("Data saved and recording stopped.");
        } 
    }

    public void SaveData()
    {
        // have the file include the date and time
        string directory = Path.Combine(Application.persistentDataPath, "Data");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, System.DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".csv");

        try
        {
            using (StreamWriter writer = new StreamWriter(path, false))
            {
                writer.WriteLine("time,xJoy_input,yJoy_input,pedal_input,collective_input,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,pitch_rate,roll_rate,yaw_rate,turn_rate,vel_x,vel_y,vel_z");
                foreach (HeliData heliData in heliDataList)
                {
                    // Convert quaternion to Euler angles (in degrees)
                    Vector3 euler = heliData.rotation.eulerAngles;
                    writer.WriteLine(
                        $"{heliData.time},{heliData.xJoy_input},{heliData.yJoy_input},{heliData.pedal_input},{heliData.collective_input}," +
                        $"{heliData.position.x},{heliData.position.y},{heliData.position.z}," +
                        $"{euler.x},{euler.y},{euler.z}," +
                        $"{heliData.pitch_rate},{heliData.roll_rate},{heliData.yaw_rate},{heliData.turn_rate}," +
                        $"{heliData.velocity.x},{heliData.velocity.y},{heliData.velocity.z}"
                    );
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"DataExtractor: failed to save recording to '{path}': {ex}");
        }
    }

}
