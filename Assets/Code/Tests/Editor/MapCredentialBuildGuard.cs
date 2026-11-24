using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CesiumForUnity;
using HeliSim.FrontEnd;
using Mapbox.Json;
using Mapbox.Json.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Player builds must receive map credentials from the user at runtime.</summary>
public sealed class MapCredentialBuildGuard : IPreprocessBuildWithReport, IProcessSceneWithReport
{
    public int callbackOrder => int.MaxValue;

    [MenuItem("HeliSim/Build/Validate Map Credential Exclusion")]
    public static void ValidateProject()
    {
        string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var inputs = new HashSet<string>(AssetDatabase.GetDependencies(scenes, true), StringComparer.Ordinal);
        // Resources can ship without scene references, including dependencies outside Resources.
        string[] resources = AssetDatabase.GetAllAssetPaths().Where(IsResourceAsset).ToArray();
        inputs.UnionWith(resources);
        inputs.UnionWith(AssetDatabase.GetDependencies(resources, true));
        ValidateInputs(inputs);
        // Includes ignored/untracked Resources configurations.
        foreach (string path in Directory.EnumerateFiles("Assets", "MapboxConfiguration.*", SearchOption.AllDirectories))
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("/Resources/Mapbox/")
                && (normalized.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                    || normalized.EndsWith(".json", StringComparison.OrdinalIgnoreCase)))
            {
                ValidateMapboxConfiguration(ReadSource(normalized), normalized);
            }
        }
        foreach (string path in resources.Where(p => p.Contains("/Resources/Mapbox/MapboxConfiguration.")))
        {
            ValidateMapboxConfiguration(ReadSource(path), path);
        }
        if (Directory.Exists("Assets/StreamingAssets"))
        {
            foreach (string path in Directory.EnumerateFiles("Assets/StreamingAssets", "*", SearchOption.AllDirectories))
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension == ".json" || extension == ".txt" || extension == ".asset"
                    || extension == ".prefab" || extension == ".unity")
                {
                    Reject(MapCredentialSerialization.ContainsCredential(ReadSource(path)), path);
                }
            }
        }
    }

    private static void ValidateInputs(IEnumerable<string> inputs)
    {
        foreach (string path in inputs)
        {
            if (path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                CesiumIonServer server = AssetDatabase.LoadAssetAtPath<CesiumIonServer>(path);
                ValidateServer(server, path);
                // The retired type may no longer import, but its Resources asset still ships.
                if (Path.GetFileNameWithoutExtension(path) == "CesiumRuntimeSettings")
                {
                    Reject(MapCredentialSerialization.ContainsCredential(ReadSource(path)), path);
                }
            }
            else if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) { Fail("Could not inspect map credentials in prefab", path); }
                ValidateHierarchy(prefab, path);
            }
        }
    }

    public void OnPreprocessBuild(BuildReport report) => ValidateProject();

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null && !BuildPipeline.isBuildingPlayer) { return; }
        if (!string.IsNullOrEmpty(scene.path))
        {
            ValidateInputs(AssetDatabase.GetDependencies(scene.path, true));
        }
        foreach (GameObject root in scene.GetRootGameObjects()) { ValidateHierarchy(root, scene.path); }
    }

    public static void ValidateMapboxConfiguration(string json, string path)
    {
        JObject configuration;
        try { configuration = JObject.Parse(json); }
        catch { Fail("Mapbox configuration is not a valid JSON object", path); return; }
        // Duplicate/escaped keys must not let a parser discard a credential value.
        int tokenFields = 0;
        using (var reader = new JsonTextReader(new StringReader(json)))
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonToken.Comment) { Fail("Mapbox configuration must not contain JSON comments", path); }
                if (reader.TokenType == JsonToken.PropertyName && (string)reader.Value == "AccessToken")
                {
                    tokenFields++;
                }
            }
        }
        if (tokenFields != 1) { Fail("Mapbox configuration requires one unambiguous AccessToken field", path); }
        JToken token = configuration["AccessToken"];
        if (token == null || (token.Type != JTokenType.String && token.Type != JTokenType.Null))
        {
            Fail("Mapbox configuration requires a string or null AccessToken field", path);
        }
        Reject(token.Type == JTokenType.String && !string.IsNullOrEmpty((string)token), path);
    }

    private static bool IsResourceAsset(string path) => path.Contains("/Resources/")
        && !AssetDatabase.IsValidFolder(path)
        && (path.StartsWith("Assets/", StringComparison.Ordinal) || path.StartsWith("Packages/", StringComparison.Ordinal));

    private static void ValidateHierarchy(GameObject root, string path)
    {
        foreach (Cesium3DTileset tileset in root.GetComponentsInChildren<Cesium3DTileset>(true))
        {
            Reject(!string.IsNullOrEmpty(tileset.ionAccessToken), path);
            ValidateSerializedServer(tileset, path);
        }
        foreach (CesiumIonRasterOverlay overlay in root.GetComponentsInChildren<CesiumIonRasterOverlay>(true))
        {
            Reject(!string.IsNullOrEmpty(overlay.ionAccessToken), path);
            ValidateSerializedServer(overlay, path);
        }
        foreach (MapServiceTokenEntry entry in root.GetComponentsInChildren<MapServiceTokenEntry>(true))
        {
            var serialized = new SerializedObject(entry);
            foreach (string field in new[] { "cesiumToken", "mapboxToken" })
            {
                var input = serialized.FindProperty(field)?.objectReferenceValue as TMP_InputField;
                Reject(input != null && !string.IsNullOrEmpty(input.text), path);
            }
        }
    }

    private static void ValidateServer(CesiumIonServer server, string path)
    {
        if (server != null) { Reject(!string.IsNullOrEmpty(server.defaultIonAccessToken), path); }
    }

    private static void ValidateSerializedServer(Component component, string path)
    {
        // SDK ionServer getters can assign defaults/create an asset in the Editor.
        var serialized = new SerializedObject(component);
        ValidateServer(serialized.FindProperty("_ionServer")?.objectReferenceValue as CesiumIonServer, path);
    }

    private static string ReadSource(string path)
    {
        try
        {
            if (path.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                if (package == null) { Fail("Could not resolve map credential source", path); }
                int packageEnd = path.IndexOf('/', "Packages/".Length);
                return File.ReadAllText(Path.Combine(package.resolvedPath, path.Substring(packageEnd + 1)));
            }
            return File.ReadAllText(path);
        }
        catch { Fail("Could not read map credential source", path); return null; }
    }

    private static void Reject(bool hasCredential, string path)
    {
        if (hasCredential)
        {
            Fail("Map credential found; clear serialized authentication and enter credentials in the running application", path);
        }
    }

    private static void Fail(string message, string path) => throw new BuildFailedException(message + ": " + path);
}
