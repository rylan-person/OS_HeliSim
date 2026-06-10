using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UISliderController : MonoBehaviour
{
    [SerializeField] private Slider slider;
    [SerializeField] private TMP_InputField inputField;

    public void OnSliderChanged()
    {
        inputField.text = slider.value.ToString();
    }

    public void OnTextInputChanged()
    {
        slider.value = float.Parse(inputField.text);
    }
}
