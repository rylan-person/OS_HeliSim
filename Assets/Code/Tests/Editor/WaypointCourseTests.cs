#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class WaypointCourseTests
{
    private const string CoursePath = "Assets/Level/Prefabs/WaypointsNew.prefab";
    private GameObject root;
    private WaypointManager manager;
    private WaypointManager courseAsset;
    private GameObject[] gates;
    private Waypoint[] checkpoints;
    private Collider helicopterCollider;
    private string outputPath;

    [SetUp]
    public void SetUp()
    {
        root = new GameObject("Isolated waypoint course tests");
        GameObject managerObject = new GameObject("Inactive test manager");
        managerObject.transform.SetParent(root.transform);
        managerObject.SetActive(false);
        // Keeping this component inactive prevents Awake/Start from claiming the singleton
        // or reading the user's persistent LapTimes.txt. Exercise public course APIs below.
        manager = managerObject.AddComponent<WaypointManager>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CoursePath);
        Assert.That(prefab, Is.Not.Null, CoursePath);
        courseAsset = prefab.GetComponentInChildren<WaypointManager>(true);
        Assert.That(courseAsset, Is.Not.Null);
        GameObject[] sourceGates = Get<GameObject[]>(courseAsset, "waypoints");
        gates = new GameObject[sourceGates.Length];
        checkpoints = new Waypoint[gates.Length];
        for (int i = 0; i < gates.Length; i++)
        {
            gates[i] = UnityEngine.Object.Instantiate(sourceGates[i], root.transform);
            gates[i].SetActive(true);
            checkpoints[i] = gates[i].GetComponentInChildren<Waypoint>(true);
            Assert.That(checkpoints[i], Is.Not.Null, $"Course gate {i + 1}");
            Set(checkpoints[i], "waypointManager", manager);
            checkpoints[i].waypointNumber = i + 1; // Same initialization as manager.Awake.
        }

        GameObject helicopter = new GameObject("Bound helicopter collider");
        helicopter.transform.SetParent(root.transform);
        helicopter.tag = "Helicopter";
        helicopterCollider = helicopter.AddComponent<BoxCollider>();
        manager.helicopterTransform = helicopter.transform;
        manager._sectors = courseAsset._sectors;
        Set(manager, "waypoints", gates);
        Set(manager, "timingManagers", Array.Empty<Timing>());
        Set(manager, "averageSectors", Filled(0f));
        Set(manager, "bestSectors", Filled(float.MaxValue));
        Set(manager, "bestLapSectors", Filled(float.MaxValue));
        outputPath = Path.Combine(Application.temporaryCachePath, $"course-test-{Guid.NewGuid():N}.csv");
        Set(manager, "filePath", outputPath);
        manager.resetLap();
    }

    [TearDown]
    public void TearDown()
    {
        if (manager != null && manager.transform.parent == null) UnityEngine.Object.DestroyImmediate(manager.gameObject);
        if (root != null) UnityEngine.Object.DestroyImmediate(root);
        if (outputPath != null && File.Exists(outputPath)) File.Delete(outputPath);
    }

    [Test]
    public void ShippedCourse_HasOrderedSectorEndpointsAndWiredTriggerGeometry()
    {
        GameObject[] sourceGates = Get<GameObject[]>(courseAsset, "waypoints");
        Assert.That(sourceGates, Has.Length.EqualTo(15));
        Assert.That(courseAsset._sectors, Is.EqualTo(5));
        int expectedSector = 1;
        for (int i = 0; i < sourceGates.Length; i++)
        {
            Waypoint checkpoint = sourceGates[i].GetComponentInChildren<Waypoint>(true);
            Assert.That(Get<WaypointManager>(checkpoint, "waypointManager"), Is.SameAs(courseAsset));
            GameObject[] fills = Get<GameObject[]>(checkpoint, "checkpointFills");
            Assert.That(fills, Has.Length.EqualTo(2));
            Assert.That(fills[0], Is.Not.Null);
            Assert.That(fills[1], Is.Not.Null);
            MeshCollider trigger = checkpoint.GetComponent<MeshCollider>();
            Assert.That(trigger, Is.Not.Null, $"Gate {i + 1} needs a collider beside its callback");
            Assert.That(trigger.enabled && trigger.isTrigger && trigger.convex, Is.True);
            Assert.That(trigger.sharedMesh, Is.Not.Null);
            int sector = Get<int>(checkpoint, "waypointSectorEnd");
            if (sector != -1) Assert.That(sector, Is.EqualTo(expectedSector++));
        }
        Assert.That(expectedSector, Is.EqualTo(manager._sectors + 1));
        Assert.That(Get<int>(checkpoints[checkpoints.Length - 1], "waypointSectorEnd"), Is.EqualTo(manager._sectors));
    }

    [Test]
    public void GateCallbacks_RejectWrongOrderDuplicateAndUnboundOrUntaggedColliders()
    {
        manager.lapActive = true;
        int endpoint = Array.FindIndex(checkpoints, checkpoint => Get<int>(checkpoint, "waypointSectorEnd") == 1);
        Hit(checkpoints[endpoint], helicopterCollider);
        Assert.That(Get<int>(manager, "currentWaypoint"), Is.Zero);
        Assert.That(Get<int>(manager, "currentSector"), Is.Zero);

        GameObject stranger = new GameObject("Unbound tagged helicopter");
        stranger.transform.SetParent(root.transform);
        stranger.tag = "Helicopter";
        Hit(checkpoints[0], stranger.AddComponent<BoxCollider>());
        helicopterCollider.tag = "Untagged";
        Hit(checkpoints[0], helicopterCollider);
        Assert.That(Get<int>(manager, "currentWaypoint"), Is.Zero);

        helicopterCollider.tag = "Helicopter";
        Hit(checkpoints[0], helicopterCollider);
        Hit(checkpoints[0], helicopterCollider);
        Assert.That(Get<int>(manager, "currentWaypoint"), Is.EqualTo(1));
        Assert.That(manager.finishWaypoint(gates.Length + 1), Is.False);
    }

    [Test]
    public void OrderedCourse_AccumulatesSectorTimesAndCompletesExactlyOneLap()
    {
        CompleteLap(2f);
        Assert.That(Get<int>(manager, "currentWaypoint"), Is.EqualTo(gates.Length));
        Assert.That(Get<int>(manager, "currentSector"), Is.EqualTo(manager._sectors));
        Assert.That(manager.lapActive, Is.False);
        Assert.That(manager.goLock, Is.True);
        Assert.That(Get<List<float>>(manager, "lapTimes"), Is.EqualTo(new[] { gates.Length * 2f }));
        Assert.That(Sum(manager.currentLapSectors), Is.EqualTo(gates.Length * 2f));
        Assert.That(Get<float>(manager, "bestLap"), Is.EqualTo(gates.Length * 2f));
        Assert.That(Get<float>(manager, "lastLapDiffTime"), Is.Zero);
        Assert.That(Get<float>(manager, "optLapTime"), Is.EqualTo(gates.Length * 2f));
        Assert.That(File.Exists(outputPath), Is.True, "Completed lap should persist only to the fixture path");
        Hit(checkpoints[checkpoints.Length - 1], helicopterCollider);
        Assert.That(Get<List<float>>(manager, "lapTimes"), Has.Count.EqualTo(1));
        float bestFirstSector = Get<List<float>>(manager, "bestLapSectors")[0];
        manager.currentLapSectors[0] += 1f;
        Assert.That(Get<List<float>>(manager, "bestLapSectors")[0], Is.EqualTo(bestFirstSector),
            "Editing the current lap buffer must not mutate the saved best lap snapshot");
    }

    [Test]
    public void ResetAfterProgress_RestoresInactiveGateFillsAndClearsPartialTiming()
    {
        manager.lapActive = true;
        for (int i = 0; i < 7; i++) Hit(checkpoints[i], helicopterCollider);
        Set(manager, "currentLapTime", 17f);
        Set(manager, "currentSectorTime", 3f);
        manager.resetLap();
        Assert.That(Get<int>(manager, "currentWaypoint"), Is.Zero);
        Assert.That(Get<int>(manager, "currentSector"), Is.Zero);
        Assert.That(Get<float>(manager, "currentLapTime"), Is.Zero);
        Assert.That(Get<float>(manager, "currentSectorTime"), Is.Zero);
        Assert.That(manager.currentLapSectors, Has.All.EqualTo(0f));
        Assert.That(manager.lapActive, Is.False);
        Assert.That(Get<List<float>>(manager, "lapTimes"), Is.Empty);
        for (int i = 0; i < gates.Length; i++)
        {
            Assert.That(gates[i].activeSelf, Is.EqualTo(i < 3), $"Visibility of gate {i + 1}");
            GameObject[] fills = Get<GameObject[]>(checkpoints[i], "checkpointFills");
            Assert.That(fills[0].activeSelf, Is.EqualTo(i == 0), $"Next fill of gate {i + 1}");
            Assert.That(fills[1].activeSelf, Is.EqualTo(i != 0), $"Pending fill of gate {i + 1}");
        }
    }

    [Test]
    public void SlowerSecondLap_PreservesBestLapSectorsAndComputesAverageAndDelta()
    {
        CompleteLap(2f);
        float[] best = Get<List<float>>(manager, "bestLapSectors").ToArray();
        manager.resetLap();
        CompleteLap(3f);
        Assert.That(Get<List<float>>(manager, "bestLapSectors"), Is.EqualTo(best));
        Assert.That(Get<List<float>>(manager, "bestSectors"), Is.EqualTo(best));
        Assert.That(Get<List<float>>(manager, "bestLapSectors"), Is.Not.SameAs(manager.currentLapSectors));
        Assert.That(Get<float>(manager, "bestLap"), Is.EqualTo(gates.Length * 2f));
        Assert.That(Get<float>(manager, "lastLapDiffTime"), Is.EqualTo(gates.Length));
        Assert.That(Get<float>(manager, "averageLap"), Is.EqualTo(gates.Length * 2.5f));
        Assert.That(Get<bool>(manager, "lastLapWasNewBest"), Is.False);
        List<float> averages = Get<List<float>>(manager, "averageSectors");
        for (int i = 0; i < best.Length; i++) Assert.That(averages[i], Is.EqualTo(best[i] * 1.25f));
    }

    [Test]
    public void FasterSecondLap_ReplacesBestLapAndSectorRecords()
    {
        CompleteLap(2f);
        manager.resetLap();
        CompleteLap(1f);
        Assert.That(Get<float>(manager, "bestLap"), Is.EqualTo(gates.Length));
        Assert.That(Get<List<float>>(manager, "bestLapSectors"), Is.EqualTo(manager.currentLapSectors));
        Assert.That(Get<List<float>>(manager, "bestSectors"), Is.EqualTo(manager.currentLapSectors));
        Assert.That(Get<float>(manager, "lastLapDiffTime"), Is.EqualTo(-gates.Length));
        Assert.That(Get<float>(manager, "averageLap"), Is.EqualTo(gates.Length * 1.5f));
        Assert.That(Get<bool>(manager, "lastLapWasNewBest"), Is.True);
    }

    private void CompleteLap(float secondsPerGate)
    {
        manager.lapActive = true;
        manager.goLock = false; // MainMenu clears this after repositioning for another flight.
        for (int i = 0; i < checkpoints.Length; i++)
        {
            Set(manager, "currentLapTime", Get<float>(manager, "currentLapTime") + secondsPerGate);
            Set(manager, "currentSectorTime", Get<float>(manager, "currentSectorTime") + secondsPerGate);
            Hit(checkpoints[i], helicopterCollider);
        }
    }

    public IEnumerator FlyShippedColliderThroughEveryGate()
    {
        Scene physicsScene = SceneManager.CreateScene("Isolated course physics", new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        SceneManager.MoveGameObjectToScene(root, physicsScene);
        PhysicsScene physics = physicsScene.GetPhysicsScene();
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Level/Prefabs/Helicopters/R66_Default.prefab");
        Assert.That(source, Is.Not.Null);
        GameObject bodyObject = new GameObject("R66 physics only");
        bodyObject.transform.SetParent(root.transform);
        // Copy only native body/collider components, never instantiate flight, XR,
        // network or NOVA behaviours. Preserve each shipped collider tag and shape.
        CopyColliderHierarchy(source.transform, bodyObject.transform);
        Rigidbody body = bodyObject.AddComponent<Rigidbody>();
        EditorUtility.CopySerialized(source.GetComponent<Rigidbody>(), body);
        body.useGravity = false;
        body.isKinematic = true;
        body.position = new Vector3(10000f, 10000f, 10000f);
        physics.Simulate(0.02f);
        Assert.That(bodyObject.GetComponent<CapsuleCollider>(), Is.Not.Null);
        Assert.That(bodyObject.transform.Find("RotorCollider").GetComponent<MeshCollider>(), Is.Not.Null);
        manager.helicopterTransform = body.transform;
        manager.LapTimesFilePathOverride = outputPath;
        Set(manager, "autoRegisterTimingManagersOnStart", false);
        // Awake/Start and Update run normally. Only the manager leaves the local
        // physics scene for its normal DontDestroyOnLoad singleton lifetime.
        manager.transform.SetParent(null);
        LogAssert.Expect(LogType.Warning, "Lap times file not found. Initializing default values.");
        manager.gameObject.SetActive(true);
        yield return null;
        Assert.That(Get<string>(manager, "filePath"), Is.EqualTo(outputPath));
        Assert.That(manager.lapActive, Is.False);
        body.position += Vector3.right * 4f;
        // Local physics scenes are not advanced by the player's automatic physics
        // loop. Publish the Rigidbody pose to its Transform before Update sees it.
        physics.Simulate(0.02f);
        Assert.That(Vector3.Distance(body.transform.position, Get<Vector3>(manager, "startingPosition")), Is.GreaterThan(3f));
        for (int frame = 0; frame < 10 && !manager.lapActive; frame++) yield return null;
        Assert.That(manager.lapActive, Is.True, "Normal Update should start timing after leaving the launch position");
        float startedAt = Get<float>(manager, "currentLapTime");
        for (int frame = 0; frame < 10 && Get<float>(manager, "currentLapTime") <= startedAt; frame++) yield return null;
        Assert.That(Get<float>(manager, "currentLapTime"), Is.GreaterThan(startedAt));

        // Gate 2 is visible initially but must not advance progress before gate 1.
        yield return CrossGate(body, checkpoints[1], physics);
        Assert.That(Get<int>(manager, "currentWaypoint"), Is.Zero);
        Assert.That(Get<int>(manager, "currentSector"), Is.Zero);

        int expectedSector = 0;
        for (int i = 0; i < checkpoints.Length; i++)
        {
            float elapsedBefore = Get<float>(manager, "currentLapTime");
            yield return CrossGate(body, checkpoints[i], physics);
            Assert.That(Get<int>(manager, "currentWaypoint"), Is.EqualTo(i + 1), $"Physical crossing of gate {i + 1}");
            if (Get<int>(checkpoints[i], "waypointSectorEnd") != -1) expectedSector++;
            Assert.That(Get<int>(manager, "currentSector"), Is.EqualTo(expectedSector), $"Sector at gate {i + 1}");
            Assert.That(Get<float>(manager, "currentLapTime"), Is.GreaterThanOrEqualTo(elapsedBefore));
        }
        Assert.That(manager.lapActive, Is.False);
        Assert.That(manager.goLock, Is.True);
        Assert.That(Get<List<float>>(manager, "lapTimes"), Has.Count.EqualTo(1));
        Assert.That(manager.currentLapSectors, Has.All.GreaterThan(0f));
        float completedLap = Get<List<float>>(manager, "lapTimes")[0];
        Assert.That(Sum(manager.currentLapSectors), Is.EqualTo(completedLap).Within(0.01f));
        Assert.That(Get<float>(manager, "bestLap"), Is.EqualTo(completedLap));
        Assert.That(File.Exists(outputPath), Is.True);
        yield return CrossGate(body, checkpoints[checkpoints.Length - 1], physics);
        Assert.That(Get<List<float>>(manager, "lapTimes"), Has.Count.EqualTo(1));
        UnityEngine.Object.DestroyImmediate(root);
        root = null;
        yield return SceneManager.UnloadSceneAsync(physicsScene);
    }

    private static IEnumerator CrossGate(Rigidbody body, Waypoint checkpoint, PhysicsScene physics)
    {
        // The shipped mesh is Unity's XZ plane: its transformed local Y is the
        // crossing direction. Move in small steps through the volume, rather than
        // teleporting over a thin gate or invoking OnTriggerEnter directly.
        Vector3 center = checkpoint.GetComponent<Collider>().bounds.center;
        Vector3 normal = checkpoint.transform.up;
        body.position = center - normal * 15f;
        physics.Simulate(0.02f);
        yield return null;
        for (int step = 1; step <= 30; step++)
        {
            body.position = center + normal * (step - 15f);
            physics.Simulate(0.02f);
            yield return null;
        }
    }

    private static void CopyColliderHierarchy(Transform source, Transform target)
    {
        target.gameObject.layer = source.gameObject.layer;
        target.gameObject.tag = source.gameObject.tag;
        foreach (Collider collider in source.GetComponents<Collider>())
        {
            if (!collider.enabled || !collider.CompareTag("Helicopter")) continue;
            Collider copy = (Collider)target.gameObject.AddComponent(collider.GetType());
            EditorUtility.CopySerialized(collider, copy);
        }
        foreach (Transform child in source)
        {
            GameObject copy = new GameObject(child.name);
            copy.transform.SetParent(target, false);
            copy.transform.localPosition = child.localPosition;
            copy.transform.localRotation = child.localRotation;
            copy.transform.localScale = child.localScale;
            CopyColliderHierarchy(child, copy.transform);
            copy.SetActive(child.gameObject.activeSelf);
        }
    }

    private List<float> Filled(float value)
    {
        var values = new List<float>();
        for (int i = 0; i < manager._sectors; i++) values.Add(value);
        return values;
    }

    private static float Sum(List<float> values)
    {
        float total = 0f;
        foreach (float value in values) total += value;
        return total;
    }

    // Callback dispatch isolates routing/order from frame scheduling. The asset test
    // checks the shipped trigger wiring; this does not claim a PhysX crossing test.
    private static void Hit(Waypoint checkpoint, Collider collider)
    {
        typeof(Waypoint).GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(checkpoint, new object[] { collider });
    }

    private static T Get<T>(object target, string name)
    {
        return (T)Field(target, name).GetValue(target);
    }

    private static void Set(object target, string name, object value)
    {
        Field(target, name).SetValue(target, value);
    }

    private static FieldInfo Field(object target, string name)
    {
        FieldInfo field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        return field;
    }
}

