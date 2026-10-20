using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HeliSim.FrontEnd
{
    public sealed class MenuSessionCoordinator : MonoBehaviour
    {
        public const string MenuScenePath = "Assets/Level/Scenes/MainMenu.unity";
        private static MenuSessionCoordinator instance;
        private static string menuMessage;
        private readonly HashSet<GameObject> sessionRoots = new HashSet<GameObject>();
        private MenuTransitionView transitionView;
        private NetworkManager sessionManager;
        private MenuSessionOptions options;
        private bool transitioning;
        private bool returnRequested;
        private bool sessionActive;

        public static bool IsTransitioning => instance != null && instance.transitioning;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { instance = null; menuMessage = null; }

        private void Awake()
        {
            if (instance != null && instance != this) { Destroy(gameObject); return; }
            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private static MenuSessionCoordinator GetOrCreate()
        {
            if (instance == null)
                new GameObject("MenuSessionCoordinator").AddComponent<MenuSessionCoordinator>();
            return instance;
        }

        public static string ConsumeMenuMessage()
        {
            string message = menuMessage;
            menuMessage = null;
            return message;
        }

        public static void Launch(MenuSessionOptions launchOptions)
        {
            if (!Application.isPlaying || launchOptions == null || IsTransitioning) return;
            var copy = launchOptions.Clone();
            if (!copy.TryValidate(Application.CanStreamedLevelBeLoaded, out string error))
            {
                Debug.LogWarning(error);
                return;
            }
            var coordinator = GetOrCreate();
            if (coordinator.sessionActive) return;
            coordinator.options = copy;
            coordinator.StartCoroutine(coordinator.LaunchRoutine());
        }

        public static void RequestReturnToMenu()
        {
            if (!Application.isPlaying || !Application.CanStreamedLevelBeLoaded(MenuScenePath)) return;
            var coordinator = GetOrCreate();
            if (coordinator.transitioning) { coordinator.returnRequested = true; return; }
            coordinator.CaptureSessionRoots();
            coordinator.StartCoroutine(coordinator.ReturnRoutine(null));
        }

        private void Update()
        {
            if (sessionActive && !transitioning &&
                (sessionManager == null || (!sessionManager.IsListening && !sessionManager.ShutdownInProgress)))
            {
                string reason = sessionManager != null ? sessionManager.DisconnectReason : null;
                StartCoroutine(ReturnRoutine(string.IsNullOrWhiteSpace(reason) ? "Session ended. You can start or join another flight." : reason));
            }
        }

        private void ShowProgress(string text)
        {
            if (transitionView == null)
            {
                transitionView = new GameObject("SessionTransition", typeof(RectTransform)).AddComponent<MenuTransitionView>();
                transitionView.transform.SetParent(transform, false);
                transitionView.Initialize(() => returnRequested = true);
            }
            transitionView.gameObject.SetActive(true);
            transitionView.SetMessage(text);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private IEnumerator LaunchRoutine()
        {
            transitioning = true;
            returnRequested = false;
            ShowProgress("Loading flight environment...");
            SceneManager.sceneLoaded += CaptureLoadedScene;
            AsyncOperation load = null;
            string loadError = null;
            try { load = SceneManager.LoadSceneAsync(options.ScenePath, LoadSceneMode.Single); }
            catch (Exception exception) { loadError = exception.Message; }
            if (load == null)
            {
                SceneManager.sceneLoaded -= CaptureLoadedScene;
                yield return ReturnRoutine("Could not load the flight environment. " + loadError);
                yield break;
            }
            while (!load.isDone)
            {
                ShowProgress(returnRequested ? "Finishing scene load before returning..." : "Loading flight environment... " + Mathf.RoundToInt(load.progress * 100f / 0.9f) + "%");
                yield return null;
            }
            SceneManager.sceneLoaded -= CaptureLoadedScene;
            yield return null; // Let scene Start methods initialise fields before the shared startup path.
            CaptureSessionRoots();
            if (returnRequested) { yield return ReturnRoutine(null); yield break; }

            var roleUi = FindFirstObjectByType<RoleSelectionUI>(FindObjectsInactive.Include);
            sessionManager = NetworkManager.Singleton;
            if (roleUi == null || sessionManager == null ||
                !roleUi.ConfigureEndpoint(options.Address, options.Port, options.Mode == MenuLaunchMode.Solo))
            {
                yield return ReturnRoutine("The flight scene could not initialise its connection controls.");
                yield break;
            }
            if (options.Role == ClientRole.Spectator) roleUi.OnClickSpectator();
            else roleUi.OnClickPlayer();
            if (options.Mode == MenuLaunchMode.Join) roleUi.StartAsClient();
            else roleUi.StartAsHost();

            float deadline = Time.realtimeSinceStartup + 30f;
            while (!returnRequested && sessionManager != null && !sessionManager.IsConnectedClient &&
                roleUi.IsConnecting && Time.realtimeSinceStartup < deadline)
            {
                ShowProgress(roleUi.StatusMessage);
                yield return null;
            }
            if (returnRequested || sessionManager == null || !sessionManager.IsConnectedClient)
            {
                string failure = returnRequested ? null : roleUi.IsConnecting ? "Connection timed out. Check the address and network, then retry." : roleUi.StatusMessage;
                yield return ReturnRoutine(failure);
                yield break;
            }

            new MenuSessionOptionsStore().Save(options);
            var setup = gameObject.AddComponent<MenuGameplaySetup>();
            setup.Initialize(options);
            sessionActive = true;
            transitioning = false;
            transitionView.gameObject.SetActive(false);
        }

        private void CaptureLoadedScene(Scene scene, LoadSceneMode mode)
        {
            foreach (GameObject root in scene.GetRootGameObjects()) sessionRoots.Add(root);
            CaptureSessionRoots();
        }

        private void CaptureSessionRoots()
        {
            sessionManager = NetworkManager.Singleton;
            if (sessionManager != null) sessionRoots.Add(sessionManager.transform.root.gameObject);
            if (CustomConnectionManager.Instance != null) sessionRoots.Add(CustomConnectionManager.Instance.transform.root.gameObject);
            if (WaypointManager.Instance != null) sessionRoots.Add(WaypointManager.Instance.transform.root.gameObject);
            if (TimeTrialCameraManager.Instance != null) sessionRoots.Add(TimeTrialCameraManager.Instance.transform.root.gameObject);
            if (CameraSwitcher.Instance != null) sessionRoots.Add(CameraSwitcher.Instance.transform.root.gameObject);
        }

        private IEnumerator ReturnRoutine(string message)
        {
            transitioning = true;
            sessionActive = false;
            if (!string.IsNullOrWhiteSpace(message)) menuMessage = message;
            ShowProgress("Closing session...");
            foreach (NovaController nova in FindObjectsByType<NovaController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                nova.active = false;
                nova.enabled = false;
            }
            FinalizeRecordings();
            CaptureSessionRoots();
            if (sessionManager != null)
            {
                sessionManager.Shutdown();
                float deadline = Time.realtimeSinceStartup + 15f;
                while (sessionManager != null && (sessionManager.IsListening || sessionManager.ShutdownInProgress) && Time.realtimeSinceStartup < deadline)
                    yield return null;
                if (sessionManager != null && (sessionManager.IsListening || sessionManager.ShutdownInProgress))
                {
                    ShowProgress("Network shutdown has not finished. Waiting for it to complete safely...");
                    while (sessionManager != null && (sessionManager.IsListening || sessionManager.ShutdownInProgress)) yield return null;
                }
            }
            foreach (GameObject root in sessionRoots)
                if (root != null && root != gameObject) Destroy(root);
            sessionRoots.Clear();
            yield return null;
            PlayerPrefs.Save();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;
            AsyncOperation load = SceneManager.LoadSceneAsync(MenuScenePath, LoadSceneMode.Single);
            while (load != null && !load.isDone) yield return null;
            transitioning = false;
            Destroy(gameObject);
        }

        private static void FinalizeRecordings()
        {
            foreach (DataExtractor recorder in FindObjectsByType<DataExtractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!recorder.isRecording) continue;
                try { recorder.TryStopRecording(out _); }
                catch (Exception exception) { Debug.LogWarning("Could not finish flight recording: " + exception.Message); }
            }
            foreach (HelicopterRecorder recorder in FindObjectsByType<HelicopterRecorder>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (recorder.IsRecording) try { recorder.StopRecording(); } catch (Exception exception) { Debug.LogWarning("Could not finish journey recording: " + exception.Message); }
            foreach (VRCameraRecording recorder in FindObjectsByType<VRCameraRecording>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (recorder.IsRecording) try { recorder.StopRecording(); } catch (Exception exception) { Debug.LogWarning("Could not finish VR recording: " + exception.Message); }
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= CaptureLoadedScene;
            if (instance == this) instance = null;
        }
    }
}
