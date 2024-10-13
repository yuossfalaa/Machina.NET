﻿using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Machina.Drivers.Communication.Protocols;
using Machina.Types.Geometry;

namespace Machina.Drivers.Communication;

/// <summary>
/// A class that manages TCP communication with ABB devices, including sending/receiving messages, 
/// queuing them, releasing them to the TCP server when appropriate, and raising events on 
/// buffer empty.
/// </summary>
internal class TCPCommunicationManagerABB
{

    #region Variables
    private RobotCursor _releaseCursor;
    private RobotCursor _executionCursor;
    private RobotCursor _motionCursor;

    private Driver _parentDriver;
    internal RobotLogger logger;

    private const int INIT_TIMEOUT = 5000;  // in millis
    internal Vector initPos;
    internal Rotation initRot;
    internal Joints initAx;
    internal ExternalAxes initExtAx;

    // Properties for Driver module
    private TcpClient _clientSocket;
    private NetworkStream _clientNetworkStream;
    private TCPConnectionStatus _clientStatus;
    private Thread _receivingThread;
    private Thread _sendingThread;
    private string _ip = "";
    public string IP => _ip;
    private int _port = 0;
    public int Port => _port;
    private bool _isDeviceBufferFull = false;

    private Protocols.Base _translator;
    private List<string> _messageBuffer = new List<string>();
    private byte[] _sendMsgBytes;
    private byte[] _receiveMsgBytes = new byte[1024];
    private int _receiveByteCount;
    private string _response;

    private int _sentMessages = 0;
    private int _receivedMessages = 0;
    private int _maxStreamCount = 10;
    private int _sendNewBatchOn = 2;

    private string _deviceDriverVersion = null;

    // Props for Monitor module (if available)
    private TcpClient _monitorClientSocket;
    private TCPConnectionStatus _monitorStatus;
    private Thread _monitoringThread;
    private string _monitorIP;
    private int _monitorPort;
    private int _monitorReceiveByteCount;
    private string _monitorMessage;
    private byte[] _monitorReceiveMsgBytes = new byte[1024];
    private int _monitorReceivedMessages = 0;

    private bool _isMonitored = false;
    public bool IsMonitored => _isMonitored;

    private readonly object _dataReceivedLock = new object();
    #endregion

    #region Ctor
    internal TCPCommunicationManagerABB(Driver driver, RobotCursor releaseCursor, RobotCursor executionCursor, string ip, int port)
    {
        this._parentDriver = driver;
        this.logger = driver.parentControl.logger;
        this._releaseCursor = releaseCursor;
        this._executionCursor = executionCursor;
        // @TODO: homogeinize how the driver picks cursors from parent control: as constructor arguments or directly from the object 
        this._motionCursor = driver.parentControl.MotionCursor;
        this._ip = ip;
        this._port = port;
        this._monitorIP = ip;
        this._monitorPort = port + 1;  // these should be configurable...   

        this._translator = Protocols.Factory.GetTranslator(this._parentDriver);
    }

    #endregion

    #region Internal Methods
    internal bool Connect()
    {
        try
        {
            SetClientSocket();

            StartSendingThread();

            StartRecivingThread();

            if (!WaitForInitialization())
            {
                logger.Error("Timeout when waiting for initialization data from the controller");
                Disconnect();
                return false;
            }

            TrySettingMonitorConnection();

            return _clientSocket.Connected;
        }
        catch (Exception ex)
        {
            logger.Debug(ex);
            Disconnect();
        }

        return false;
    }


    internal bool Disconnect()
    {
        DisconnectMonitor();

        if (_clientSocket != null)
        {
            _clientStatus = TCPConnectionStatus.Disconnected;
            try
            {
                _clientSocket.Client.Disconnect(false);
            }
            catch { }
            _clientSocket.Close();
            if (_clientNetworkStream != null) _clientNetworkStream.Dispose();
            return true;
        }

        return false;
    }

    internal bool ConfigureBuffer(int minActions, int maxActions)
    {
        this._maxStreamCount = maxActions;
        this._sendNewBatchOn = minActions;
        return true;
    }

    //internal bool Monitor()
    //{
    //    if (TryConnectMonitor())
    //    {
    //        // Establish a MotionCursor on `Control`
    //        this._parentDriver.parentControl.InitializeMotionCursor();
    //        this._motionCursor = this._parentDriver.parentControl.MotionCursor;

    //        return true;
    //    }

    //    return false;
    //}

    #endregion

