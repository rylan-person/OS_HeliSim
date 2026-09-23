using System;

public static class BindingDeviceDisplayFormatter
{
    public static string FormatPath(string effectivePath)
    {
        if (string.IsNullOrWhiteSpace(effectivePath))
        {
            return string.Empty;
        }

        int start = effectivePath.IndexOf('<');
        int end = start >= 0 ? effectivePath.IndexOf('>', start + 1) : -1;
        string deviceName = start >= 0 && end > start
            ? effectivePath.Substring(start + 1, end - start - 1)
            : effectivePath;

        if (deviceName.StartsWith("HID::", StringComparison.OrdinalIgnoreCase))
        {
            deviceName = deviceName.Substring("HID::".Length);
        }

        deviceName = deviceName.Trim();
        if (string.Equals(deviceName, "ThrustmasterRudderPedals", StringComparison.OrdinalIgnoreCase))
        {
            return "Thrustmaster Rudder Pedals";
        }
        if (deviceName.StartsWith("Thustmaster ", StringComparison.OrdinalIgnoreCase))
        {
            deviceName = "Thrustmaster " + deviceName.Substring("Thustmaster ".Length);
        }

        const string warthogSuffix = " - HOTAS Warthog";
        if (deviceName.EndsWith(warthogSuffix, StringComparison.OrdinalIgnoreCase))
        {
            deviceName = deviceName.Substring(0, deviceName.Length - warthogSuffix.Length);
        }

        int firstSpace = deviceName.IndexOf(' ');
        if (firstSpace > 0)
        {
            string manufacturer = deviceName.Substring(0, firstSpace);
            string remaining = deviceName.Substring(firstSpace + 1).TrimStart();
            if (remaining.StartsWith(manufacturer + " ", StringComparison.OrdinalIgnoreCase))
            {
                deviceName = manufacturer + " " + remaining.Substring(manufacturer.Length + 1);
            }
        }

        return deviceName;
    }
}
