using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrimDirectionUIController : MonoBehaviour
{
    public double[] trimDirectionValues = new double[2] { 0, 0 };
    // max text ui element
    public TMPro.TextMeshProUGUI maxText;
    // min text ui element
    public TMPro.TextMeshProUGUI minText;
    
    // background image ui
    public UnityEngine.UI.Image backgroundImage;

    [SerializeField] private bool isHighlighted = false;

    // Default Colour hex
    private string defaultColourHex = "#FFFFFF";
    // unity colour
    private Color defaultColour = new(255, 255, 255, 0.1f);
    // Highlighted Colour hex
    private string highlightedColourHex = "#6A75FF";
    // unity colour
    private Color highlightedColour = new(106/255, 117/255, 255/255, 0.2f);

    public void ToggleBackgroundColour()
    {
        Debug.Log("Toggling background colour Current Highlighted: " + isHighlighted);
        if (isHighlighted)
        {
            backgroundImage.color = defaultColour;
            isHighlighted = false;
        }
        else
        {
            backgroundImage.color = highlightedColour;
            isHighlighted = true;
        }
    }

    public void UpdateTrimDirectionValues(double[] newValues)
    {
        trimDirectionValues = newValues;
        // Rounded to 1 decimal places
        maxText.text = trimDirectionValues[1].ToString("F1");
        minText.text = trimDirectionValues[0].ToString("F1");
    }
}
