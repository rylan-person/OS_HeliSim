using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Timing : MonoBehaviour
{
    public bool IsExternallyDriven { get; set; }
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

    private readonly Color purple = new Color(0.29f, 0.82f, 0.76f);

    private readonly Color green = new Color(0.44f, 0.76f, 0.57f);

    private readonly Color slower = new Color(0.95f, 0.47f, 0.43f);
    private readonly Color close = new Color(0.96f, 0.75f, 0.42f);

    private string FormatTime(float time, int decimalPlaces)
    {
        if (float.IsNaN(time) || float.IsInfinity(time) || time >= float.MaxValue * 0.5f)
            return decimalPlaces == 3 ? "-.---" : "-.--";

        int scale = decimalPlaces == 3 ? 1000 : 100;
        int totalUnits = Mathf.RoundToInt(Mathf.Max(0f, time) * scale);
        int minutes = totalUnits / (60 * scale);
        int seconds = totalUnits / scale % 60;
        int fraction = totalUnits % scale;

        if (decimalPlaces == 3)
            return minutes > 0 ? $"{minutes}:{seconds:00}.{fraction:000}" : $"{seconds}.{fraction:000}";

        return minutes > 0 ? $"{minutes}:{seconds:00}.{fraction:00}" : $"{seconds}.{fraction:00}";
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
            timeDifferenceString = newTimeDifference < 0
                ? "-" + FormatTime(-newTimeDifference, 2)
                : FormatTime(0f, 2);
        }

        lastLapTimeDifferenceText.text = timeDifferenceString;

        // if the time is less than the best sector time set the color to purple
        Color color = slower;
        if (newTimeDifference < 0)
        {
            color = purple;
        }
        else if (newTimeDifference < 10)
        {
            color = close;
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
            sectorTimeDifferenceString = newSectorTimeDifference < 0
                ? "-" + FormatTime(-newSectorTimeDifference, 2)
                : FormatTime(0f, 2);
        }

        // if the time is less than the best sector time set the color to purple
        Color color = slower;
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
            color = close;
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

    public void Render(PlayerTimeTrialState state)
    {
        if (state == null)
        {
            ResetDisplay();
            return;
        }

        if (currentLapTimeText != null)
            currentLapTimeText.text = state.LapActive || state.CurrentLapTime > 0f
                ? FormatTime(state.CurrentLapTime, 3) : "-.---";
        if (bestLapTimeText != null)
            bestLapTimeText.text = state.BestLapTime > 0f ? FormatTime(state.BestLapTime, 3) : "-.---";
        if (optLapTimeText != null)
            optLapTimeText.text = state.OptLapTime > 0f ? FormatTime(state.OptLapTime, 3) : "-.---";
        if (lastLapTimeText != null)
        {
            lastLapTimeText.text = state.LastLapTime > 0f ? FormatTime(state.LastLapTime, 3) : "-.---";
            lastLapTimeText.color = state.LastLapTime > 0f && state.LastLapWasNewBest ? purple : Color.white;
        }
        if (lastLapTimeDifferenceText != null)
        {
            if (state.LastLapTime > 0f && state.BestLapTime > 0f)
                updateLastLapTimeDifference(state.LastLapDiffTime, state.BestLapTime, state.AverageLapTime);
            else
            {
                lastLapTimeDifferenceText.text = "-.--";
                lastLapTimeDifferenceText.color = Color.white;
            }
        }

        int sectorCount = sectorTimesText != null ? sectorTimesText.Length : 0;
        for (int i = 0; i < sectorCount; i++)
        {
            if (sectorTimesText[i] == null)
                continue;

            float split = i < state.CurrentSectorSplitCount ? state.GetCurrentSectorSplit(i) : 0f;
            if (split > 0f)
            {
                sectorTimesText[i].text = FormatTime(split, 3);
                if (sectorTimesDifferenceText != null && i < sectorTimesDifferenceText.Length && sectorTimesDifferenceText[i] != null)
                {
                    float best = state.GetBestSector(i);
                    if (best < float.MaxValue * 0.5f)
                        updateSectorTimeDifference(i, split, state.GetAverageSector(i), best, state.GetBestLapSector(i));
                    else
                        resetSectorDifferenceTime(i);
                }
            }
            else
            {
                sectorTimesText[i].text = state.LapActive && i == state.CurrentSector
                    ? FormatTime(state.CurrentSectorTime, 3) : "-.---";
                if (sectorTimesDifferenceText != null && i < sectorTimesDifferenceText.Length && sectorTimesDifferenceText[i] != null)
                    resetSectorDifferenceTime(i);
            }
        }
    }

    public void ResetDisplay()
    {
        if (currentLapTimeText != null) currentLapTimeText.text = "-.---";
        if (bestLapTimeText != null) bestLapTimeText.text = "-.---";
        if (optLapTimeText != null) optLapTimeText.text = "-.---";
        if (lastLapTimeText != null)
        {
            lastLapTimeText.text = "-.---";
            lastLapTimeText.color = Color.white;
        }
        if (lastLapTimeDifferenceText != null)
        {
            lastLapTimeDifferenceText.text = "-.--";
            lastLapTimeDifferenceText.color = Color.white;
        }

        if (sectorTimesText == null)
            return;
        for (int i = 0; i < sectorTimesText.Length; i++)
        {
            if (sectorTimesText[i] != null) sectorTimesText[i].text = "-.---";
            if (sectorTimesDifferenceText != null && i < sectorTimesDifferenceText.Length && sectorTimesDifferenceText[i] != null)
            {
                sectorTimesDifferenceText[i].text = "-.--";
                sectorTimesDifferenceText[i].color = Color.white;
            }
        }
    }

}
