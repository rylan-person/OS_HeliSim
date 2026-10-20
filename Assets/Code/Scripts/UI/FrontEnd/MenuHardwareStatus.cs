using System.Text;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace HeliSim.FrontEnd
{
    public static class MenuHardwareStatus
    {
        public static string BuildSummary()
        {
            var summary = new StringBuilder();
            summary.Append("Input devices detected: ");
            var devices = InputSystem.devices;
            if (devices.Count == 0)
            {
                summary.Append("none");
            }
            for (int i = 0; i < devices.Count; i++)
            {
                if (i > 0)
                {
                    summary.Append(", ");
                }
                summary.Append(string.IsNullOrEmpty(devices[i].displayName) ? devices[i].layout : devices[i].displayName);
            }
            summary.AppendLine();
            summary.AppendLine(XRSettings.isDeviceActive ? "XR: headset active." : "XR: no active headset reported; desktop use is available.");
            summary.Append("NOVA: physical connection unverified; no motion output from this menu.");
            return summary.ToString();
        }
    }
}
