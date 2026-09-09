/*
 * NOVA Controller for Unity
 * Copyright (C) 2020, Eight360 Limited
 * All Rights Reserved
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
 * COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
 * OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
 *
 * 2020-11-11: DA - Tidy up for distribution.
 * 
 */

using System;
using System.Text;
using System.Net;
using System.Net.Sockets;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using System.IO;
using System.Text.RegularExpressions;


public class NovaController : MonoBehaviour
{
    public enum Mode
    {
        RotationOnly,
        BasicMotionCuing,
        MotionCueing
    };

    [Header("Input Actions")]
    [Tooltip("On input.started the control mode will be changed to the specified type")]
    [SerializeField] private InputAction controlTypeCued;
    [FormerlySerializedAs("controlTypeQuat")][SerializeField] private InputAction controlTypeRotation;

    [Header("Networking")]
    [Tooltip("Name or IPv4 address of NOVA Control System")]
    public string novaIpAddress = "localhost";

    [Tooltip("IPv4 port number of NOVA Control System")]
    public int novaPort = 28360;

    [Header("Control data")]
    [Tooltip("RigidBody for control")]
    public Rigidbody rigidBody;

    [Header("GForce Calculator")]
    public GForceCalculator gForceCalculator;

    [Tooltip("Optional transform used to visualize the computed g-force direction; only required in MotionCueing mode.")]
    public GameObject testObject;

    [FormerlySerializedAs("aQuatFilt")]
    [Tooltip("Final Quaternion filter parameter 1 unfiltered 0 static")]
    [Range(0, 1.0f)]
    public float aFinalFilter = 0.02f;

    [Tooltip("Select motion cueing algorithm")]
    public Mode controlMode = Mode.RotationOnly;

    [Header("Basic Motion Cueing settings")]
    [Tooltip("Motion Cueing Factor, 1.0 realistic, 0.0 off")]
    [Range(0, 1.0f)]
    public float motionCueingFactor = 0.8f;

    [FormerlySerializedAs("aAccelFilt")]
    [Header("Filter Weights")]
    [Tooltip("acceleration filter parameter 1 unfiltered 0 static")]
    [Range(0, 1.0f)]
    public float aAccelerationFilter = 0.05f;



    [HideInInspector]
    public bool active = false;

    private Vector3 lastVelocity;


    private Vector3 smoothAccel;
    private Quaternion smoothQuat;
    private IPEndPoint remoteEndPoint;
    private UdpClient client;
    private int sequenceNumber;


    // Post Additions
    string settingsPath;
    // End Post Additions

    public void Start()
    {
        // Post Additions
        settingsPath = Application.persistentDataPath + "/Settings/novaIp.txt";
        Debug.Log("Settings Path: " + settingsPath);
        LoadSettings();
        // End Post Additions

        //if rigidbody is not initally defined try get it from the parent, else use given RB.
        if (!rigidBody)
        {
            rigidBody = GetComponent<Rigidbody>();
        }

        if (!rigidBody)
        {
            Debug.LogWarning("Component " + this.name + " has no Rigidbody component");
            active = false;
        }
        else
        {
            tryActivate();
        }
        controlTypeCued.started += OnControlTypeCuedStarted;
        controlTypeRotation.started += OnControlTypeRotationStarted;
        controlTypeCued.Enable();
        controlTypeRotation.Enable();
    }

    private void OnDisable()
    {
        controlTypeCued.started -= OnControlTypeCuedStarted;
        controlTypeRotation.started -= OnControlTypeRotationStarted;
        controlTypeCued.Disable();
        controlTypeRotation.Disable();
    }

    private void OnDestroy()
    {
        client?.Dispose();
    }

    private void OnControlTypeCuedStarted(InputAction.CallbackContext ctx) => ControlStateQuat();
    private void OnControlTypeRotationStarted(InputAction.CallbackContext ctx) => ControlStateCued();

    // Post Additions
    void LoadSettings()
    {
        if (File.Exists(settingsPath))
        {
            novaIpAddress = File.ReadAllText(settingsPath).Trim();
        }
        else
        {
            Debug.LogWarning("Settings file not found: " + settingsPath);
        }
    }
    // End Post Additions

    private void ControlStateQuat()
    {
        controlMode = Mode.RotationOnly;
        Debug.Log("switching to quat");

    }

    private void ControlStateCued()
    {
        controlMode = Mode.BasicMotionCuing;
        Debug.Log("switching to cued quat");
    }

    private void tryActivate()
    {
        remoteEndPoint = GetRemoteEndpoint(novaIpAddress, novaPort);
        if (remoteEndPoint == null)
        {
            active = false;
            return;
        }

        Debug.Log("Sending NOVA Control messages to " + remoteEndPoint.Address + " : " + remoteEndPoint.Port);
        client = new UdpClient(remoteEndPoint.AddressFamily);
        sequenceNumber = 0;
        active = true;
    }

