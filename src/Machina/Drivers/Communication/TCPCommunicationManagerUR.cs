using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Machina.Drivers.Communication.Protocols;
using Machina.Types.Geometry;

namespace Machina.Drivers.Communication;

/// <summary>
/// A class that manages TCP communication with UR devices, including sending/receiving messages, 
/// queuing them, releasing them to the TCP server when appropriate, and raising events on 
/// buffer empty.
/// </summary>
internal class TCPCommunicationManagerUR
{
    /**
     * Machina communication with UR robots:
     *  - Machina will connect via TCP/IP socket client to the robot's real time client at port 30003.
     *  - From this port, it will receive a buffer with information about the robot state at 125Hz.
     *  - To this port, Machina will send string buffers with compiled programs containing a number of actions
     *      determined by _maxStreamCount. 
     *  - Machina will also establish a TCP server to receive acknowledgement messages from the robot. Every uplaoded
     *      program contains instructions to connect to this server, and send an acknowledgement. This will help 
     *      Machina track program execution at runtime.
     *  - Machina will listens to these acknowledgements, and when, upon remining _sendNewBatchOn, compile and upload
     *      a new program with the next batch of actions. If _sendNewBatchOn is less than 2, Machina will
     *      duplicate the remaining actions.
     *  
     *  References:
     *      https://www.universal-robots.com/how-tos-and-faqs/how-to/ur-how-tos/remote-control-via-tcpip-16496/
     */
    #region Variables

    internal TCPConnectionStatus ClientSocketStatus { get; private set; }

    private RobotCursor _issueCursor;
    private RobotCursor _motionCursor;
    private Driver _parentDriver;
    internal RobotLogger logger;

    private const int INIT_TIMEOUT = 5000;  // in millis
    internal Vector initPos;
    internal Rotation initRot;
    internal Joints initAx;

    /// <summary>
    ///  The client socket that connects to the robot's secondary client.
    /// </summary>
    private TcpClient _clientSocket;
    private NetworkStream _clientNetworkStream;
    private Thread _clientReceivingThread;
    private Thread _clientSendingThread;
    private string _robotIP;
    public string IP => _robotIP;
    private int _robotPort = 30003;
    public int Port => _robotPort;
    private bool _isDeviceBufferFull = false;

    private TcpListener _serverSocket;
    private Thread _serverListeningThread;
    private byte[] _serverListeningBytes = new byte[2048];
    private string _serverListeningMsg;
    private int[] _serverListeningInts;
    private bool _isServerListeningRunning = false;
    private Thread _serverSendingThread;
    private byte[] _ServerSendingBytes;
    public string ServerIP => _serverIP;
    private string _serverIP;
    public int ServerPort => _serverPort;
    private int _serverPort = 7003;
    private TcpClient _robotSocket;
    private NetworkStream _robotNetworkStream;

    private Protocols.Base _translator;

    private List<int> _sentIDs = new List<int>();
    private List<int> _receivedIDs = new List<int>();
    private byte[] _sendMsgBytes;
    private byte[] _receiveMsgBytes = new byte[2048];
    private int _receiveByteCount;
    private string _response;
    private string[] _responseChunks;

    private int _sentMessages = 0;
    private int _receivedMessages = 0;
    private int _maxStreamCount = 10;
    private int _sendNewBatchOn = 2;

    private bool _bufferEmptyEventIsRaiseable = true;


    #endregion

    #region Ctor
    internal TCPCommunicationManagerUR(Driver driver, RobotCursor writeCursor, RobotCursor motionCursor, string robotIP, int robotPort)
    {
        this._parentDriver = driver;
        this.logger = driver.parentControl.logger;
        this._issueCursor = writeCursor;
        this._motionCursor = motionCursor;
        this._robotIP = robotIP;
        //this._robotPort = robotPort;  // It will always be 30003, user need not care about this

        this._translator = Protocols.Factory.GetTranslator(this._parentDriver);

    }
    #endregion

    #region Internal Methods
    internal bool Disconnect()
    {
        if (_clientSocket != null)
        {
            // Upload an empty script to stop the running program
            string emptyScript = LoadEmptyScript();
            if (!UploadScriptToDevice(emptyScript, false))
            {
                Logger.Error("Could not load empty script to robot");
            }

            try
            {
                ClientSocketStatus = TCPConnectionStatus.Disconnected;
                DisposeClientSocketTcpClient();

                _isServerListeningRunning = false;

                DisposeRobotSocketTcpClient();

                _serverSocket.Stop();
                _serverSocket = null;
                return true;
            }
            catch (Exception ex)
            {
                logger.Error("Something went wrong on disconnection:");
                logger.Error(ex);
                return false;
            }
        }

        return false;
    }


