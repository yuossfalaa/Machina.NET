using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.ComponentModel;
using Machina.Controllers;
using Machina.Drivers;
using Machina.Types.Geometry;
using Machina.Types.Data;

namespace Machina;

/// <summary>
/// The core class that centralizes all private control.
/// </summary>
class Control
{
    #region Internal & Private Vars

    /// <summary>
    /// Operation modes by default
    /// </summary>
    internal ControlType _controlMode;
    internal ControlManager _controlManager;


    internal CycleType runMode = DEFAULT_RUNMODE;
    internal ConnectionType connectionMode;


    /// <summary>
    /// A reference to the Robot object this class is driving.
    /// </summary>
    internal Robot parentRobot;

    /// <summary>
    /// A reference to the parent Robot's Logger object.
    /// </summary>
    internal RobotLogger logger;

    /// <summary>
    /// Instances of the main robot Controller and Task
    /// </summary>
    private Driver _driver;
    internal Driver Driver { get { return _driver; } set { _driver = value; } }


    /// <summary>
    /// A mutable alias for the cursor that will be used to return the most recent state for the robot,
    /// a.k.a. which cursor to use for sync GetJoints(), GetPose()-kind of functions...
    /// Mainly the issueCursor for Offline modes, executionCursor for Stream, etc.
    /// </summary>
    private RobotCursor _stateCursor;

    /// <summary>
    /// Are cursors ready to start working?
    /// </summary>
    private bool _areCursorsInitialized = false;



    /// <summary>
    /// A shared instance of a Thread to manage sending and executing actions
    /// in the controller, which typically takes a lot of resources
    /// and halts program execution.
    /// </summary>
    private Thread actionsExecuter;


    //// @TODO: this will need to get reallocated when fixing stream mode...
    //public StreamQueue streamQueue;

    /// <summary>
    /// A new instance rolling counter for action ids, to replace the previous static one. 
    /// </summary>
    private int _actionCounter = 1;


    #endregion

    #region Public Vars
    // Some 'environment variables' to define check states and behavior
    public const bool SAFETY_STOP_IMMEDIATE_ON_DISCONNECT = true;         // when disconnecting from a controller, issue an immediate Stop request?

    // @TODO: move to cursors, make it device specific
    public const double DEFAULT_SPEED = 20;                                 // default speed for new actions in mm/s and deg/s
    public const double DEFAULT_ACCELERATION = 30;                          // default acc for new actions in mm/s^2 and deg/s^2; zero values let the controller figure out accelerations
    public const double DEFAULT_PRECISION = 5;                              // default precision for new actions

    public const MotionType DEFAULT_MOTION_TYPE = MotionType.Linear;        // default motion type for new actions
    public const ReferenceCS DEFAULT_REFCS = ReferenceCS.World;             // default reference coordinate system for relative transform actions
    public const ControlType DEFAULT_CONTROLMODE = ControlType.Offline;
    public const CycleType DEFAULT_RUNMODE = CycleType.Once;
    public const ConnectionType DEFAULT_CONNECTIONMODE = ConnectionType.User;

    public ControlType ControlMode { get { return _controlMode; } internal set { _controlMode = value; } }

    public IssueActionManager IssueActionManager;

    // Cursors
    private RobotCursor _issueCursor, _releaseCursor, _executionCursor, _motionCursor;
    /// <summary>
    /// A virtual representation of the state of the device after application of issued actions.
    /// </summary>
    public RobotCursor IssueCursor => _issueCursor;

    /// <summary>
    /// A virtual representation of the state of the device after releasing pending actions to the controller.
    /// Keeps track of the state of an issue robot immediately following all the actions released from the 
    /// actionsbuffer to target device defined by controlMode, like an offline program, a full intruction execution 
    /// or a streamed target.
    /// </summary>
    public RobotCursor ReleaseCursor => _releaseCursor;

    /// <summary>
    /// A virtual representation of the state of the device after an action has been executed. 
    /// </summary>
    public RobotCursor ExecutionCursor => _executionCursor;

