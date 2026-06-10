using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Code.Scripts.UI
{
	public class SettingsMenu : MonoBehaviour
	{
		[Header("UI Elements")]
		[SerializeField] private TMP_Dropdown controlScheme;
		[SerializeField] private TMP_InputField volume;
		[SerializeField] private TMP_InputField screenSpaceError;
		[SerializeField] private Toggle preloadAncestors;
		[SerializeField] private Toggle preloadSiblings;
		[SerializeField] private Toggle forbidHoles;
		[SerializeField] private TMP_InputField maxSimultaneousTileLoads;
		[SerializeField] private TMP_InputField loadingDescendantLimit;
		[SerializeField] private TMP_InputField MaxCachedBytes;
		[SerializeField] private Toggle enableFrustumCulling;
		[SerializeField] private Toggle enableFogCulling;
		[SerializeField] private Toggle enableForceScreenSpaceError;
		[SerializeField] private TMP_InputField culledScreenSpaceError;

		[Header("Backend Link")]
		[SerializeField] private PrefSettings prefSettings;

		private void OnEnable()
		{
			SetUiVariables();
		}

		public void SetUiVariables()
		{
			volume.text = prefSettings.volume.ToString();
			// run when settings menu is opened - needs to populate all the settings with existing data from playerprefs
			controlScheme.value = (int)prefSettings.controlScheme;
			//volume.text = (AudioListener.volume*200).ToString();
			Debug.Log("Volume: " + prefSettings.volume);
			volume.text = prefSettings.volume.ToString();
			screenSpaceError.text = prefSettings.screenSpaceError.ToString();
			preloadAncestors.isOn = prefSettings.preloadAncestors;
			preloadSiblings.isOn = prefSettings.preloadSiblings;
			forbidHoles.isOn = prefSettings.forbidHoles;
			maxSimultaneousTileLoads.text = prefSettings.MaximumSimultaneousTileLoads.ToString();
			loadingDescendantLimit.text = prefSettings.LoadingDescendantLimit.ToString();
			MaxCachedBytes.text = prefSettings.MaximumCachedBytes.ToString();
			enableFrustumCulling.isOn = prefSettings.enableFrustumCulling;
			enableFogCulling.isOn = prefSettings.enableFogCulling;
			enableForceScreenSpaceError.isOn = prefSettings.enableForceScreenSpaceError;
			culledScreenSpaceError.text = prefSettings.culledScreenSpaceError.ToString();
		}

		public void ApplySettings()
		{
			// runs when apply settings is pressed, saves all settings to playerprefs and updates the game manager(s)
			prefSettings.controlScheme = (global::ControlScheme)(ControlScheme)controlScheme.value;
			prefSettings.volume = int.Parse(volume.text);
			prefSettings.screenSpaceError = int.Parse(screenSpaceError.text);
			prefSettings.preloadAncestors = preloadAncestors.isOn;
			prefSettings.preloadSiblings = preloadSiblings.isOn;
			prefSettings.forbidHoles = forbidHoles.isOn;
			prefSettings.MaximumSimultaneousTileLoads = uint.Parse(maxSimultaneousTileLoads.text);
			prefSettings.LoadingDescendantLimit = uint.Parse(loadingDescendantLimit.text);
			prefSettings.MaximumCachedBytes = int.Parse(MaxCachedBytes.text);
			prefSettings.enableFrustumCulling = enableFrustumCulling.isOn;
			prefSettings.enableFogCulling = enableFogCulling.isOn;
			prefSettings.enableForceScreenSpaceError = enableForceScreenSpaceError.isOn;
			prefSettings.culledScreenSpaceError = int.Parse(culledScreenSpaceError.text);
			prefSettings.VariablesToObjects();
		}
	}

	public enum ControlScheme
	{
		KBM,
		Warthog
	}
}
