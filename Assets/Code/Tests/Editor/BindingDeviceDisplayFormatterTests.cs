using NUnit.Framework;

public class BindingDeviceDisplayFormatterTests
{
    [TestCase("<HID::Thrustmaster Throttle - HOTAS Warthog>/button16", "Thrustmaster Throttle")]
    [TestCase("<HID::Saitek Saitek X52 Flight Control System>/stick/y", "Saitek X52 Flight Control System")]
    [TestCase("<HID::Thustmaster Joystick - HOTAS Warthog>/stick/x", "Thrustmaster Joystick")]
    [TestCase("<ThrustmasterRudderPedals>/rotationSmall", "Thrustmaster Rudder Pedals")]
    [TestCase("", "")]
    [TestCase(null, "")]
    public void FormatPath_ReturnsReadableDeviceName(string path, string expected)
    {
        Assert.That(BindingDeviceDisplayFormatter.FormatPath(path), Is.EqualTo(expected));
    }
}
