using System.Globalization;
using TMPro;
using UnityEngine;

// Keeps the slider optional: incomplete or invalid text stays available for validation.
public sealed class InFlightSettingSlider : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Slider slider;
    [SerializeField] private TMP_InputField inputField;
    private string lastText;

    public void OnSliderChanged()
    {
        if (slider != null && inputField != null)
        {
            inputField.text = Mathf.RoundToInt(slider.value).ToString(CultureInfo.CurrentCulture);
        }
    }

    private void LateUpdate()
    {
        if (inputField == null || slider == null || inputField.text == lastText) return;
        lastText = inputField.text;
        if (int.TryParse(lastText, NumberStyles.Integer, CultureInfo.CurrentCulture, out int value))
        {
            slider.SetValueWithoutNotify(value);
        }
    }
}