    #region Private Methods
    private void TrySettingMonitorConnection()
    {
        // During YuMi development I was having a really weird problem: if a monitor is running, I cannot connect to another driver in the same unit... 
        // So, for the time being, let's make monitoring an explicit process with its own API?
        if (TryConnectMonitor())
        {
            // Establish a MotionCursor on `Control`
            this._parentDriver.parentControl.InitializeMotionCursor();
            this._motionCursor = this._parentDriver.parentControl.MotionCursor;
        }
    }

    private void StartRecivingThread()
    {
        _receivingThread = new Thread(ReceivingMethod);
        _receivingThread.IsBackground = true;
        _receivingThread.Start();
    }

    private void StartSendingThread()
    {
        _sendingThread = new Thread(SendingMethod);
        _sendingThread.IsBackground = true;
        _sendingThread.Start();
    }

    private void SetClientSocket()
    {
        _clientSocket = new TcpClient();
        _clientSocket.Connect(this._ip, this._port);
        _clientStatus = TCPConnectionStatus.Connected;
        _clientNetworkStream = _clientSocket.GetStream();
        _clientSocket.ReceiveBufferSize = 1024;
        _clientSocket.SendBufferSize = 1024;
    }

    private bool TryConnectMonitor()
    {
        try
        {
            SetMonitorClientSocket();

            StartMonitoringThread();

            _isMonitored = true;

            return _monitorClientSocket.Connected;
        }
        catch (Exception ex)
        {
            logger.Info("Real-time monitoring not available on this device");
            DisconnectMonitor();
        }

        return false;
    }

    private void StartMonitoringThread()
    {
        _monitoringThread = new Thread(MonitoringMethod);
        _monitoringThread.IsBackground = true;
        _monitoringThread.Start();
    }

    private void SetMonitorClientSocket()
    {
        _monitorClientSocket = new TcpClient();
        _monitorClientSocket.Connect(this._monitorIP, this._monitorPort);
        _monitorStatus = TCPConnectionStatus.Connected;
        _monitorClientSocket.ReceiveBufferSize = 1024;
        _monitorClientSocket.SendBufferSize = 1024;
    }

    private bool DisconnectMonitor()
    {
        if (_monitorClientSocket != null && _isMonitored)
        {
            try
            {
                if (_monitorClientSocket.Connected != null && _monitorClientSocket.Connected)
                {
                    _monitorClientSocket.Client.Disconnect(false);
                }
            }
            catch (Exception ex)
            {
                logger.Error("Something went wrong trying to disconnect from monitor");
                logger.Error(ex.ToString());
            }

            _monitorClientSocket.Close();
            _monitorStatus = TCPConnectionStatus.Disconnected;
            _isMonitored = false;

            return true;
        }

        return false;
    }

    private bool WaitForInitialization()
    {
        int time = 0;
        logger.Debug("Waiting for intialization data from controller...");

        bool Isinitialized() => _deviceDriverVersion == null || initAx == null || initPos == null || initRot == null || initExtAx == null;

        // @TODO: this is awful, come on...
        while (Isinitialized() && time < INIT_TIMEOUT)
        {
            time += 33;
            Thread.Sleep(33);
        }

        return _deviceDriverVersion != null || initAx != null && initPos != null && initRot != null && initExtAx == null;
    }

    private void SendingMethod(object obj)
    {
        NameThread("MachinaTCPDriverSendingThread");


        // Expire the thread on disconnection
        while (_clientStatus != TCPConnectionStatus.Disconnected)
        {
            SendMessagesIfNeeded();

            //RaiseBufferEmptyEventCheck();

            Thread.Sleep(30);
        }
    }

    private void SendMessagesIfNeeded()
    {
        while (this.ShouldSend() && this._releaseCursor.AreActionsPending())
        {
            var msgs = this._translator.GetMessagesForNextAction(this._releaseCursor);
            if (msgs != null)
            {
                foreach (var msg in msgs)
                {
                    SendMessage(msg);
                }
            }

            // Action was released to the device, raise event
            this._parentDriver.parentControl.RaiseActionReleasedEvent();
        }
    }

    private void SendMessage(string msg)
    {
        _sendMsgBytes = Encoding.ASCII.GetBytes(msg);
        _clientNetworkStream.Write(_sendMsgBytes, 0, _sendMsgBytes.Length);
        _sentMessages++;
        logger.Debug($"Sent: {msg}");
    }

