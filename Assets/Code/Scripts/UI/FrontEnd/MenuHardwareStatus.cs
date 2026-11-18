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

        /// <summary>Card value for flight controls, e.g. "3 devices".</summary>
        public static string InputDevicesValue()
        {
            int count = InputSystem.devices.Count;
            return count == 1 ? "1 device" : count + " devices";
        }

        /// <summary>Card detail naming the detected input devices.</summary>
        public static string InputDevicesDetail()
        {
            var devices = InputSystem.devices;
            if (devices.Count == 0)
            {
                return "No input devices detected. Connect a keyboard or flight controls.";
            }

            var names = new StringBuilder();
            for (int i = 0; i < devices.Count; i++)
            {
                if (i > 0)
                {
                    names.Append(i == devices.Count - 1 ? " and " : ", ");
                }

                names.Append(string.IsNullOrEmpty(devices[i].displayName) ? devices[i].layout : devices[i].displayName);
            }

            return names + ". Keyboard fallback stays available.";
        }

        /// <summary>Read-only state of a header status chip. NOVA is never probed from the menu.</summary>
        public enum ChipState
        {
            Neutral,
            Ok,
            Caution,
        }

        /// <summary>Header chip for input: a joystick when one is connected, otherwise keyboard and mouse.</summary>
        public static string InputChipLabel(out ChipState state)
        {
            if (Joystick.all.Count > 0)
            {
                state = ChipState.Ok;
                return Joystick.all.Count == 1 ? "Joystick + keyboard" : Joystick.all.Count + " joysticks + keyboard";
            }

            if (Keyboard.current != null)
            {
                state = ChipState.Ok;
                return Mouse.current != null ? "Keyboard & mouse" : "Keyboard";
            }

            state = ChipState.Caution;
            return "No input device";
        }

        /// <summary>Header chip for VR: an active OpenXR headset, or desktop use.</summary>
        public static string HeadsetChipLabel(out ChipState state)
        {
            state = HeadsetActive ? ChipState.Ok : ChipState.Neutral;
            return HeadsetActive ? "VR headset active" : "Desktop · no VR";
        }

        /// <summary>The menu never connects to NOVA, so its chip always reads unverified.</summary>
        public const string NovaChipLabel = "NOVA unverified";

        public static bool HeadsetActive => XRSettings.isDeviceActive;

        public static string HeadsetValue() => HeadsetActive ? "Active" : "Not active";

        public static string HeadsetDetail() => HeadsetActive
            ? "Headset reported by the OpenXR runtime. Recenter once seated."
            : "No headset reported. Desktop flying is available. Start your PC VR runtime before launching.";
    }
}
