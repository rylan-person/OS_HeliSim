using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class TelemetryDashboardPanel : DashboardPanel
{
    public override string PanelName => "Telemetry";

    [Header("Graphs")]
    [SerializeField] private TelemetryLineGraphic topGraph;
    [SerializeField] private TelemetryLineGraphic bottomGraph;

    [Header("Dropdowns")]
    [Tooltip("Single dropdown for toggling series on the top graph.")]
    [SerializeField] private TMP_Dropdown topGraphDropdown;
    [Tooltip("Single dropdown for toggling series on the bottom graph.")]
    [SerializeField] private TMP_Dropdown bottomGraphDropdown;

    // Palette used when auto-adding a series that has no existing color config.
    private static readonly Color[] SeriesPalette =
    {
        new Color(0.20f, 0.60f, 1.00f), // blue
        new Color(1.00f, 0.55f, 0.10f), // orange
        new Color(0.20f, 0.85f, 0.35f), // green
        new Color(0.95f, 0.25f, 0.25f), // red
        new Color(0.80f, 0.40f, 1.00f), // purple
        new Color(0.95f, 0.90f, 0.20f), // yellow
    };

    private static List<TMP_Dropdown.OptionData> cachedOptions;
    private static string[] seriesNames;

    protected override void Awake()
    {
        base.Awake();
        EnsureCachedOptions();
        InitDropdown(topGraphDropdown, topGraph);
        InitDropdown(bottomGraphDropdown, bottomGraph);
    }

    private static void EnsureCachedOptions()
    {
        if (cachedOptions != null)
            return;

        seriesNames = Enum.GetNames(typeof(TelemetrySeries));
        // Index 0 is the non-selectable title entry; series start at index 1.
        cachedOptions = new List<TMP_Dropdown.OptionData> { new TMP_Dropdown.OptionData("Series") };
        foreach (string name in seriesNames)
            cachedOptions.Add(new TMP_Dropdown.OptionData(name));
    }

    private void InitDropdown(TMP_Dropdown dropdown, TelemetryLineGraphic graph)
    {
        if (dropdown == null || graph == null)
            return;

        dropdown.ClearOptions();
        dropdown.AddOptions(cachedOptions);
        RefreshDropdownLabels(dropdown, graph);
        dropdown.SetValueWithoutNotify(0);

        dropdown.onValueChanged.AddListener(index =>
        {
            if (index == 0) // title row — ignore
            {
                dropdown.SetValueWithoutNotify(0);
                return;
            }

            // Series enum is offset by 1 because index 0 is the title.
            TelemetrySeries selected = (TelemetrySeries)(index - 1);
            if (graph.HasSeries(selected))
                graph.RemoveSeries(selected);
            else
                graph.AddSeries(selected, SeriesPalette[(index - 1) % SeriesPalette.Length]);

            RefreshDropdownLabels(dropdown, graph);

            // Always reset to 0 (title) so the same item can be clicked again.
            dropdown.SetValueWithoutNotify(0);
        });
    }

    private static void RefreshDropdownLabels(TMP_Dropdown dropdown, TelemetryLineGraphic graph)
    {
        var options = dropdown.options;
        // options[0] is the title row — leave it as-is.
        for (int i = 1; i < options.Count; i++)
        {
            TelemetrySeries s = (TelemetrySeries)(i - 1);
            string name = seriesNames[i - 1];

            if (graph.HasSeries(s))
            {
                // Find the color for this series to tint the checkmark
                var config = graph.series.Find(c => c.series == s);
                string hex = config != null ? ColorUtility.ToHtmlStringRGB(config.color) : "ffffff";
                options[i].text = $"<color=#{hex}>\u2713</color> {name}";
            }
            else
            {
                options[i].text = $"     {name}";
            }
        }

        // Force the dropdown to refresh its item list if it is open
        dropdown.RefreshShownValue();
    }
}