    private void ReceivingMethod(object obj)
    {
        NameThread("MachinaTCPDriverListeningThread");

        // Scope leftover chunks from response messages on this thread
        string leftOverChunk = "";

        // Expire the thread on disconnection
        while (_clientStatus != TCPConnectionStatus.Disconnected)
        {
            ReciveMessageIfExist(ref leftOverChunk);

            Thread.Sleep(30);
        }
    }

    private void ReciveMessageIfExist(ref string leftOverChunk)
    {
        if (_clientSocket.Available > 0)
        {
            _receiveByteCount = _clientSocket.GetStream().Read(_receiveMsgBytes, 0, _receiveMsgBytes.Length);
            _response = Encoding.UTF8.GetString(_receiveMsgBytes, 0, _receiveByteCount);

            var msgs = SplitResponse(_response, ref leftOverChunk);
            foreach (var msg in msgs)
            {
                logger.Debug("Received message from driver: " + msg);
                ParseResponse(msg);
                _receivedMessages++;
            }
        }
    }

    private void MonitoringMethod(object obj)
    {
        NameThread("MachinaTCPMonitorListeningThread");

        // Scope leftover chunks from response messages on this thread
        string leftOverChunk = "";

        while (_monitorStatus != TCPConnectionStatus.Disconnected)
        {
            ReceiveMonitoringData(ref leftOverChunk);

            Thread.Sleep(30);
        }
    }

    private void ReceiveMonitoringData(ref string leftOverChunk)
    {
        if (_monitorClientSocket.Available > 0)
        {
            _monitorReceiveByteCount = _monitorClientSocket.GetStream().Read(_monitorReceiveMsgBytes, 0, _monitorReceiveMsgBytes.Length);
            _monitorMessage = Encoding.UTF8.GetString(_monitorReceiveMsgBytes, 0, _monitorReceiveByteCount);

            var msgs = SplitResponse(_monitorMessage, ref leftOverChunk);
            foreach (var msg in msgs)
            {
                ParseResponse(msg);
                _monitorReceivedMessages++;
                // @TODO: do something with the message: update MotionCursor --> Inserted in `DataReceived()`, not great place, make this more programmatic.
            }
        }
    }

    private static void NameThread(string name)
    {
        if (Thread.CurrentThread.Name == null)
        {
            Thread.CurrentThread.Name = name;
        }
    }