// Select from the EditMode runner: this fixture owns entering/exiting Play mode.
public class WaypointCoursePhysicsTests
{
    private const string SetupKey = "HeliSim.WaypointCoursePhysicsTests.Setup";
    private const string FolderKey = "HeliSim.WaypointCoursePhysicsTests.Folder";
    private WaypointCourseTests course;

    [Serializable]
    private class SavedSetup
    {
        public SavedScene[] scenes;
    }

    [Serializable]
    private class SavedScene
    {
        public string path;
        public bool isLoaded;
        public bool isActive;
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Assert.That(EditorApplication.isPlaying, Is.False, "Use the EditMode runner");
        Assert.That(SessionState.GetString(FolderKey, ""), Is.Empty, "Previous course test needs cleanup");
        string authoringSetup = SessionState.GetString(MultiplayerVerificationRunner.AuthoringSetupKey, "");
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            Assert.That(scene.isDirty, Is.False, "Preserve unsaved authoring before course physics verification");
            if (string.IsNullOrEmpty(scene.path))
                Assert.That(authoringSetup, Is.Not.Empty, "Use MultiplayerVerificationRunner to retain authoring scene setup");
        }
        if (string.IsNullOrEmpty(authoringSetup))
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            var saved = new SavedScene[setup.Length];
            for (int i = 0; i < setup.Length; i++)
                saved[i] = new SavedScene { path = setup[i].path, isLoaded = setup[i].isLoaded, isActive = setup[i].isActive };
            authoringSetup = JsonUtility.ToJson(new SavedSetup { scenes = saved });
        }
        SessionState.SetString(SetupKey, authoringSetup);
        string folder = "Assets/__WaypointCoursePhysics_" + Guid.NewGuid().ToString("N");
        SessionState.SetString(FolderKey, folder);
        AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
        Scene isolatedScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Assert.That(EditorSceneManager.SaveScene(isolatedScene, folder + "/Isolated.unity"), Is.True);
        yield return new EnterPlayMode();
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (EditorApplication.isPlaying)
        {
            if (course != null) course.TearDown();
            yield return new ExitPlayMode();
        }
        string saved = SessionState.GetString(SetupKey, "");
        string folder = SessionState.GetString(FolderKey, "");
        if (!string.IsNullOrEmpty(saved))
        {
            SavedScene[] scenes = JsonUtility.FromJson<SavedSetup>(saved).scenes;
            var setup = new SceneSetup[scenes.Length];
            for (int i = 0; i < scenes.Length; i++)
                setup[i] = new SceneSetup { path = scenes[i].path, isLoaded = scenes[i].isLoaded, isActive = scenes[i].isActive };
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        if (!string.IsNullOrEmpty(folder)) Assert.That(AssetDatabase.DeleteAsset(folder), Is.True, "Temporary course scene cleanup");
        SessionState.EraseString(SetupKey);
        SessionState.EraseString(FolderKey);
    }

    [UnityTest]
    public IEnumerator ShippedR66Colliders_CrossEveryCourseGateAndFinishTimedLap()
    {
        course = new WaypointCourseTests();
        course.SetUp();
        yield return course.FlyShippedColliderThroughEveryGate();
    }
}
#endif
