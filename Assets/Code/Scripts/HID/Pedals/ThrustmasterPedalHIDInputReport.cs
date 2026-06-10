using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.DualShock;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;

[StructLayout(LayoutKind.Explicit, Size = 7)]
#if UNITY_EDITOR
[InitializeOnLoad] // Make sure static constructor is called during startup.
#endif
class ThrustmasterPedalHIDInputReport : IInputStateTypeInfo
{
    public FourCC format => new FourCC('H', 'I', 'D');

    [FieldOffset(0)] public byte reportId;

    // Pass through a nummber between 0 and 255
    // 0 is no input, 255 is full input
    [InputControl(name = "rightPedalSmall", layout = "Axis", format = "BYTE", offset = 1)]
    [FieldOffset(1)] public byte rightPedalSmall;

    // Pass through a nummber between 0 and 3
    // 0 is no input, 3 is full input
    [InputControl(name = "rightPedalLarge", layout = "Axis", format = "BYTE", offset = 2)]
    [FieldOffset(2)] public byte rightPedalLarge;

    [InputControl(name = "leftPedalSmall", layout = "Axis", format = "BYTE", offset = 3)]
    [FieldOffset(3)] public byte leftPedalSmall;
    [InputControl(name = "leftPedalLarge", layout = "Axis", format = "BYTE", offset = 4)]
    [FieldOffset(4)] public byte leftPedalLarge;

    [InputControl(name = "rotationSmall", layout = "Axis", format = "BYTE", offset = 5)]
    [FieldOffset(5)] public byte rotationSmall;

    [InputControl(name = "rotationLarge", layout = "Axis", format = "BYTE", offset = 6)]
    [FieldOffset(6)] public byte rotationLarge;
}