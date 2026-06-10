using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Timing : MonoBehaviour
{
    // List of sector times textUI
    public TMPro.TextMeshProUGUI[] sectorTimesText;

    // List of sector times difference textUI
    public TMPro.TextMeshProUGUI[] sectorTimesDifferenceText;

    // Current Lap Time TextUI
    public TMPro.TextMeshProUGUI currentLapTimeText;

    // Last Lap Time
    public TMPro.TextMeshProUGUI lastLapTimeText;

    // Last Lap Time Difference
    public TMPro.TextMeshProUGUI lastLapTimeDifferenceText;

    // Best Lap Time TextUI
    public TMPro.TextMeshProUGUI bestLapTimeText;

    // Opt Lap Time TextUI
    public TMPro.TextMeshProUGUI optLapTimeText;

    private Color purple = new Color(0.7843f, 0, 1f);

    private Color green = new Color(0, 1f, 0.25f);

    private string FormatTime(float time, int decimalPlaces)
    {
        int minutes = (int)(time / 60);
        int seconds = (int)(time % 60);
        int milliseconds = Mathf.RoundToInt((time - Mathf.Floor(time)) * 1000);

        if (decimalPlaces == 3)
        {
            return minutes > 0
                ? $"{minutes}:{seconds:00}.{milliseconds:000}"
                : $"{seconds}.{milliseconds:000}";
        }
        else // assume 2 decimal places
        {
            milliseconds = milliseconds / 10; // convert to 2 digits
            return minutes > 0
                ? $"{minutes}:{seconds:00}.{milliseconds:00}"
                : $"{seconds}.{milliseconds:00}";
        }
    }


    // Update current lap Time
    public void updateCurrentLapTime(float newLapTime)
    {
        string formattedTime = FormatTime(newLapTime, 3);

        currentLapTimeText.text = formattedTime;
    }

    // Update best lap Time
    public void updateBestLapTime(float newLapTime)
    {
        string formattedTime = FormatTime(newLapTime, 3);

        bestLapTimeText.text = formattedTime;
    }

    // Update opt lap Time
    public void updateOptLapTime(float newLapTime)
    {
        string formattedTime = FormatTime(newLapTime, 3);

        optLapTimeText.text = formattedTime;
    }

    public void updateLastLapTime(float newTime, bool newBest)
    {
        lastLapTimeText.text = FormatTime(newTime, 3);

        if (newBest)
        {
            lastLapTimeText.color = purple;
        }
        else
        {
            lastLapTimeText.color = new Color(1, 1, 1);
        }
    }

    public void updateLastLapTimeDifference(float newTimeDifference, float bestTime, float averageTime)
    {

        if (newTimeDifference > 120)
        {
            newTimeDifference = 120;
        }
        if (newTimeDifference < -120)
        {
            newTimeDifference = -120;
        }
        string timeDifferenceString;
        if (newTimeDifference > 0)
        {
            timeDifferenceString = "+" + FormatTime(newTimeDifference, 2);
        }
        else
        {
            timeDifferenceString = FormatTime(newTimeDifference, 2);
        }

        lastLapTimeDifferenceText.text = timeDifferenceString;

        // if the time is less than the best sector time set the color to purple
        Color color = Color.red;
        if (newTimeDifference < 0)
        {
            color = purple;
        }
        else if (newTimeDifference < 10)
        {
            color = Color.yellow;
        }

        lastLapTimeDifferenceText.color = color;

    }

    // Update sector time
    public void updateSectorTime(int sectorNumber, float newSectorTime)
    {
        sectorTimesText[sectorNumber].text = FormatTime(newSectorTime, 3); // 
    }

    public void resetSectorTime(int sectorNumber)
    {
        sectorTimesText[sectorNumber].text = "-.---";
    }

    // Update sector time difference with color as function input
    public void updateSectorTimeDifference(int sectorNumber, float newSectorTime, float averageSector, float bestSector, float bestLapSector)
    {
        float newSectorTimeDifference = newSectorTime - bestSector;

        if (newSectorTimeDifference > 120)
        {
            newSectorTimeDifference = 120;
        }
        if (newSectorTimeDifference < -120)
        {
            newSectorTimeDifference = -120;
        }
        string sectorTimeDifferenceString;
        if (newSectorTimeDifference > 0)
        {
            sectorTimeDifferenceString = "+" + FormatTime(newSectorTimeDifference, 2);
        }
        else
        {
            sectorTimeDifferenceString = "-" + FormatTime(newSectorTimeDifference, 2);
        }

        // if the time is less than the best sector time set the color to purple
        Color color = Color.red;
        if (newSectorTime < bestSector)
        {
            color = purple;
        }
        else if (newSectorTime < bestLapSector)
        {
            color = green;
        }
        else if (newSectorTime < averageSector)
        {
            color = Color.yellow;
        }


        sectorTimesDifferenceText[sectorNumber].text = sectorTimeDifferenceString;
        sectorTimesDifferenceText[sectorNumber].color = color;
    }

    public void resetSectorDifferenceTime(int sectorNumber)
    {
        sectorTimesDifferenceText[sectorNumber].text = "-.--";
        sectorTimesDifferenceText[sectorNumber].color = new Color(1, 1, 1);
    }

    // Reset Current Lap Time and Sector Times
    public void resetCurrentLapTime()
    {
        currentLapTimeText.text = "-.---";
        for (int i = 0; i < sectorTimesText.Length; i++)
        {
            resetSectorTime(i);
            resetSectorDifferenceTime(i);
        }
    }

}
