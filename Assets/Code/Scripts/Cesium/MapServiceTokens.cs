using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using CesiumForUnity;
using Mapbox.Unity;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Optional map credentials stored for the current Windows user, outside project assets.</summary>
public static class MapServiceTokens
{
    public static bool HasSavedCesium => ReadSaved(out var pair) && !string.IsNullOrEmpty(pair.cesium);
    public static bool HasSavedMapbox => ReadSaved(out var pair) && !string.IsNullOrEmpty(pair.mapbox);

    /// <summary>False means secure storage could not be read, rather than that no tokens were saved.</summary>
    public static bool TryGetSavedStatus(out bool cesiumSaved, out bool mapboxSaved)
    {
        bool readable = ReadSaved(out var pair);
        cesiumSaved = readable && !string.IsNullOrEmpty(pair.cesium);
        mapboxSaved = readable && !string.IsNullOrEmpty(pair.mapbox);
        return readable;
    }

    /// <summary>Save the pair atomically; blank fields keep the previously saved provider.</summary>
    public static bool TrySave(string cesiumToken, string mapboxToken)
    {
        if (!ReadSaved(out var pair)) return false;
        if (!string.IsNullOrWhiteSpace(cesiumToken)) pair.cesium = cesiumToken.Trim();
        if (!string.IsNullOrWhiteSpace(mapboxToken)) pair.mapbox = mapboxToken.Trim();
        if (!MapTokenCredentialStore.TryWrite(pair)) return false;
        Apply(pair.cesium, pair.mapbox);
        return true;
    }

    /// <summary>Forget both providers, including active session overrides. Failed deletion keeps the session intact.</summary>
    public static bool TryForget()
    {
        if (!MapTokenCredentialStore.TryDelete()) return false;
        ResetSession();
        if (Application.isPlaying)
        {
            SubscribeToScenes();
            for (int i = 0; i < SceneManager.sceneCount; i++) ApplyToScene(SceneManager.GetSceneAt(i));
        }
        return true;
    }

#if UNITY_EDITOR
    /// <summary>One-time developer migration. Existing saved values win; native readback confirms persistence.</summary>
    public static bool TryImportMissing(string cesiumToken, string mapboxToken)
    {
        if (!ReadSaved(out var pair)) return false;
        if (string.IsNullOrEmpty(pair.cesium)) pair.cesium = (cesiumToken ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(pair.mapbox)) pair.mapbox = (mapboxToken ?? string.Empty).Trim();
        return MapTokenCredentialStore.TryWrite(pair)
            && ReadSaved(out var saved)
            && saved.cesium == pair.cesium && saved.mapbox == pair.mapbox;
    }
#endif

    private static bool ReadSaved(out MapTokenCredentialStore.TokenPair pair) =>
        MapTokenCredentialStore.TryRead(out pair);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void LoadSavedTokens()
    {
        SubscribeToScenes();
        if (ReadSaved(out var pair)) Apply(pair.cesium, pair.mapbox);
    }

    private static void SubscribeToScenes()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private static CesiumIonServer runtimeServer;
    private static CesiumIonServer previousServer;
    private static readonly Dictionary<Cesium3DTileset, CesiumIonServer> tilesets =
        new Dictionary<Cesium3DTileset, CesiumIonServer>();
    private static readonly Dictionary<CesiumIonRasterOverlay, CesiumIonServer> overlays =
        new Dictionary<CesiumIonRasterOverlay, CesiumIonServer>();
    private static readonly HashSet<Behaviour> guestSuppressed = new HashSet<Behaviour>();