    /// <summary>
    /// A virtual representation of the state of the device tracked in pseudo real time. 
    /// Is independent from the other cursors, and gets updated (if available) at periodic intervals from the controller. 
    /// </summary>
    public RobotCursor MotionCursor => _motionCursor;

    #endregion

    #region Ctor
    /// <summary>
    /// Main constructor.
    /// </summary>
    public Control(Robot parentBot)
    {
        parentRobot = parentBot;
        logger = parentRobot.logger;

        _executionCursor = new RobotCursor(this, "ExecutionCursor", false, null);
        _releaseCursor = new RobotCursor(this, "ReleaseCursor", false, _executionCursor);
        _issueCursor = new RobotCursor(this, "IssueCursor", true, _releaseCursor);
        _issueCursor.LogRelativeActions = true;

        IssueActionManager = new IssueActionManager(this);

        SetControlMode(DEFAULT_CONTROLMODE);
        SetConnectionMode(DEFAULT_CONNECTIONMODE);
    }
    #endregion

    #region Setters Methods

    /// <summary>
    /// Sets current Control Mode and establishes communication if applicable.
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    public bool SetControlMode(ControlType mode)
    {
        _controlMode = mode;

        return ResetControl();
    }
    /// <summary>
    /// Sets the current ConnectionManagerType.
    /// </summary>
    /// <param name="mode"></param>
    /// <returns></returns>
    public bool SetConnectionMode(ConnectionType mode)
    {
        if (_driver == null)
        {
            throw new Exception("Missing Driver object");
        }

        if (!_driver.AvailableConnectionTypes[mode])
        {
            logger.Warning($"This device's driver does not accept ConnectionType {mode}, ConnectionMode remains {this.connectionMode}");
            return false;
        }

        this.connectionMode = mode;

        return ResetControl();
    }

    /// <summary>
    /// Sets the creddentials for logging into the controller.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public bool SetUserCredentials(string name, string password) =>
        _driver != null && _driver.SetUser(name, password);

    #endregion

    #region Getters Methods
    /// <summary>
    /// If connected to a device, return the IP address
    /// </summary>
    /// <returns></returns>
    public string GetControllerIP() => _driver.IP;

    internal Dictionary<string, string> GetDeviceDriverModules(Dictionary<string, string> parameters)
    {
        if (_controlMode == ControlType.Offline)
        {
            logger.Warning("Could not retrieve driver modules in Offline mode, must define a ConnectionMode first.");
            return null;
        }

        return _driver.GetDeviceDriverModules(parameters);
    }

    /// <summary>
    /// Returns a Vector object representing the current robot's TCP position.
    /// </summary>
    /// <returns></returns>
    public Vector GetCurrentPosition() => _stateCursor.position;

    /// <summary>
    /// Returns a Rotation object representing the current robot's TCP orientation.
    /// </summary>
    /// <returns></returns>
    public Rotation GetCurrentRotation() => _stateCursor.rotation;

    /// <summary>
    /// Returns a Joints object representing the rotations of the 6 axes of this robot.
    /// </summary>
    /// <returns></returns>
    public Joints GetCurrentAxes() => _stateCursor.axes;

    /// <summary>
    /// Returns a double?[] array representing the values for the external axes.
    /// </summary>
    /// <returns></returns>
    public ExternalAxes GetCurrentExternalAxes() => _stateCursor.externalAxesCartesian;



    /// <summary>
    /// Gets current speed setting.
    /// </summary>
    /// <returns></returns>
    public double GetCurrentSpeedSetting() => _stateCursor.speed;

    /// <summary>
    /// Gets current scceleration setting.
    /// </summary>
    /// <returns></returns>
    public double GetCurrentAccelerationSetting() => _stateCursor.acceleration;

    /// <summary>
    /// Gets current precision setting.
    /// </summary>
    /// <returns></returns>
    public double GetCurrentPrecisionSetting() => _stateCursor.precision;

