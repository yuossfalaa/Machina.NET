﻿using System;
using System.Collections.Generic;
using Machina.Drivers.Communication;
using Machina.Types.Geometry;

namespace Machina.Drivers;
     
class DriverUR : Driver
{
    #region Variables
    private TCPCommunicationManagerUR _tcpManager;

    private Dictionary<ConnectionType, bool> _availableConnectionTypes = new Dictionary<ConnectionType, bool>()
    {
        { ConnectionType.User, true },
        { ConnectionType.Machina, false }
    };
    public override Dictionary<ConnectionType, bool> AvailableConnectionTypes { get { return _availableConnectionTypes; } }
    #endregion


    #region Ctor
    /// <summary>
    /// Main constructor
    /// </summary>
    public DriverUR(Control ctrl) : base(ctrl) { }
    #endregion

    #region Public Methods
    /// <summary>
    /// Start a TCP connection to device via its address on the network.
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    /// <returns></returns>
    public override bool ConnectToDevice(string ip, int port)
    {
        // @TODO: the motionCursor should be part of the driver props?
        _tcpManager = new TCPCommunicationManagerUR(this, this.ReleaseCursor, this.parentControl.ExecutionCursor, ip, port);

        if (_tcpManager.Connect())
        {
            this.IP = _tcpManager.IP;
            this.Port = _tcpManager.Port;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Performs all necessary actions to establish a connection to a real/virtual device, 
    /// including connecting to the controller, loggin in, checking required states, etc.
    /// </summary>
    /// <param name="deviceId"></param>
    public override bool ConnectToDevice(int deviceId)
    {
        throw new ArgumentException("Can only connect to ConnectToDevice(int deviceId) in ConnectionType.Machina mode");

        return false;
    }

    /// <summary>
    /// Forces disconnection from current controller and manages associated logoffs, disposals, etc.
    /// </summary>
    /// <returns></returns>
    public override bool DisconnectFromDevice()
    {
        bool success = true;

        if (_tcpManager != null)
        {
            success &= _tcpManager.Disconnect();
        }

        return success;
    }

    public override bool Dispose()
    {
        return DisconnectFromDevice();
    }

    public override Joints GetCurrentJoints()
    {
        // temp using just the init values...
        return this._tcpManager.initAx;
    }

    public override Rotation GetCurrentOrientation()
    {
        // temp using just the init values...
        return this._tcpManager.initRot;
    }

    public override Vector GetCurrentPosition()
    {
        // temp using just the init values...
        return this._tcpManager.initPos;
    }

    public override ExternalAxes GetCurrentExternalAxes()
    {
        // Do URs have the capacity to do external axes...?
        return null;
    }

    public override void DebugDump()
    {
        throw new NotImplementedException();
    }

    public override void Reset()
    {
        throw new NotImplementedException();
    }

    public override Dictionary<string, string> GetDeviceDriverModules(Dictionary<string, string> parameters)
    {
        throw new NotImplementedException();
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Performs necessary operations to set up 'stream' control mode in the controller
    /// </summary>
    /// <returns></returns>
    private bool SetupStreamingMode()
    {
        throw new NotImplementedException();
    }

    public override bool SetRunMode(CycleType mode)
    {
        throw new NotImplementedException();
    }

    internal override bool ConfigureBuffer(int minActions, int maxActions)
    {
        return this._tcpManager.ConfigureBuffer(minActions, maxActions);
    }


    #endregion
}