    private bool ShouldSend()
    {
        if (_isDeviceBufferFull)
        {
            if (_sentMessages - _receivedMessages <= _sendNewBatchOn)
            {
                _isDeviceBufferFull = false;
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            if (_sentMessages - _receivedMessages < _maxStreamCount)
            {
                return true;
            }
            else
            {
                _isDeviceBufferFull = true;
                return false;
            }
        }
    }

    /// <summary>
    /// Take the response buffer and split it into single messages.
    /// ABB cannot use strings longer that 80 chars, so some messages must be sent in chunks. 
    /// This function parses the response for message continuation and end chars, 
    /// and returns a list of joint messages if appropriate.
    /// </summary>
    /// <param name="response"></param>
    /// <returns></returns>
    private static string[] SplitResponse(string response, ref string unfinishedChunk)
    {
        // If there were leftovers from the previous message, attach them to the response
        if (unfinishedChunk.Length != 0)
        {
            response = unfinishedChunk + response;
            unfinishedChunk = "";
        }

        bool isThisResponseComplete = response[response.Length - 1] == ABBCommunicationProtocol.STR_MESSAGE_END_CHAR;

        string[] chunks = response.Split(new char[] { ABBCommunicationProtocol.STR_MESSAGE_END_CHAR }, StringSplitOptions.RemoveEmptyEntries);
        if (chunks.Length == 0)
            // Return empty array (and keep unfinished chunk for next response with body
            return chunks;

        // Store last chunk for next response and work with the rest. 
        if (!isThisResponseComplete)
        {
            unfinishedChunk = chunks[chunks.Length - 1];

            string[] copy = new string[chunks.Length - 1];
            Array.Copy(chunks, copy, chunks.Length - 1);

            chunks = copy;
        }

        // Join '>' chunks with whitespaces
        for (int i = 0; i < chunks.Length; i++)
        {
            chunks[i] = chunks[i].Replace(ABBCommunicationProtocol.STR_MESSAGE_CONTINUE_CHAR, ' ');
        }

        return chunks;
    }


    /// <summary>
    /// Parse the response and decide what to do with it.
    /// </summary>
    /// <param name="res"></param>
    private void ParseResponse(string res)
    {
        // If first char is an id marker (otherwise, we can't know which action it is)
        // @TODO: this is hardcoded for ABB, do this programmatically...
        if (res[0] == ABBCommunicationProtocol.STR_MESSAGE_ID_CHAR)
        {
            AcknowledgmentReceived(res);
        }
        else if (res[0] == ABBCommunicationProtocol.STR_MESSAGE_RESPONSE_CHAR)
        {
            //Console.WriteLine("RECEIVED: " + res);
            DataReceived(res);
        }
    }

    private void AcknowledgmentReceived(string res)
    {
        // @TODO: Add some sanity here for incorrectly formatted messages
        string[] _responseChunks = res.Split(' ');
        string idStr = _responseChunks[0].Substring(1);
        int id = Convert.ToInt32(idStr);

        // An Action may have been issued and released, but we may have not received an acknowledgement
        // (like for example `MotionMode` which has no streamable counterpart). 
        // Since actions have correlating ids, apply all up to the last received id. 

        // To raise all proper events, even for actions that are not streamable (and no acknowledgement 
        // is received), apply all actions one at a time until reching target id. 
        int nextId = this._executionCursor.GetNextActionId();

        if (nextId <= id)
        {
            while (nextId <= id)
            {
                this._executionCursor.ApplyNextAction();

                // Raise appropriate events
                this._parentDriver.parentControl.RaiseActionExecutedEvent();
                //this._parentDriver.parentControl.RaiseMotionCursorUpdatedEvent();
                //this._parentDriver.parentControl.RaiseActionCompletedEvent();

                nextId++;
            }
        }
        else
        {
            throw new Exception("WEIRD ERROR, TAKE A LOOK AT THIS!");
        }
    }

    private void DataReceived(string res)
    {
        lock (_dataReceivedLock)
        {
            string[] _responseChunks = res.Split(' ');
            int resType = Convert.ToInt32(_responseChunks[0].Substring(1));

            double[] data = new double[_responseChunks.Length - 1];
            for (int i = 0; i < data.Length; i++)
            {
                // @TODO: add sanity like Double.TryParse(...)
                data[i] = Double.Parse(_responseChunks[i + 1]);
            }

            switch (resType)
            {
                // ">20 1 2 1;" Sends version numbers
                case ABBCommunicationProtocol.RES_VERSION:
                    this._deviceDriverVersion = Convert.ToInt32(data[0]) + "." + Convert.ToInt32(data[1]) + "." + Convert.ToInt32(data[2]);
                    int comp = Utilities.Strings.CompareVersions(ABBCommunicationProtocol.MACHINA_SERVER_VERSION, _deviceDriverVersion);
                    if (comp > -1)
                    {
                        logger.Verbose($"Using ABB Driver version {ABBCommunicationProtocol.MACHINA_SERVER_VERSION}, found {_deviceDriverVersion}.");
                    }
                    else
                    {
                        logger.Warning($"Found driver version {_deviceDriverVersion}, expected at least {ABBCommunicationProtocol.MACHINA_SERVER_VERSION}. Please update driver module or unexpected behavior may arise.");
                    }
                    break;

                // ">21 400 300 500 0 0 1 0;"
                case ABBCommunicationProtocol.RES_POSE:
                    this.initPos = new Vector(data[0], data[1], data[2]);
                    this.initRot = new Rotation(new Quaternion(data[3], data[4], data[5], data[6]));
                    break;


                // ">22 0 0 0 0 90 0;"
                case ABBCommunicationProtocol.RES_JOINTS:
                    this.initAx = new Joints(data[0], data[1], data[2], data[3], data[4], data[5]);
                    break;

                // ">23 1000 9E9 9E9 9E9 9E9 9E9;"
                case ABBCommunicationProtocol.RES_EXTAX:
                    this.initExtAx = new ExternalAxes(data[0], data[1], data[2], data[3], data[4], data[5]);
                    break;

                // ">24 X Y Z QW QX QY QZ J1 J2 J3 J4 J5 J6 A1 A2 A3 A4 A5 A6;"
                case ABBCommunicationProtocol.RES_FULL_POSE:
                    Vector pos = new Vector(data[0], data[1], data[2]);
                    Rotation rot = new Rotation(new Quaternion(data[3], data[4], data[5], data[6]));
                    Joints ax = new Joints(data[7], data[8], data[9], data[10], data[11], data[12]);
                    ExternalAxes extax = new ExternalAxes(data[13], data[14], data[15], data[16], data[17], data[18]);

                    this._motionCursor.UpdateFullPose(pos, rot, ax, extax);
                    this._parentDriver.parentControl.RaiseMotionUpdateEvent();

                    break;
            }

        }

    }

    #endregion
}