    /// <summary>
    /// Gets current Motion setting.
    /// </summary>
    /// <returns></returns>
    public MotionType GetCurrentMotionTypeSetting() => _stateCursor.motionType;

    /// <summary>
    /// Gets the reference coordinate system used for relative transform actions.
    /// </summary>
    /// <returns></returns>
    public ReferenceCS GetCurrentReferenceCS()
    {
        return IssueCursor.referenceCS;
    }

    /// <summary>
    /// Returns a Tool object representing the currently attached tool, null if none.
    /// </summary>
    /// <returns></returns>
    public Tool GetCurrentTool() => _stateCursor.tool;


    #endregion

    #region Connection Handling Methods
    /// <summary>
    /// Searches the network for a robot controller and establishes a connection with the specified one by position. 
    /// Necessary for "online" modes.
    /// </summary>
    /// <returns></returns>
    public bool ConnectToDevice(int robotId)
    {
        if (connectionMode == ConnectionType.User)
        {
            logger.Error("Cannot search for robots automatically, please use ConnectToDevice(ip, port) instead");
            return false;
        }

        // Sanity
        if (!_driver.ConnectToDevice(robotId))
        {
            logger.Error("Cannot connect to device");
            return false;
        }
        else
        {
            // If successful, initialize robot cursors to mirror the state of the device.
            // The function will initialize them based on the _comm object.
            InitializeRobotCursors();
        }

        logger.Info("Connected to " + parentRobot.Brand + " robot \"" + parentRobot.Name + "\" on " + _driver.IP + ":" + _driver.Port);

        return true;
    }

    /// <summary>
    /// Searches for a robot using Ip and Port and establishes a connection with the specified one 
    /// </summary>
    /// <returns></returns>
    public bool ConnectToDevice(string ip, int port)
    {
        if (connectionMode == ConnectionType.Machina)
        {
            logger.Error("Try ConnectToDevice() instead");
            return false;
        }

        // Sanity
        if (!_driver.ConnectToDevice(ip, port))
        {
            logger.Error("Cannot connect to device");
            return false;
        }
        else
        {
            InitializeRobotCursors();
        }

        logger.Info("Connected to " + parentRobot.Brand + " robot \"" + parentRobot.Name + "\" on " + _driver.IP + ":" + _driver.Port);
        logger.Verbose("TCP:");
        logger.Verbose("  " + this.IssueCursor.position.ToString(true));
        logger.Verbose("  " + new Orientation(this.IssueCursor.rotation).ToString(true));
        logger.Verbose("  " + this.IssueCursor.axes.ToString(true));
        if (this.IssueCursor.externalAxesCartesian != null)
        {
            logger.Verbose("External Axes (TCP):");
            logger.Verbose("  " + this.IssueCursor.externalAxesCartesian.ToString(true));
        }
        if (this.IssueCursor.externalAxesJoints != null)
        {
            logger.Verbose("External Axes (J): ");
            logger.Verbose("  " + this.IssueCursor.externalAxesJoints.ToString(true));
        }
        return true;
    }

    /// <summary>
    /// Requests the Communication object to disconnect from controller and reset.
    /// </summary>
    /// <returns></returns>
    public bool DisconnectFromDevice()
    {
        bool result = _driver.DisconnectFromDevice();
        if (result)
        {
            logger.Info("Disconnected from " + parentRobot.Brand + " robot \"" + parentRobot.Name + "\"");
        }
        else
        {
            logger.Warning("Could not disconnect from " + parentRobot.Brand + " robot \"" + parentRobot.Name + "\"");
        }

        return result;
    }

    /// <summary>
    /// Is this robot connected to a real/virtual device?
    /// </summary>
    /// <returns></returns>
    public bool IsConnectedToDevice()
    {
        return _driver.Connected;
    }

    #endregion