    /// <summary>Call before loading a flight scene. Blank fields preserve the current credential.</summary>
    public static void Apply(string cesiumToken, string mapboxToken)
    {
        if (!Application.isPlaying)
        {
            return;
        }

        MapboxAccess.SetRuntimeAccessToken(mapboxToken);
        if (string.IsNullOrWhiteSpace(cesiumToken))
        {
            return;
        }

        if (runtimeServer == null)
        {
            // Loading Resources directly avoids the SDK getter creating an asset in the Editor.
            CesiumIonServer source = Resources.Load<CesiumIonServer>("CesiumIonServers/ion.cesium.com");
            previousServer = source != null ? CesiumIonServer.serverForNewObjects : null;
            runtimeServer = IsPublicIon(source)
                ? UnityEngine.Object.Instantiate(source)
                : ScriptableObject.CreateInstance<CesiumIonServer>();
            runtimeServer.hideFlags = HideFlags.HideAndDontSave;
            SubscribeToScenes();
        }

        runtimeServer.defaultIonAccessToken = cesiumToken.Trim();
        runtimeServer.defaultIonAccessTokenId = string.Empty;
        // Preserve custom/self-hosted defaults.
        if (previousServer == null || IsPublicIon(previousServer))
        {
            CesiumIonServer.serverForNewObjects = runtimeServer;
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            ApplyToScene(SceneManager.GetSceneAt(i));
        }
    }

    private static bool IsPublicIon(CesiumIonServer server)
    {
        return server != null
            && Uri.TryCreate(server.serverUrl, UriKind.Absolute, out Uri serverUri)
            && serverUri.Scheme == Uri.UriSchemeHttps
            && string.Equals(serverUri.Host, "ion.cesium.com", StringComparison.OrdinalIgnoreCase)
            && (string.IsNullOrEmpty(server.apiUrl)
                || (Uri.TryCreate(server.apiUrl, UriKind.Absolute, out Uri apiUri)
                    && apiUri.Scheme == Uri.UriSchemeHttps
                    && string.Equals(apiUri.Host, "api.cesium.com", StringComparison.OrdinalIgnoreCase)));
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyToScene(scene);
    }

    private static void OnSceneUnloaded(Scene scene)
    {
        RemoveDestroyedObjects(tilesets);
        RemoveDestroyedObjects(overlays);
        guestSuppressed.RemoveWhere(component => component == null);
    }

    private static void RemoveDestroyedObjects<T>(Dictionary<T, CesiumIonServer> objects)
        where T : UnityEngine.Object
    {
        var destroyed = new List<T>();
        foreach (T key in objects.Keys)
        {
            if (key == null) { destroyed.Add(key); }
        }
        foreach (T key in destroyed)
        {
            objects.Remove(key);
        }
    }

    private static void ApplyToScene(Scene scene)
    {
        if (!scene.isLoaded)
        {
            return;
        }

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Cesium3DTileset tileset in root.GetComponentsInChildren<Cesium3DTileset>(true))
            {
                if (tileset.tilesetSource == CesiumDataSource.FromCesiumIon && IsPublicIon(tileset.ionServer))
                {
                    if (runtimeServer != null)
                    {
                        if (!tilesets.ContainsKey(tileset))
                            tilesets.Add(tileset, tileset.ionServer == runtimeServer ? previousServer : tileset.ionServer);
                        tileset.ionServer = runtimeServer;
                    }
                    UpdateGuestSuppression(tileset, tileset.ionAccessToken, tileset.ionServer);
                }
            }

            foreach (CesiumIonRasterOverlay overlay in root.GetComponentsInChildren<CesiumIonRasterOverlay>(true))
            {
                if (IsPublicIon(overlay.ionServer))
                {
                    if (runtimeServer != null)
                    {
                        if (!overlays.ContainsKey(overlay))
                            overlays.Add(overlay, overlay.ionServer == runtimeServer ? previousServer : overlay.ionServer);
                        overlay.ionServer = runtimeServer;
                    }
                    UpdateGuestSuppression(overlay, overlay.ionAccessToken, overlay.ionServer);
                }
            }
        }
    }

    private static void UpdateGuestSuppression(Behaviour component, string token, CesiumIonServer server)
    {
        // Editor authoring behavior is unchanged. Only restore components this service disabled.
        if (Application.isEditor) return;
        bool hasToken = !string.IsNullOrWhiteSpace(token)
            || (server != null && !string.IsNullOrWhiteSpace(server.defaultIonAccessToken));
        if (hasToken)
        {
            if (guestSuppressed.Remove(component)) component.enabled = true;
        }
        else if (component.enabled)
        {
            guestSuppressed.Add(component);
            component.enabled = false;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (runtimeServer != null)
        {
            CesiumIonServer.serverForNewObjects = previousServer;
            foreach (var entry in tilesets)
            {
                if (entry.Key != null) { entry.Key.ionServer = entry.Value; }
            }
            foreach (var entry in overlays)
            {
                if (entry.Key != null) { entry.Key.ionServer = entry.Value; }
            }
            UnityEngine.Object.DestroyImmediate(runtimeServer);
        }
        tilesets.Clear();
        overlays.Clear();
        foreach (Behaviour component in guestSuppressed)
            if (component != null) component.enabled = true;
        guestSuppressed.Clear();
        runtimeServer = null;
        previousServer = null;
        MapboxAccess.ResetRuntimeAccessToken();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

#if UNITY_EDITOR
    private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
        {
            ResetSession();
        }
    }
#endif
}

