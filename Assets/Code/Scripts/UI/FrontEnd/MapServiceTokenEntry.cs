using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace HeliSim.FrontEnd
{
    /// <summary>Manages the signed-in Windows user's streaming credentials.</summary>
    public sealed class MapServiceTokenEntry : MonoBehaviour
    {
        public const string CesiumSignupUrl = "https://ion.cesium.com/signup";
        public const string MapboxSignupUrl = "https://account.mapbox.com/auth/signup/";

        [SerializeField] private TMP_InputField cesiumToken;
        [SerializeField] private TMP_InputField mapboxToken;
        [SerializeField] private Button applyButton;
        [SerializeField] private Button forgetButton;
        [SerializeField] private Button cesiumSignupButton;
        [SerializeField] private Button mapboxSignupButton;
        [SerializeField] private TMP_Text statusLabel;

        public void Configure(TMP_InputField cesium, TMP_InputField mapbox, Button apply,
            Button cesiumSignup, Button mapboxSignup, TMP_Text status, Button forget)
        {
            UnwireControls();
            cesiumToken = cesium;
            mapboxToken = mapbox;
            applyButton = apply;
            forgetButton = forget;
            cesiumSignupButton = cesiumSignup;
            mapboxSignupButton = mapboxSignup;
            statusLabel = status;
            MaskFields();
            if (isActiveAndEnabled) WireControls();
        }

        private void OnEnable()
        {
            MaskFields();
            WireControls();
            if (Application.isPlaying) ShowSavedState();
        }

        private void OnDisable()
        {
            UnwireControls();
            ClearFields();
            SetStatus(string.Empty);
        }

        public void ApplyTokens()
        {
            string cesium = cesiumToken != null ? cesiumToken.text.Trim() : string.Empty;
            string mapbox = mapboxToken != null ? mapboxToken.text.Trim() : string.Empty;
            if (cesium.Length == 0 && mapbox.Length == 0)
            {
                ClearFields();
                SetStatus("Enter a token to update that service. Blank fields keep the current settings.");
                return;
            }

            if (!MapServiceTokens.TrySave(cesium, mapbox))
            {
                SetStatus("Could not save your tokens to Windows Credential Manager. Try again.");
                return;
            }
            ClearFields();
            ShowSavedState();
        }

        public void ForgetTokens()
        {
            if (!MapServiceTokens.TryForget())
            {
                SetStatus("Could not remove your saved tokens from Windows Credential Manager.");
                return;
            }
            ClearFields();
            ShowSavedState();
        }

        private void ShowSavedState()
        {
            if (!MapServiceTokens.TryGetSavedStatus(out bool cesium, out bool mapbox))
            {
                SetStatus("Could not read saved tokens from Windows Credential Manager.");
                return;
            }
            SetStatus((cesium ? "Cesium ion saved" : "No saved Cesium ion token") + "  ·  " +
                (mapbox ? "Mapbox saved" : "No saved Mapbox token"));
            if (forgetButton != null) forgetButton.interactable = cesium || mapbox;
        }

        public void OpenCesiumSignup() => Application.OpenURL(CesiumSignupUrl);
        public void OpenMapboxSignup() => Application.OpenURL(MapboxSignupUrl);

        private void MaskFields()
        {
            if (cesiumToken != null) cesiumToken.contentType = TMP_InputField.ContentType.Password;
            if (mapboxToken != null) mapboxToken.contentType = TMP_InputField.ContentType.Password;
        }

        private void ClearFields()
        {
            if (cesiumToken != null) cesiumToken.SetTextWithoutNotify(string.Empty);
            if (mapboxToken != null) mapboxToken.SetTextWithoutNotify(string.Empty);
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null) statusLabel.text = message;
        }

        private void WireControls()
        {
            UnwireControls();
            if (applyButton != null) applyButton.onClick.AddListener(ApplyTokens);
            if (forgetButton != null) forgetButton.onClick.AddListener(ForgetTokens);
            if (cesiumSignupButton != null) cesiumSignupButton.onClick.AddListener(OpenCesiumSignup);
            if (mapboxSignupButton != null) mapboxSignupButton.onClick.AddListener(OpenMapboxSignup);
        }

        private void UnwireControls()
        {
            if (applyButton != null) applyButton.onClick.RemoveListener(ApplyTokens);
            if (forgetButton != null) forgetButton.onClick.RemoveListener(ForgetTokens);
            if (cesiumSignupButton != null) cesiumSignupButton.onClick.RemoveListener(OpenCesiumSignup);
            if (mapboxSignupButton != null) mapboxSignupButton.onClick.RemoveListener(OpenMapboxSignup);
        }
    }
}
