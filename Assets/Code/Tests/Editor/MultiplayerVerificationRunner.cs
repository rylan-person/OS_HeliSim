using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// One runner for the connected Editor; persists its callback across test domain reloads.
[InitializeOnLoad]
public static class MultiplayerVerificationRunner
{
    public const string AuthoringSetupKey = "HeliSim.MultiplayerVerification.AuthoringSetup";
    [Serializable] private class AuthoringSetup { public SceneRecord[] scenes; }
    [Serializable] private class SceneRecord { public string path; public bool isLoaded; public bool isActive; }
    private const string OutputKey = "HeliSim.MultiplayerVerification.Output";
    private static readonly Results callbacks = new Results();

    static MultiplayerVerificationRunner()
    {
        TestRunnerApi.RegisterTestCallback(callbacks);
    }

    public static void Discover(string path)
    {
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.RetrieveTestList(TestMode.EditMode, root =>
        {
            var names = new List<string>();
            Collect(root, names);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllLines(path, names);
            UnityEngine.Object.DestroyImmediate(api);
        });
    }

    private static void Collect(ITestAdaptor test, List<string> names)
    {
        if (!test.IsSuite) names.Add(test.FullName);
        foreach (var child in test.Children) Collect(child, names);
    }

    public static void Run(string output, string[] names)
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer)
            throw new InvalidOperationException("Editor is busy.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                throw new InvalidOperationException("Save authoring scenes before verification.");
        }
        if (!string.IsNullOrEmpty(SessionState.GetString(OutputKey, "")))
            throw new InvalidOperationException("A verification run is already active.");
        var setup = UnityEditor.SceneManagement.EditorSceneManager.GetSceneManagerSetup();
        var records = new SceneRecord[setup.Length];
        for (int i = 0; i < setup.Length; i++)
            records[i] = new SceneRecord { path = setup[i].path, isLoaded = setup[i].isLoaded, isActive = setup[i].isActive };
        SessionState.SetString(AuthoringSetupKey, JsonUtility.ToJson(new AuthoringSetup { scenes = records }));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        SessionState.SetString(OutputKey, output);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names }));
    }

    private class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            var output = SessionState.GetString(OutputKey, "");
            if (string.IsNullOrEmpty(output)) return;
            try { TestRunnerApi.SaveResultToFile(result, output); }
            finally { SessionState.EraseString(OutputKey); SessionState.EraseString(AuthoringSetupKey); }
        }
    }
}