    #region Export Methods
    /// <summary>
    /// For Offline modes, it flushes all pending actions and returns a devide-specific program 
    /// as a nested string List, representing the different program files.
    /// </summary>
    /// <param name="inlineTargets">Write inline targets on action statements, or declare them as independent variables?</param>
    /// <param name="humanComments">If true, a human-readable description will be added to each line of code</param>
    /// <returns></returns>
    public RobotProgram Export(bool inlineTargets, bool humanComments)
    {
        if (_controlMode != ControlType.Offline)
        {
            logger.Warning("Export() only works in Offline mode");
            return null;
        }

        var robotProgram = ReleaseCursor.FullProgramFromBuffer(inlineTargets, humanComments);

        return robotProgram;
    }

    #endregion

    #region Issue Action Request Methods
    /// <summary>
    /// Issues an action from a stringified instruction form, such as "Move(100, 0, 0)".
    /// Handles overloaded methods by matching the parameter types.
    /// </summary>
    /// <param name="statement">The action command to execute</param>
    /// <returns>Whether the execution was successful</returns>
    public bool IssueApplyActionRequestFromStringStatement(string statement)
    {
        if (!IsValidStatement(statement))
            return false;

        if (!TryParseStatement(statement, out string[] args))
            return false;

        string methodName = args[0];
        var providedArgs = args.Skip(1).ToArray(); // Exclude method name
        object[] convertedParams = new object[providedArgs.Length];

        // Find all methods with the same name (case-sensitive)
        var candidateMethods = Robot._reflectedAPI
                                    .Where(kv => kv.Key.Equals(methodName, StringComparison.Ordinal))
                                    .Select(kv => kv.Value)
                                    .ToArray();

        //@TODO:Why is this case Sensitive , 'Move' will move the robot and 'move' will send you to space ??
        //TryGetCandidate
        if (!TryGetCaseInsensitiveCandidate(candidateMethods, methodName))
            return false;

        // Filter candidate methods by matching parameter counts and types
        if (!TryGetMatchingMethod(candidateMethods, methodName, providedArgs, out MethodInfo matchedMethod))
            return false;

        // Correct number of parameters?
        if (!TryGetParameterInfo(matchedMethod, args, methodName, out ParameterInfo[] paramInfos))
            return false;

        // Convert parameters to the correct types
        if (!TryConvertParamaters(providedArgs, ref convertedParams, paramInfos))
            return false;

        // Handle optional parameters (fill with defaults)
        HandelOptionalParamaters(providedArgs, ref convertedParams, paramInfos);

        // Invoke the method and handle the return value
        return InvokeMethod(methodName, convertedParams, matchedMethod);
    }

    /// <summary>
    /// Issue an Action of whatever kind...
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool IssueApplyActionRequest(Action action)
    {
        if (!_areCursorsInitialized)
        {
            logger.Error("Cursors not initialized. Did you .Connect()?");
            return false;
        }

        // Use this robot instance to id the action.
        action.Id = _actionCounter++;

        bool success = IssueCursor.Issue(action);

        if (success) RaiseActionIssuedEvent();

        return success;
    }

    /// <summary>
    /// Tries to find the best matching method based on the argument types and count.
    /// </summary>
    /// <param name="candidateMethods">The candidate methods with the same name</param>
    /// <param name="providedArgs">The arguments from the user</param>
    /// <returns>The matching method or null if no match found</returns>
    private MethodInfo FindMatchingMethod(MethodInfo[] candidateMethods, string[] providedArgs)
    {
        foreach (var method in candidateMethods)
        {
            ParameterInfo[] parameters = method.GetParameters();

            // Match on argument count first
            if (parameters.Length >= providedArgs.Length)
            {
                bool allParametersMatch = true;
                for (int i = 0; i < providedArgs.Length; i++)
                {
                    if (!TryConvertParameter(providedArgs[i], parameters[i].ParameterType, out _))
                    {
                        allParametersMatch = false;
                        break;
                    }
                }

                if (allParametersMatch)
                {
                    return method;
                }
            }
        }

        return null; // No match found
    }
    /// <summary>
    /// Tries to convert a string value into a target type.
    /// </summary>
    /// <param name="input">The input string to convert</param>
    /// <param name="targetType">The target type for the conversion</param>
    /// <param name="convertedValue">The output converted value</param>
    /// <returns>True if conversion succeeded, otherwise false</returns>
    private bool TryConvertParameter(string input, Type targetType, out object convertedValue)
    {
        try
        {
            TypeConverter converter = TypeDescriptor.GetConverter(targetType);
            if (converter != null && converter.CanConvertFrom(typeof(string)))
            {
                convertedValue = converter.ConvertFromInvariantString(input);
                return true;
            }
        }
        catch
        {
            logger.Error($"Could not parse \"{input}\" into a {targetType}.");

        }

        convertedValue = null;
        return false;
    }

