using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem;
using UnityEngine;

[InputControlLayout(stateType = typeof(ThrustmasterPedalHIDInputReport))]
class ThrustmasterRudderPedals : UnityEngine.InputSystem.HID.HID
{

    static ThrustmasterRudderPedals()
    {
        InputSystem.RegisterLayout<ThrustmasterRudderPedals>(
            matches: new InputDeviceMatcher()
                .WithInterface("HID")
                .WithCapability("vendorId", 0x044F)
                .WithCapability("productId", 0xB679));
    }

    [RuntimeInitializeOnLoadMethod]
    static void Init() { }

}