    public void SetRigidBody(Rigidbody rb)
    {
        rigidBody = rb;
        tryActivate();
    }

    public void SetIP(string address)
    {
        novaIpAddress = address;
    }

    public void SetPort(int port)
    {
        novaPort = port;
    }

    private IPEndPoint GetRemoteEndpoint(string ipAddress, int port)
    {
        try
        {
            var addresses = Dns.GetHostEntry(ipAddress).AddressList;
            if (addresses == null || addresses.Length == 0)
            {
                Debug.LogError($"{nameof(NovaController)}: DNS lookup for '{ipAddress}' returned no addresses.");
                return null;
            }

            return new IPEndPoint(addresses[0], port);
        }
        catch (SocketException ex)
        {
            Debug.LogError($"{nameof(NovaController)}: failed to resolve NOVA host '{ipAddress}': {ex.Message}");
            return null;
        }
    }

    private void FixedUpdate()
    {
        if (!rigidBody || !active) return;

        var velocity = rigidBody.linearVelocity;
        var rotation = rigidBody.rotation;
        var acceleration = (velocity - lastVelocity) / Time.fixedDeltaTime;
        lastVelocity = velocity;
        ++sequenceNumber;

        switch (controlMode)
        {

            case Mode.BasicMotionCuing:
                rotation = doBasicMotionCueing(rotation, acceleration);
                break;

            case Mode.RotationOnly:
                break;

            case Mode.MotionCueing:
                rotation = doMotionCueing(rotation);
                break;

            default:
                Debug.LogWarning("Invalid Control Mode " + controlMode.ToString());
                active = false;
                break;
        }

        smoothQuat = Quaternion.Slerp(smoothQuat, rotation, aFinalFilter);
        SendAsQuaternion(sequenceNumber, smoothQuat);
    }

    private readonly StringBuilder _messageBuilder = new StringBuilder(64);

    private void SendAsQuaternion(int sequenceNumber, Quaternion unityRotation)
    {
        // Convert from Unity's left-handed Y-up, to NOVA's right-handed Z-up:
        Quaternion rhsOrientation = new Quaternion(-unityRotation.z, unityRotation.x, -unityRotation.y, unityRotation.w);

        // Unity Quaternion order is XYZW, but NOVA Control expects WXYZ.
        // Build the message with a reused StringBuilder instead of string.Format
        // to avoid allocating a new string every physics tick.
        _messageBuilder.Clear();
        _messageBuilder.Append(sequenceNumber).Append(',')
            .Append(rhsOrientation.w).Append(',')
            .Append(rhsOrientation.x).Append(',')
            .Append(rhsOrientation.y).Append(',')
            .Append(rhsOrientation.z).Append('\n');

        SendControlMessage(_messageBuilder);
    }

    private Quaternion doBasicMotionCueing(Quaternion rotation, Vector3 acceleration)
    {

        Vector3 gravity = new Vector3(0.0f, 9.81f, 0.0f);
        //compute the vehicle intrinsic force vector;
        acceleration = acceleration * motionCueingFactor;
        smoothAccel = (1 - aAccelerationFilter) * smoothAccel + aAccelerationFilter * (Quaternion.Inverse(rotation) * acceleration + gravity);
        //clamp to maximum 1g of acceleration cueing
        float a = 9.81f;
        smoothAccel = new Vector3(Mathf.Clamp(smoothAccel[0], -a, a), Mathf.Clamp(smoothAccel[1], -a, a), Mathf.Clamp(smoothAccel[2], -a, a));
        double roll = 180.0f / Math.PI * Math.Atan2(smoothAccel[0], smoothAccel[1]);
        double pitch = -180.0f / Math.PI * Math.Atan2(smoothAccel[2], smoothAccel[1]);
        return rotation * Quaternion.Euler((float)pitch, (float)0.0, (float)roll);
    }

    private Quaternion doMotionCueing(Quaternion rotation)
    {
        // Use gForceDirection from gForceCalculator
        Vector3 gForceDirection = gForceCalculator.gForceDirection;
        float gForceMagnitude = gForceCalculator.gForceMagnitude;

        Quaternion result = Quaternion.LookRotation(gForceDirection) * transform.rotation;

        if (testObject != null)
        {
            testObject.transform.rotation = result;
        }

        return result;
    }

    private void SendControlMessage(StringBuilder message)
    {
        try
        {
            byte[] data = Encoding.UTF8.GetBytes(message.ToString());
            client.Send(dgram: data, bytes: data.Length, endPoint: remoteEndPoint);
        }
        catch (Exception err)
        {
            Debug.LogError(err.ToString());
        }
    }
}