// Windows Credential Manager protects this per-user record. No credential is written to
// PlayerPrefs, assets, logs or repository files. Unsupported platforms remain usable.
internal static class MapTokenCredentialStore
{
    [Serializable]
    internal sealed class TokenPair
    {
        public string cesium = string.Empty;
        public string mapbox = string.Empty;
    }

    private static string Target
    {
        get
        {
#if UNITY_EDITOR
            string testTarget = UnityEditor.SessionState.GetString("HeliSim.MapServiceTokens.TestTarget", string.Empty);
            if (testTarget.StartsWith("HeliSim/MapServiceTokens/Tests/", StringComparison.Ordinal)) return testTarget;
#endif
            return "HeliSim/MapServiceTokens/v1";
        }
    }

    internal static bool TryRead(out TokenPair pair)
    {
        pair = new TokenPair();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        IntPtr pointer = IntPtr.Zero;
        try
        {
            if (!CredRead(Target, 1, 0, out pointer)) return Marshal.GetLastWin32Error() == 1168;
            var credential = Marshal.PtrToStructure<Credential>(pointer);
            if (credential.blobSize == 0 || credential.blobSize > 2560 || credential.blob == IntPtr.Zero) return false;
            byte[] bytes = new byte[credential.blobSize];
            Marshal.Copy(credential.blob, bytes, 0, bytes.Length);
            try
            {
                pair = JsonUtility.FromJson<TokenPair>(Encoding.UTF8.GetString(bytes));
                return pair != null && pair.cesium != null && pair.mapbox != null;
            }
            finally { Array.Clear(bytes, 0, bytes.Length); }
        }
        catch (Exception) { return false; }
        finally { if (pointer != IntPtr.Zero) CredFree(pointer); }
#else
        return false;
#endif
    }

    internal static bool TryWrite(TokenPair pair)
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(pair));
        IntPtr blob = IntPtr.Zero;
        try
        {
            if (bytes.Length > 2560) return false;
            blob = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new Credential
            {
                type = 1, targetName = Target, blobSize = (uint)bytes.Length,
                blob = blob, persist = 2, userName = "HeliSim"
            };
            return CredWrite(ref credential, 0);
        }
        catch (Exception) { return false; }
        finally
        {
            if (blob != IntPtr.Zero)
            {
                for (int i = 0; i < bytes.Length; i++) Marshal.WriteByte(blob, i, 0);
                Marshal.FreeHGlobal(blob);
            }
            Array.Clear(bytes, 0, bytes.Length);
        }
#else
        return false;
#endif
    }

    internal static bool TryDelete()
    {
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        try { return CredDelete(Target, 1, 0) || Marshal.GetLastWin32Error() == 1168; }
        catch (Exception) { return false; }
#else
        return false;
#endif
    }

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint flags;
        public uint type;
        public string targetName;
        public string comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME lastWritten;
        public uint blobSize;
        public IntPtr blob;
        public uint persist;
        public uint attributeCount;
        public IntPtr attributes;
        public string targetAlias;
        public string userName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);
    [DllImport("Advapi32.dll")]
    private static extern void CredFree(IntPtr credential);
#endif
}
