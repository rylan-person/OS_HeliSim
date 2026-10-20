using System;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class MenuRecordingLifecycleTests
{
    [TestCase(typeof(HelicopterRecorder))]
    [TestCase(typeof(VRCameraRecording))]
    public void DisablingActiveJourneyRecorder_SavesOnceBeforeDestruction(Type type)
    {
        var root = new GameObject("RecordingLifecycleTest");
        string name = "menu-recording-test-" + Guid.NewGuid().ToString("N") + ".txt";
        string path = Path.Combine(Application.persistentDataPath, name);
        try
        {
            var recorder = root.AddComponent(type);
            type.GetField("fileName").SetValue(recorder, name);
            type.GetMethod("StartRecording").Invoke(recorder, null);
            type.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(recorder, null);
            Assert.That(File.Exists(path), Is.True, "Disconnect teardown must flush active buffers.");
            Assert.That(type.GetProperty("IsRecording").GetValue(recorder), Is.False);
            File.WriteAllText(path, "already saved");
            type.GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(recorder, null);
            Assert.That(File.ReadAllText(path), Is.EqualTo("already saved"), "A repeated teardown must not overwrite the recording.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public void FailedJourneySave_KeepsRecordingBufferAvailableForRetry()
    {
        var root = new GameObject("RecordingFailureTest");
        try
        {
            var recorder = root.AddComponent<HelicopterRecorder>();
            recorder.fileName = "missing-" + Guid.NewGuid().ToString("N") + "/recording.txt";
            recorder.StartRecording();
            Assert.Throws<DirectoryNotFoundException>(() => recorder.StopRecording());
            Assert.That(recorder.IsRecording, Is.True);
            // End the fixture without causing the finalization hook to retry its expected failure.
            typeof(HelicopterRecorder).GetField("isRecording", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(recorder, false);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void DisablingCsvRecorder_SavesItsBufferOnceAndPreservesTheColumnSchema()
    {
        string directory = Path.Combine(Application.persistentDataPath, "Data");
        string[] before = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.csv") : Array.Empty<string>();
        var root = new GameObject("CsvRecordingLifecycleTest");
        string[] created = Array.Empty<string>();
        try
        {
            var recorder = root.AddComponent<DataExtractor>();
            recorder.heliDataList.Add(new DataExtractor.HeliData { time = 0.125f });
            recorder.isRecording = true;
            var disable = typeof(DataExtractor).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
            disable.Invoke(recorder, null);
            created = Directory.GetFiles(directory, "*.csv").Except(before).ToArray();
            Assert.That(created.Length, Is.EqualTo(1));
            string[] lines = File.ReadAllLines(created[0]);
            Assert.That(lines.Length, Is.EqualTo(2));
            Assert.That(lines[0], Is.EqualTo("time,xJoy_input,yJoy_input,pedal_input,collective_input,pos_x,pos_y,pos_z,rot_x,rot_y,rot_z,pitch_rate,roll_rate,yaw_rate,turn_rate,vel_x,vel_y,vel_z"));
            Assert.That(recorder.isRecording, Is.False);
            disable.Invoke(recorder, null);
            Assert.That(Directory.GetFiles(directory, "*.csv").Except(before).Count(), Is.EqualTo(1));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            foreach (string path in created) File.Delete(path);
        }
    }
}
