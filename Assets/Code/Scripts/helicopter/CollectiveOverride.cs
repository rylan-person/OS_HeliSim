using System;
using System.Collections;
using System.Collections.Generic;
using Oyedoyin.Common;
using UnityEngine;
using UnityEngine.UI;

public class CollectiveOverride : MonoBehaviour
{
    [SerializeField] private bool m_collectiveOverride = false;
    [SerializeField] private Slider m_collectiveOverrideValueSlider;
    [SerializeField] private float m_collectiveOverrideValue = 0f;
    [SerializeField] private Toggle m_collectiveOverrideToggle;
    [SerializeField] private Controller m_controller;
    /*
    TODO - Collective override is a feature that allows the player to set a specific collective value that will be used instead of the actual collective input. This can be useful for testing or for creating specific scenarios. The player can enable or disable the collective override and set the override value using a slider. When the collective override is enabled, the controller will use the override value instead of the actual collective input.
    public void SetCollectiveOverride(bool bol)
    {
        bool overrideValue = m_collectiveOverrideToggle.isOn;

        m_controller.m_collectiveOverride = overrideValue;
        m_collectiveOverride = overrideValue;

        m_controller.m_collectiveOverrideValue = m_collectiveOverrideValue;

        m_collectiveOverrideValueSlider.gameObject.SetActive(m_collectiveOverride);

        if (m_collectiveOverride)
        {
            m_controller.UpdateCollectiveFromOverride();
        }
    }

    public void SetCollectiveOverrideValue(Single sin)
    {
        float overrideValue = m_collectiveOverrideValueSlider.value;

        if (!m_collectiveOverride)
        {
            Debug.LogWarning("Collective override is not enabled. Please enable it before setting the override value.");
            return;
        }
        m_collectiveOverrideValue = overrideValue;
        m_controller.m_collectiveOverrideValue = m_collectiveOverrideValue;

        m_controller.UpdateCollectiveFromOverride();
    }
    */
}