    private bool InvokeMethod(string methodName, object[] convertedParams, MethodInfo matchedMethod)
    {
        try
        {
            object result = matchedMethod.Invoke(this.parentRobot, convertedParams);

            if (result is bool success)
            {
                return success;
            }
            else
            {
                logger.Error($"Unexpected return type from \"{methodName}\". Expected bool.");
                return false;
            }
        }
        catch (Exception ex)
        {
            logger.Error($"Error executing method \"{methodName}\": {ex.Message}");
            return false;
        }
    }

    private static void HandelOptionalParamaters(string[] providedArgs, ref object[] convertedParams, ParameterInfo[] paramInfos)
    {
        for (int i = providedArgs.Length; i < paramInfos.Length; i++)
        {
            convertedParams[i] = paramInfos[i].DefaultValue ?? Type.Missing;
        }
    }

    private bool TryConvertParamaters(string[] providedArgs, ref object[] convertedParams, ParameterInfo[] paramInfos)
    {
        for (int i = 0; i < providedArgs.Length; i++)
        {
            Type targetType = paramInfos[i].ParameterType;
            if (!TryConvertParameter(providedArgs[i], targetType, out convertedParams[i]))
            {
                logger.Error($"Could not convert \"{providedArgs[i]}\" to type {targetType.Name}.");
                return false;
            }
        }
        return true;
    }

    private bool TryGetParameterInfo(MethodInfo matchedMethod, string[] args, string methodName, out ParameterInfo[] paramInfos)
    {
        paramInfos = matchedMethod.GetParameters();
        // how many did the user missed providing
        int missingParameterCount = paramInfos.Length - args.Length + 1;
        if (paramInfos.Length != args.Length - 1)
        {
            // Check if the method contains any optional parameters
            // how many parameters are optional in the method (have default values)?
            int optionalParameterCount = paramInfos.Count((param) => param.IsOptional);

            if (paramInfos.Length < args.Length - 1 ||              // too many args provided
                missingParameterCount > optionalParameterCount)     // less than minimum
            {
                logger.Error($"Incorrect amount of parameters for \"{methodName}\", please use as \"{matchedMethod}\"");
                return false;
            }
            else
            {
                logger.Debug($"Detected action entry with {optionalParameterCount} optional parameters.");
                return true;
            }
        }
        return true;
    }

    private bool TryGetMatchingMethod(MethodInfo[] candidateMethods, string methodName, string[] providedArgs, out MethodInfo matchedMethod)
    {
        matchedMethod = FindMatchingMethod(candidateMethods, providedArgs);
        if (matchedMethod == null)
        {
            logger.Error($"No suitable method found with matching parameters for \"{methodName}\".");
            return false;
        }
        return true;
    }