    internal bool Connect()
    {
        try
        {
            SetupClientSocketToConnect();

            SetupClientReceivingThread();

            SetServerIp();

            logger.Debug("Machina local IP: " + _serverIP);

            StartServerSocket();

            SetupServerListeningThread();

            if (!InitConnection())
                return false;

            return _clientSocket.Connected;
        }
        catch (Exception ex)
        {
            logger.Error("Something went wrong trying to connect to robot...");
            logger.Debug(ex);
            Disconnect();
            return false;
        }
    }

    internal bool ConfigureBuffer(int minActions, int maxActions)
    {
        this._maxStreamCount = maxActions;
        this._sendNewBatchOn = minActions;
        return true;
    }


    #endregion

    #region Private Methods

    private bool InitConnection()
    {
        string drScript = LoadDriverScript();
        if (!UploadScriptToDevice(drScript, false))
        {
            logger.Error("Could not upload driver to robot");
            Disconnect();
            return false;
        }

        if (!WaitForInitialization())
        {
            logger.Error("Timeout when waiting for initialization data from the controller");
            Disconnect();
            return false;
        }

        return true;
    }
    private void StartServerSocket()
    {
        _serverSocket = new TcpListener(IPAddress.Parse(_serverIP), _serverPort);
        _serverSocket.Start();
    }

    private void SetServerIp()
    {
        if (!Net.Net.GetLocalIPAddressInNetwork(_robotIP, "255.255.255.0", out _serverIP))
        {
            throw new Exception("ERROR: Could not figure out local IP");
        }
    }

    private void SetupServerListeningThread()
    {
        _isServerListeningRunning = true;
        _serverListeningThread = new Thread(ServerReceivingMethod);
        _serverListeningThread.IsBackground = true;
        _serverListeningThread.Start();
    }

    private void SetupClientReceivingThread()
    {
        _clientReceivingThread = new Thread(ClientReceivingMethod);
        _clientReceivingThread.IsBackground = true;
        _clientReceivingThread.Start();
    }

    private void SetupClientSocketToConnect()
    {
        _clientSocket = new TcpClient();
        _clientSocket.Connect(this._robotIP, this._robotPort);
        ClientSocketStatus = TCPConnectionStatus.Connected;
        _clientNetworkStream = _clientSocket.GetStream();
        _clientSocket.ReceiveBufferSize = 2048;
        _clientSocket.SendBufferSize = 1024;
    }

    private void DisposeClientSocketTcpClient()
    {
        _clientSocket.Client.Disconnect(false);
        _clientSocket.Close();
        _clientNetworkStream?.Dispose();
    }

    private void DisposeRobotSocketTcpClient()
    {
        // TESTING
        if (_robotSocket != null)
        {
            _robotSocket.Close();
            _robotSocket.Dispose();
            _robotSocket = null;
        }
    }

    /// <summary>
    /// This method reads the buffer coming from the robot socket server and parses it into state info.
    /// </summary>
    /// <param name="obj"></param>
    private void ClientReceivingMethod(object obj)
    {
        //// @TODO: Parse the 30002 buffer to get information about the robot state

        // Expire the thread on disconnection
        while (ClientSocketStatus != TCPConnectionStatus.Disconnected)
        {
            Thread.Sleep(30);
        }
    }

    /// <summary>
    /// This method sends buffered instructions to the client socket on the robot whenever necessary
    /// </summary>
    /// <param name="obj"></param>
    private void ServerSendingMethod(object obj)
    {
        if (Thread.CurrentThread.Name == null)
        {
            Thread.CurrentThread.Name = "MachinaTCPDriverServerSendingMethod";
        }
        logger.Debug("Started TCP server sender for UR robot communication");

        // Expire thread if no socket
        while (_robotSocket != null)
        {
            while (this.ShouldSend() && this._issueCursor.AreActionsPending())
            {
                SendBytes();
            }

            //RaiseBufferEmptyEventCheck();

            Thread.Sleep(30);
        }

        logger.Debug("Stopped TCP server sender for UR robot communication");
    }

    private void SendBytes()
    {
        _sendMsgBytes = this._translator.GetBytesForNextAction(this._issueCursor);

        // If the action had instruction representation
        if (_sendMsgBytes != null)
        {
            _robotNetworkStream.Write(_sendMsgBytes, 0, _sendMsgBytes.Length);
            _sentIDs.Add(this._issueCursor.GetLastAction().Id);
            _sentMessages++;

            logger.Debug("Sending:");
            logger.Debug("  " + this._issueCursor.GetLastAction());
            logger.Debug("  [" + string.Join(",", (Utilities.Conversion.ByteArrayToInt32Array(_sendMsgBytes))) + "]");
        }

        // Action was released to the device, raise event
        this._parentDriver.parentControl.RaiseActionReleasedEvent();
    }

