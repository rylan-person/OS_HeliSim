using UnityEngine;

namespace HeliSim.FrontEnd
{
    // Applies local preferences once, after the owning helicopter and its UI exist.
    public sealed class MenuGameplaySetup : MonoBehaviour
    {
        private MenuSessionOptions options;
        private bool assistanceApplied;
        private bool recordingApplied;

        public void Initialize(MenuSessionOptions launchOptions) { options = launchOptions.Clone(); }

        private void Update()
        {
            if (options == null || options.Role != ClientRole.Player) { enabled = false; return; }
            var helicopter = HelicopterComponents.Instance;
            if (helicopter == null) return;
            if (!assistanceApplied)
            {
                if (options.AssistancePreset < 0) assistanceApplied = true;
                else
                {
                    var menu = FindFirstObjectByType<MainMenu>(FindObjectsInactive.Include);
                    if (menu != null) assistanceApplied = menu.TrySetAssistancePreset(options.AssistancePreset);
                }
            }
            if (!recordingApplied)
            {
                var recorder = helicopter.GetComponentInParent<DataExtractor>();
                if (recorder == null) recorder = helicopter.GetComponentInChildren<DataExtractor>(true);
                if (recorder == null && options.RecordFlightData && helicopter.rotaryController != null)
                {
                    recorder = helicopter.rotaryController.gameObject.AddComponent<DataExtractor>();
                    recorder.rotaryController = helicopter.rotaryController;
                    recorder.heli_transform = helicopter.rotaryController.transform;
                    recorder.heli_core = helicopter.rotaryController.GetComponentInChildren<Oyedoyin.Common.SilantroCore>(true);
                }
                if (recorder != null && options.RecordFlightData)
                {
                    recorder.heliDataList.Clear();
                    recorder.isRecording = true;
                }
                recordingApplied = true;
            }
            if (assistanceApplied && recordingApplied) enabled = false;
        }
    }
}