    private bool IsValidStatement(string statement)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            logger.Error("Instruction statement cannot be null or empty.");
            return false;
        }
        return true;
    }

    private bool TryParseStatement(string statement, out string[] args)
    {
        // Parse the statement into method name and arguments
        args = Machina.Utilities.Parsing.ParseStatement(statement);
        if (args == null || args.Length == 0)
        {
            logger.Error($"Invalid instruction: \"{statement}\"");
            return false;
        }
        return true;
    }

    private bool TryGetCaseInsensitiveCandidate(MethodInfo[] candidateMethods, string methodName)
    {
        if (candidateMethods.Length == 0)
        {
            if (Robot._reflectedAPICaseInsensitive.TryGetValue(methodName, out MethodInfo methodNoCasing))
            {
                logger.Error($"Did you mean \"{methodNoCasing.Name}\"? Remember, Machina is case-sensitive.");
            }
            else
            {
                logger.Error($"No method found with the name \"{methodName}\".");
            }
            return false;
        }
        return true;
    }
    #endregion

    #region Private Methods
    /// <summary>
    /// Resets control parameters using the appropriate ControlManager.
    /// </summary>
    /// <returns></returns>
    private bool ResetControl()
    {
        _controlManager = ControlFactory.GetControlManager(this);

        bool success = _controlManager.Initialize();

        if (ControlMode == ControlType.Offline)
        {
            InitializeRobotCursors(null, Rotation.FlippedAroundY, null);    // @TODO: this should depend on the Robot brand, model, cursor and so many other things... Added this quick fix to allow programs to start with MoveTo() instructions.
        }

        if (!success)
        {
            logger.Error("Couldn't SetControlMode()");
            throw new Exception("Couldn't SetControlMode()");
        }

        return success;
    }

    internal bool ConfigureBuffer(int minActions, int maxActions)
    {
        return this._driver.ConfigureBuffer(minActions, maxActions);
    }

    /// <summary>
    /// Disconnects and resets the Communication object.
    /// </summary>
    /// <returns></returns>
    private bool DropCommunication()
    {
        if (_driver == null)
        {
            logger.Debug("Communication protocol not established, no DropCommunication() performed.");
            return false;
        }
        bool success = _driver.DisconnectFromDevice();
        _driver = null;
        return success;
    }

    /// <summary>
    /// Initializes all instances of robotCursors with base information
    /// </summary>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <param name="joints"></param>
    /// <returns></returns>
    internal bool InitializeRobotCursors(Point position = null, Rotation rotation = null, Joints joints = null, ExternalAxes extAx = null,
        double speed = Control.DEFAULT_SPEED, double acc = Control.DEFAULT_ACCELERATION, double precision = Control.DEFAULT_PRECISION,
        MotionType mType = Control.DEFAULT_MOTION_TYPE, ReferenceCS refCS = Control.DEFAULT_REFCS)

    {
        bool success = true;
        success &= IssueCursor.Initialize(position, rotation, joints, extAx, speed, acc, precision, mType, refCS);
        success &= ReleaseCursor.Initialize(position, rotation, joints, extAx, speed, acc, precision, mType, refCS);
        success &= ExecutionCursor.Initialize(position, rotation, joints, extAx, speed, acc, precision, mType, refCS);

        _areCursorsInitialized = success;

        return success;
    }


    internal bool InitializeRobotCursors()
    {
        if (_driver == null)
        {
            throw new Exception("Cannot initialize Robotcursors without a _comm object");
        }

        // If successful, initialize robot cursors to mirror the state of the device
        Vector currPos = _driver.GetCurrentPosition();
        Rotation currRot = _driver.GetCurrentOrientation();
        Joints currJnts = _driver.GetCurrentJoints();
        ExternalAxes currExtAx = _driver.GetCurrentExternalAxes();

        return InitializeRobotCursors(currPos, currRot, currJnts, currExtAx);
    }

    internal bool InitializeMotionCursor()
    {
        _motionCursor = new RobotCursor(this, "MotionCursor", false, null);
        //_motionCursor.Initialize();  // No need for this, since this is just a "zombie" cursor, a holder of static properties updated in real-time with no actions applied to it. Any init info is negligible.
        return true;
    }

    /// <summary>
    /// Sets which cursor to use as most up-to-date tracker.
    /// </summary>
    /// <param name="cursor"></param>
    internal void SetStateCursor(RobotCursor cursor)
    {
        this._stateCursor = cursor;
    }
    #endregion

    #region Debug Methods
    public void DebugDump()
    {
        DebugBanner();
        _driver.DebugDump();
    }

    public void DebugBuffers()
    {
        logger.Debug("VIRTUAL BUFFER:");
        IssueCursor.LogBufferedActions();

        logger.Debug("WRITE BUFFER:");
        ReleaseCursor.LogBufferedActions();

        logger.Debug("MOTION BUFFER");
        ExecutionCursor.LogBufferedActions();
    }

    public void DebugRobotCursors()
    {
        if (IssueCursor == null)
            logger.Debug("Virtual cursor not initialized");
        else
            logger.Debug(IssueCursor);

        if (ReleaseCursor == null)
            logger.Debug("Write cursor not initialized");
        else
            logger.Debug(ReleaseCursor);

        if (ExecutionCursor == null)
            logger.Debug("Motion cursor not initialized");
        else
            logger.Debug(ReleaseCursor);
    }

    /// <summary>
    /// Printlines a "DEBUG" ASCII banner... ;)
    /// </summary>
    private void DebugBanner()
    {
        logger.Debug("");
        logger.Debug("██████╗ ███████╗██████╗ ██╗   ██╗ ██████╗ ");
        logger.Debug("██╔══██╗██╔════╝██╔══██╗██║   ██║██╔════╝ ");
        logger.Debug("██║  ██║█████╗  ██████╔╝██║   ██║██║  ███╗");
        logger.Debug("██║  ██║██╔══╝  ██╔══██╗██║   ██║██║   ██║");
        logger.Debug("██████╔╝███████╗██████╔╝╚██████╔╝╚██████╔╝");
        logger.Debug("╚═════╝ ╚══════╝╚═════╝  ╚═════╝  ╚═════╝ ");
        logger.Debug("");
    }

    #endregion

    #region Events Methods
    /// <summary>
    /// Use this to trigger an `ActionIssued` event.
    /// </summary>
    internal void RaiseActionIssuedEvent()
    {
        Action lastAction = this.IssueCursor.GetLastAction();

        ActionIssuedEventArgs args = new ActionIssuedEventArgs(lastAction, this.GetCurrentPosition(), this.GetCurrentRotation(), this.GetCurrentAxes(), this.GetCurrentExternalAxes());

        this.parentRobot.OnActionIssued(args);
    }

    /// <summary>
    /// Use this to trigger an `ActionReleased` event.
    /// </summary>
    internal void RaiseActionReleasedEvent()
    {
        Action lastAction = this.ReleaseCursor.GetLastAction();
        int pendingRelease = this.ReleaseCursor.ActionsPendingCount();

        ActionReleasedEventArgs args = new ActionReleasedEventArgs(lastAction, pendingRelease, GetCurrentPosition(), GetCurrentRotation(), GetCurrentAxes(), GetCurrentExternalAxes());

        this.parentRobot.OnActionReleased(args);
    }

    /// <summary>
    /// Use this to trigger an `ActionExecuted` event.
    /// </summary>
    internal void RaiseActionExecutedEvent()
    {
        Action lastAction = this.ExecutionCursor.GetLastAction();
        int pendingExecutionOnDevice = this.ExecutionCursor.ActionsPendingCount();
        int pendingExecutionTotal = this.ReleaseCursor.ActionsPendingCount() + pendingExecutionOnDevice;

        ActionExecutedEventArgs args = new ActionExecutedEventArgs(lastAction, pendingExecutionOnDevice, pendingExecutionTotal, this.GetCurrentPosition(), this.GetCurrentRotation(), this.GetCurrentAxes(), this.GetCurrentExternalAxes());

        this.parentRobot.OnActionExecuted(args);
    }

    /// <summary>
    /// Use this to trigger a `MotionUpdate` event.
    /// </summary>
    internal void RaiseMotionUpdateEvent()
    {
        MotionUpdateEventArgs args = new MotionUpdateEventArgs(this.MotionCursor.position, this.MotionCursor.rotation, this.MotionCursor.axes, this.MotionCursor.externalAxesCartesian);

        this.parentRobot.OnMotionUpdate(args);
    }

    #endregion

}