    /// <summary>
    /// This method listens to int messages from the client socket on the robots, and parses them as ids
    /// of completed actions.
    /// </summary>
    /// <param name="obj"></param>
    private void ServerReceivingMethod(object obj)
    {
        NameRecivingServerMethodThread();

        logger.Debug("Started TCP server listener for UR robot communication");

        // Do not kill threads by aborting them... https://stackoverflow.com/questions/1559255/whats-wrong-with-using-thread-abort/1560567#1560567
        while (_isServerListeningRunning)
        {
            logger.Debug("Waiting for a connection... ");

            if (!TryAcceptClientConnection())
                continue;

            logger.Debug("Connected client: " + _robotIP);

            StartSendingThreadFromRecivingMethod();

            _robotNetworkStream = _robotSocket.GetStream();

            ReceiveAndProcessData();

            Thread.Sleep(30);
        }

        logger.Debug("Stopped TCP server listener for UR robot communication");
    }
    // Method to receive and process data from the client
    
    private void ReceiveAndProcessData()
    {
        // Super quick and dirty workaround to receive data from the script, REALLY need to improve this...
        List<int> responseBuffer = new List<int>();

        // Loop to receive all the data sent by the client.
        try
        {
            int receivedCount;
            while (_isServerListeningRunning && (receivedCount = _robotNetworkStream.Read(_serverListeningBytes, 0, _serverListeningBytes.Length)) != 0)
            {
                _serverListeningInts = Utilities.Conversion.ByteArrayToInt32Array(_serverListeningBytes, receivedCount, false);

                logger.Debug("Received (id): [" + string.Join(",", _serverListeningInts) + "]");

                ProcessIncomingData(responseBuffer);

            }
        }
        catch (Exception e)
        {
            logger.Error("Something went wrong with the client... ");
            logger.Error(e);
        }
    }

    private void ProcessIncomingData(List<int> responseBuffer)
    {
        int item;
        for (int i = 0; i < _serverListeningInts.Length; i++)
        {
            item = _serverListeningInts[i];

            // In the middle of receiving data
            if (responseBuffer.Count != 0)
            {
                HandleBufferedData(ref responseBuffer, item);
            }
            else
            {
                HandleNewData(ref responseBuffer, item);
            }
        }
    }

    private void HandleNewData(ref List<int> responseBuffer, int item)
    {
        // It is a response flag
        if (item < -1)
        {
            responseBuffer.Add(item);
        }
        else
        {
            if (ProcessResponse(item))
            {
                _receivedMessages++;
            }
        }
    }

    private void HandleBufferedData(ref List<int> responseBuffer, int item)
    {
        // If receiving RES_END, parse response
        if (item == URCommunicationProtocol.RES_END)
        {
            ProcessResponse(responseBuffer);
            responseBuffer.Clear();
        }
        else
        {
            responseBuffer.Add(item);
        }
    }

    private void StartSendingThreadFromRecivingMethod()
    {
        _serverSendingThread = new Thread(ServerSendingMethod);
        _serverSendingThread.IsBackground = true;
        _serverSendingThread.Start();
    }

    private bool TryAcceptClientConnection()
    {
        // Perform a blocking call to accept requests.
        // You could also user server.AcceptSocket() here.
        try
        {
            _robotSocket = _serverSocket.AcceptTcpClient();
            return true;

        }
        catch (Exception ex)
        {
            logger.Error("Something went wrong waiting for client connection");
            logger.Error(ex);
            return false;   
        }
    }

    private static void NameRecivingServerMethodThread()
    {
        if (Thread.CurrentThread.Name == null)
        {
            Thread.CurrentThread.Name = "MachinaTCPDriverServerReceivingMethod";
        }
    }

    private bool WaitForInitialization()
    {
        int time = 0;
        logger.Debug("Waiting for intialization data from controller...");

        // @TODO: this is awful, come on...
        while ((initAx == null || initPos == null || initRot == null) && time < INIT_TIMEOUT)
        {
            time += 33;
            Thread.Sleep(33);
        }

        return initAx != null && initPos != null && initRot != null;
    }

