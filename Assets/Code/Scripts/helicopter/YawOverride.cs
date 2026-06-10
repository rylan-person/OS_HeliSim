using System.Collections;
using System.Collections.Generic;
using Oyedoyin.Common;
using UnityEngine;
using UnityEngine.UI;

public class YawOverride : MonoBehaviour
{
    [SerializeField] private Controller m_controller;

    [SerializeField] private bool m_yawOverride = false;

    [SerializeField] private Toggle m_yawOverrideToggle;

    public void SetYawOverride(bool bol)
    {
        m_yawOverride = m_yawOverrideToggle.isOn;
        // TODO - Implement yaw override in the controller. When enabled, the controller will use a specific yaw value instead of the actual yaw input. This can be useful for testing or for creating specific scenarios. The player can enable or disable the yaw override using a toggle. When the yaw override is enabled, the controller will use the override value instead of the actual yaw input.
        // m_controller.ToggleYawOverride(m_yawOverride);
    }
}