    private bool ShouldSend()
    {
        int remaining = CalculateRemaining();

        if (_isDeviceBufferFull)
        {
            if (remaining <= _sendNewBatchOn)
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
            if (remaining < _maxStreamCount)
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


    private bool ProcessResponse(int id)
    {
        // Some messages actually contain several instructions (like a pop call may). 
        // In this case, ids are -1 except for the last instruction, that contains the right id.
        // If an id is below 1, just ignore it. 
        if (id < 1)
            return false;

        _receivedIDs.Add(id);


        //// THIS IS CURRENTLY UNTESTED FOR URs. @TODO: VERIFY THIS WORKS.
        // An Action may have been issued and released, but we may have not received an acknowledgement
        // (like for example `MotionMode` which has no streamable counterpart). 
        // Since actions have correlating ids, apply all up to the last received id. 

        // To raise all proper events, even for actions that are not streamable (and no acknowledgement 
        // is received), apply all actions one at a time until reching target id. 
        int nextId = this._motionCursor.GetNextActionId();

        if (nextId <= id)
        {
            while (nextId <= id)
            {
                this._motionCursor.ApplyNextAction();

                // Raise appropriate events
                this._parentDriver.parentControl.RaiseActionExecutedEvent();

                nextId++;
            }
        }
        else
        {
            throw new Exception("WEIRD ERROR, TAKE A LOOK AT THIS!");
        }

        return true;
    }

    private bool ProcessResponse(List<int> values)
    {
        string res = string.Join(",", values.ToArray());
        logger.Debug("Response: \"" + res + "\"");

        // OMG THIS IS QUICK AND DIRTY...
        switch (values[0])
        {
            case URCommunicationProtocol.RES_FULL_POSE:
                if (values.Count != 13)
                {
                    Logger.Debug("Improperly formatted response: " + res);
                }

                initPos = new Vector(1000 * values[1] / 10000.0, 1000 * values[2] / 10000.0, 1000 * values[3] / 10000.0);

                initRot = new Rotation(new RotationVector(Geometry.TO_DEGS * values[4] / 10000.0,
                    Geometry.TO_DEGS * values[5] / 10000.0, Geometry.TO_DEGS * values[6] / 10000.0).ToQuaternion());

                initAx = new Joints(Geometry.TO_DEGS * values[7] / 10000,
                    Geometry.TO_DEGS * values[8] / 10000,
                    Geometry.TO_DEGS * values[9] / 10000,
                    Geometry.TO_DEGS * values[10] / 10000,
                    Geometry.TO_DEGS * values[11] / 10000,
                    Geometry.TO_DEGS * values[12] / 10000);

                logger.Debug("Received robot pose:");
                logger.Debug(initPos);
                logger.Debug(initRot);
                logger.Debug(initAx);

                break;
        }

        return true;
    }

    private string LoadEmptyScript()
    {
        string emptyScript = IO.ReadTextResource("Machina.Resources.DriverModules.UR.empty.script");
        return emptyScript;
    }

    private string LoadDriverScript()
    {
        string driverScript = IO.ReadTextResource("Machina.Resources.DriverModules.UR.machina_ur_driver.script");

        // @TODO: remove comments, trailing spaces and empty lines from script
        driverScript = driverScript.Replace("{{HOSTNAME}}", _serverIP);
        driverScript = driverScript.Replace("{{PORT}}", _serverPort.ToString());

        return driverScript;
    }

    private bool UploadScriptToDevice(string script, bool consoleDump = false)
    {
        if (ClientSocketStatus != TCPConnectionStatus.Connected)
        {
            logger.Error("Not connected to device");
            return false;
        }

        logger.Debug("Uploading module to device...");
        if (consoleDump) logger.Debug(script);

        try
        {
            _sendMsgBytes = Encoding.ASCII.GetBytes(script);
            _clientNetworkStream.Write(_sendMsgBytes, 0, _sendMsgBytes.Length);
        }
        catch (Exception ex)
        {
            logger.Error("Something went wrong trying to upload module to robot:");
            logger.Error(ex);
            return false;
        }

        return true;
    }

    private int CalculateRemaining()
    {
        int slen = _sentIDs.Count;
        int rlen = _receivedIDs.Count;
        if (rlen == 0) return slen;

        int lastReceivedID = _receivedIDs.Last();
        int remaining = 0;
        for (int i = slen - 1; i >= 0; i--)
        {
            if (_sentIDs[i] == lastReceivedID)
            {
                remaining = slen - 1 - i;
                break;
            }
        }
        return remaining;
    }

    private void DebugLists()
    {
        logger.Debug("SENT IDS: ");
        string ids = "";
        foreach (var id in _sentIDs) ids += id + ", ";
        logger.Debug(ids);
        logger.Debug("");

        logger.Debug("RCVD IDS: ");
        ids = "";
        foreach (var id in _receivedIDs) ids += id + ", ";
        logger.Debug(ids);
        logger.Debug("");
    }

    #endregion
}
