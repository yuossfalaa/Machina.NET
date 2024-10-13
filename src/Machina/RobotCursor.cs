using System;
using System.Collections.Generic;
using Machina.Types.Geometry;
using Machina.Types.Data;

namespace Machina;

/// <summary>
/// Represents an abstraction of the state of a robotic device. 
/// Keeps track of things such as position, orientation, joint configuration,
/// current speed, zone, etc.
/// Useful as virtual representation of a simulated or controlled robot actuator. 
/// </summary>
internal class RobotCursor
{
    #region Variables
    private bool _logRelativeActions = false;
    public bool LogRelativeActions
    {
        get { return _logRelativeActions; }
        set { _logRelativeActions = value; }
    }

    // Public props
    public string name;
    public Vector position, prevPosition;
    public Rotation rotation, prevRotation;
    public Joints axes, prevAxes;
    public double speed, acceleration, precision;
    public double? armAngle;
    public MotionType motionType;
    public ReferenceCS referenceCS;
    public Tool tool;

    // Because of YuMi, now we are keeping track of two sets of external axes! @TODO: make this less ABB-centric somehow?
    public ExternalAxes externalAxesCartesian;
    public ExternalAxes externalAxesJoints;

    // Keep a dictionary of all Tools that have been defined on this robot, and are available for Attach/Detach
    internal Dictionary<string, Tool> availableTools;

    // Some robots use ints as pin identifiers (UR, KUKA), while others use strings (ABB). 
    // All pin ids are stored as strings, and are parsed to ints internally if possible. 
    internal Dictionary<string, bool> digitalOutputs;
    internal Dictionary<string, double> analogOutputs;


    // 3D printing
    public bool isExtruding;
    public double extrusionRate;
    public Dictionary<RobotPartType, double> partTemperature;
    public double extrudedLength, prevExtrudedLength;  // the length of filament that has been extruded, i.e. the "E" parameter

    /// <summary>
    /// Last Action that was applied to this cursor
    /// </summary>
    public Action lastAction = null;
    protected bool initialized = false;
    private bool applyImmediately = false;  // when an action is issued to this cursor, apply it immediately?


    /// <summary>
    /// Who manages this Cursor?
    /// </summary>
    public Control parentControl;

    /// <summary>
    /// A reference to this parent's Robot Logger.
    /// </summary>
    internal RobotLogger logger;

    /// <summary>
    /// Specified RobotCursor instance will be issued all Actions 
    /// released from this one. 
    /// </summary>
    public RobotCursor child;

    /// <summary>
    /// Robot program compilers now belong to the RobotCursor. 
    /// It makes it easier to attach the right device-specific type, 
    /// and to use the cursor's information to generate the program. 
    /// </summary>
    public Compiler compiler;

    /// <summary>
    /// A buffer that stores Push and PopSettings() states.
    /// </summary>
    internal SettingsBuffer settingsBuffer;

    /// <summary>
    /// Manages pending and released Actions, plus blocks. 
    /// </summary>
    public ActionBuffer actionBuffer;

    /// <summary>
    /// A lock for buffer manipulation operations. 
    /// </summary>
    public readonly object actionBufferLock = new object();




    #endregion

    #region Ctor & Initialization

    /// <summary>
    /// Main constructor.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="applyImmediately"></param>
    public RobotCursor(Control parentControl, string name, bool applyImmediately, RobotCursor childCursor)
    {
        this.parentControl = parentControl;
        this.logger = parentControl.logger;
        this.name = name;
        this.applyImmediately = applyImmediately;
        this.child = childCursor;

        // @TODO: make this programmatic
        compiler = this.parentControl.parentRobot.Brand switch
        {
            RobotType.HUMAN => new CompilerHuman(),
            RobotType.MACHINA => new CompilerMACHINA(),
            RobotType.ABB => new CompilerABB(),
            RobotType.UR => new CompilerUR(),
            RobotType.KUKA => new CompilerKUKA(),
            RobotType.ZMORPH => new CompilerZMORPH(),
            _ => throw new InvalidOperationException("Unsupported Robot Type"),
        };

        // Initialize buffers
        actionBuffer = new ActionBuffer(this);
        settingsBuffer = new SettingsBuffer();
    }
    /// <summary>
    /// Minimum information necessary to initialize a robot object.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <param name="joints"></param>
    /// <returns></returns>
    public bool Initialize(Vector position, Rotation rotation, Joints joints, ExternalAxes extAx,
        double speed, double acceleration, double precision, MotionType mType, ReferenceCS refCS)
    {
        if (position != null)
        {
            this.position = new Vector(position);
            this.prevPosition = new Vector(position);
        }
        if (rotation != null)
        {
            this.rotation = new Rotation(rotation);
            this.prevRotation = new Rotation(rotation);
        }
        if (joints != null)
        {
            this.axes = new Joints(joints);
            this.prevAxes = new Joints(joints);
        }
        if (extAx != null)
        {
            // @TODO split this definition
            this.externalAxesCartesian = new ExternalAxes(extAx);
            this.externalAxesJoints = new ExternalAxes(extAx);
        }

        this.acceleration = acceleration;
        this.speed = speed;
        this.precision = precision;
        this.motionType = mType;
        this.referenceCS = refCS;

        this.availableTools = new Dictionary<string, Tool>();

        // Add a "noTool" default object and make it the default.
        //this.availableTools["noTool"] = Tool.Create("noTool", 0, 0, 0, 1, 0, 0, 0, 1, 0, 0.001, 0, 0, 0);
        //this.tool = this.availableTools["noTool"];

        this.tool = null;  // reverted back to default `null` tool...

        this.digitalOutputs = new Dictionary<string, bool>();
        this.analogOutputs = new Dictionary<string, double>();


        // Initialize temps to zero
        this.partTemperature = new Dictionary<RobotPartType, double>();
        foreach (RobotPartType part in Enum.GetValues(typeof(RobotPartType)))
        {
            partTemperature[part] = 0;
        }
        isExtruding = false;
        extrusionRate = 0;
        extrudedLength = 0;

        this.initialized = true;
        return this.initialized;
    }

    #endregion

    #region Issue Action Methods

    /// <summary>
    /// Add an action to this cursor's buffer, to be released whenever assigned priority.
    /// </summary>
    /// <param name="action"></param>
    public bool Issue(Action action)
    {
        lock (actionBufferLock)
        {
            actionBuffer.Add(action);
            if (applyImmediately)
            {
                return ApplyNextAction();
            }
            return true;
        }

    }

    /// <summary>
    /// Applies next single action pending in the buffer.
    /// </summary>
    /// <returns></returns>
    public bool ApplyNextAction()
    {
        lock (actionBufferLock)
        {
            lastAction = actionBuffer.GetNext();
            if (lastAction == null) return false;
            bool success = Apply(lastAction);
            if (success && child != null)
            {
                child.Issue(lastAction);
            }
            return success;
        }
    }

    /// <summary>
    /// Applies next single action pending in the buffer and outs that action.
    /// </summary>
    /// <param name="lastAction"></param>
    /// <returns></returns>
    public bool ApplyNextAction(out Action lastAction)
    {
        lock (actionBufferLock)
        {
            lastAction = actionBuffer.GetNext();
            if (lastAction == null) return false;
            bool success = Apply(lastAction);
            if (success && child != null)
            {
                child.Issue(lastAction);
            }
            return success;
        }
    }


    /// <summary>
    /// Ascends the pending actions buffer searching for the given id, and applies
    /// them all, inclusive of the one searched. 
    /// This assumes ids are correlative and ascending, will stop if it finds an
    /// id larger thatn the given one. 
    /// </summary>
    /// <param name="id"></param>
    /// <returns></returns>
    public List<Action> ApplyActionsUntilId(int id)
    {
        lock (actionBufferLock)
        {
            var actions = actionBuffer.GetAllUpToId(id);
            bool success = true;

            foreach (Action a in actions)
            {
                success &= Apply(a);
                if (success && child != null)
                {
                    child.Issue(a);
                    lastAction = a;
                }
            }

            return actions;
        }
    }
    #endregion

    #region Navigate Actions Methods
    /// <summary>
    /// Returns the last Action that was released by the buffer.
    /// </summary>
    /// <returns></returns>
    public Action GetLastAction()
    {
        lock (actionBufferLock)
        {
            return actionBuffer.GetLast();
        }
    }
    /// <summary>
    /// Returns the id of the next Action pending to be applied. 
    /// </summary>
    /// <returns></returns>
    internal int GetNextActionId() => actionBuffer.QueryIdOfNext();

    /// <summary>
    /// Requests all un-blocked pending Actions in the buffer to be flagged
    /// as a block. 
    /// </summary>
    public void QueueActions()
    {
        actionBuffer.SetBlock();
    }

    /// <summary>
    /// Are there Actions pending in the buffer?
    /// </summary>
    /// <returns></returns>
    public bool AreActionsPending()
    {
        return actionBuffer.AreActionsPending();
    }

    /// <summary>
    /// Returns the number of actions pending in this cursor's buffer.
    /// </summary>
    /// <returns></returns>
    public int ActionsPendingCount()
    {
        return actionBuffer.ActionsPendingCount();
    }

    #endregion

    #region Robot Program Methods
    /// <summary>
    /// Return a device-specific RobotProgram with all the Actions pending in the buffer.
    /// </summary>
    /// <param name="inlineTargets">Write inline targets on action statements, or declare them as independent variables?</param>
    /// <param name="humanComments">If true, a human-readable description will be added to each line of code</param>
    /// <returns></returns>
    public RobotProgram FullProgramFromBuffer(bool inlineTargets, bool humanComments)
    {
        return compiler.UNSAFEFullProgramFromBuffer(Utilities.Strings.SafeProgramName(parentControl.parentRobot.Name), this, false, inlineTargets, humanComments);
    }



    /// <summary>
    /// Return a device-specific program with the next block of Actions pending in the buffer.
    /// </summary>
    /// <param name="inlineTargets">Write inline targets on action statements, or declare them as independent variables?</param>
    /// <param name="humanComments">If true, a human-readable description will be added to each line of code</param>
    /// <returns></returns>
    public RobotProgram ProgramFromBlock(bool inlineTargets, bool humanComments)
    {
        return compiler.UNSAFEFullProgramFromBuffer(Utilities.Strings.SafeProgramName(parentControl.parentRobot.Name), this, true, inlineTargets, humanComments);
    }

    #endregion

    #region Setting and Debug Methods
    public void LogBufferedActions()
    {
        lock (actionBufferLock)
        {
            actionBuffer.DebugBufferedActions();
        }
    }

    /// <summary>
    /// Returns a Settings object representing the current state of this RobotCursor.
    /// </summary>
    /// <returns></returns>
    public Settings GetSettings()
    {
        return new Settings(this.speed, this.acceleration, this.precision, this.motionType, this.referenceCS, this.extrusionRate);
    }
    #endregion

    #region Forced Updates

    /// <summary>
    /// Force-update a full pose without going through Action application.
    /// Temporarily here for MotionUpdate cursors, until I figure out a better way of dealing with it... 
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="rot"></param>
    /// <param name="ax"></param>
    /// <param name="extax"></param>
    internal void UpdateFullPose(Vector pos, Rotation rot, Joints ax, ExternalAxes extax)
    {
        this.position = pos;
        this.rotation = rot;
        this.axes = ax;
        this.externalAxesCartesian = extax;
    }

    #endregion

    #region Apply Actions
    /// <summary>
    /// Applies an Action to this cursor. 
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool Apply(Action action)
    {
        if (action == null)
        {
            logger.Verbose("Action is null.");
            return false;
        }

        return action.Apply(this);
    }

    /// <summary>
    /// This action doesn't change the state of the cursor... 
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionCustomCode action) => true;

    #region Apply Settings Action

    /// <summary>
    /// Apply Acceleration Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionAcceleration action)
    {
        if (action.relative)
            this.acceleration += action.acceleration;
        else
            this.acceleration = action.acceleration;

        if (this.acceleration < 0) this.acceleration = 0;

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("Acceleration set to " + this.acceleration);
        }

        return true;
    }

    /// <summary>
    /// Apply Speed Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionSpeed action)
    {
        if (action.relative)
            this.speed += action.speed;
        else
            this.speed = action.speed;

        if (this.speed < 0) this.speed = 0;

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("Speed set to " + this.speed);
        }

        return true;
    }

    /// <summary>
    /// Apply Zone Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionPrecision action)
    {
        if (action.relative)
            this.precision += action.precision;
        else
            this.precision = action.precision;

        if (this.precision < 0) precision = 0;

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("Precision set to " + this.precision + " mm");
        }

        return true;
    }

    /// <summary>
    /// Apply Motion Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionMotionMode action)
    {
        this.motionType = action.motionType;

        return true;
    }

    /// <summary>
    /// Apply ReferenceCS Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionCoordinates action)
    {
        this.referenceCS = action.referenceCS;

        return true;
    }

    /// <summary>
    /// Apply a Push or Pop Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionPushPop action)
    {
        if (action.push)
        {
            return this.settingsBuffer.Push(this);
        }
        else
        {
            Settings s = settingsBuffer.Pop(this);
            if (s != null)
            {
                this.acceleration = s.Acceleration;
                this.speed = s.Speed;
                this.precision = s.Precision;
                this.motionType = s.MotionType;
                this.referenceCS = s.RefCS;
                this.extrusionRate = s.ExtrusionRate;
                return true;
            }
        }
        return false;
    }

    #endregion

    #region Apply Motion Action
    /// <summary>
    /// Apply Translation Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionTranslation action)
    {
        Vector newPosition = new Vector();

        if (action.relative)
        {
            // If user issued a relative action, make sure there are absolute values to work with. (This limitation is due to current lack of FK/IK solvers)
            if (position == null || rotation == null)
            {
                logger.Warning($"Cannot apply \"{action}\", must provide absolute position values first before applying relative ones... ");
                return false;
            }

            if (referenceCS == ReferenceCS.World)
            {
                newPosition = position + action.translation;
            }
            else
            {
                //Vector worldVector = Vector.Rotation(action.translation, Rotation.Conjugate(this.rotation));
                Vector worldVector = Vector.Rotation(action.translation, this.rotation);
                newPosition = position + worldVector;
            }
        }
        else
        {
            // Fail if issued abs movement without prior rotation info. (This limitation is due to current lack of FK/IK solvers)
            if (rotation == null)
            {
                logger.Warning($"Cannot apply \"{action}\", currently missing TCP orientation to work with... ");
                return false;
            }

            newPosition.Set(action.translation);
        }

        // @TODO: this must be more programmatically implemented 
        //if (Control.SAFETY_CHECK_TABLE_COLLISION)
        //{
        //    if (Control.IsBelowTable(newPosition.Z))
        //    {
        //        if (Control.SAFETY_STOP_ON_TABLE_COLLISION)
        //        {
        //            Console.WriteLine("Cannot perform action: too close to base XY plane --> TCP.z = {0}", newPosition.Z);
        //            return false;
        //        }
        //        else
        //        {
        //            Console.WriteLine("WARNING: too close to base XY plane, USE CAUTION! --> TCP.z = {0}", newPosition.Z);
        //        }
        //    }
        //}

        prevPosition = position;
        position = newPosition;

        prevRotation = rotation;  // to flag same-orientation change

        prevAxes = axes;
        axes = null;      // flag joints as null to avoid Joint instructions using obsolete data

        if (isExtruding) this.ComputeExtrudedLength();

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("TCP position at " + this.position);
        }

        return true;
    }


    /// <summary>
    /// Apply Rotation Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionRotation action)
    {
        Rotation newRot;

        // @TODO: implement some kind of security check here...
        if (action.relative)
        {
            // If user issued a relative action, make sure there are absolute values to work with (this limitation is due to current lack of FK/IK solvers).
            if (position == null || rotation == null)
            {
                logger.Warning($"Cannot apply \"{action}\", must provide absolute rotation values first before applying relative ones... ");
                return false;
            }

            prevRotation = rotation;
            if (referenceCS == ReferenceCS.World)
            {
                //rotation.RotateGlobal(action.rotation);
                newRot = Rotation.Global(rotation, action.rotation);  // @TODO: TEST THIS
            }
            else
            {
                //rotation.RotateLocal(action.rotation);
                newRot = Rotation.Local(rotation, action.rotation);  // @TODO: TEST THIS
            }
        }
        else
        {
            // Fail if issued abs rotation without prior position info (this limitation is due to current lack of FK/IK solvers).
            if (position == null)
            {
                logger.Warning($"Cannot apply \"{action}\", currently missing TCP position to work with... ");
                return false;
            }

            newRot = new Rotation(action.rotation);
        }

        prevRotation = rotation;
        rotation = newRot;

        prevPosition = position;  // to flag same-position change

        prevAxes = axes;
        axes = null;      // flag joints as null to avoid Joint instructions using obsolete data

        if (isExtruding) this.ComputeExtrudedLength();

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("TCP orientation at " + new Orientation(this.rotation));
        }

        return true;
    }


    /// <summary>
    /// Apply Transformation Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionTransformation action)
    {
        Vector newPos;
        Rotation newRot;

        // Relative transform
        if (action.relative)
        {
            // If user issued a relative action, make sure there are absolute values to work with. (This limitation is due to current lack of FK/IK solvers)
            if (position == null || rotation == null)
            {
                logger.Warning($"Cannot apply \"{action}\", must provide absolute transform values first before applying relative ones...");
                return false;
            }

            // This is Translate + Rotate
            if (action.translationFirst)
            {
                if (referenceCS == ReferenceCS.World)
                {
                    newPos = position + action.translation;
                    newRot = Rotation.Combine(action.rotation, rotation);  // premultiplication
                }
                else
                {
                    //Vector worldVector = Vector.Rotation(action.translation, Rotation.Conjugate(this.rotation));
                    Vector worldVector = Vector.Rotation(action.translation, this.rotation);
                    newPos = position + worldVector;
                    newRot = Rotation.Combine(rotation, action.rotation);  // postmultiplication
                }
            }

            // or Rotate + Translate
            else
            {
                if (referenceCS == ReferenceCS.World)
                {
                    newPos = position + action.translation;
                    newRot = Rotation.Combine(action.rotation, rotation);  // premultiplication
                }

                else
                {
                    // @TOCHECK: is this correct?
                    newRot = Rotation.Combine(rotation, action.rotation);  // postmultiplication
                    //Vector worldVector = Vector.Rotation(action.translation, Rotation.Conjugate(newRot));
                    Vector worldVector = Vector.Rotation(action.translation, newRot);
                    newPos = position + worldVector;
                }

            }

        }

        // Absolute transform
        else
        {
            newPos = new Vector(action.translation);
            newRot = new Rotation(action.rotation);
        }

        //// @TODO: this must be more programmatically implemented 
        //if (Control.SAFETY_CHECK_TABLE_COLLISION)
        //{
        //    if (Control.IsBelowTable(newPos.Z))
        //    {
        //        if (Control.SAFETY_STOP_ON_TABLE_COLLISION)
        //        {
        //            Console.WriteLine("Cannot perform action: too close to base XY plane --> TCP.z = {0}", newPos.Z);
        //            return false;
        //        }
        //        else
        //        {
        //            Console.WriteLine("WARNING: too close to base XY plane, USE CAUTION! --> TCP.z = {0}", newPos.Z);
        //        }
        //    }
        //}

        prevPosition = position;
        position = newPos;
        prevRotation = rotation;
        rotation = newRot;

        prevAxes = axes;
        axes = null;  // flag joints as null to avoid Joint instructions using obsolete data

        if (isExtruding) this.ComputeExtrudedLength();

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("TCP transform at " + this.position + " " + new Orientation(this.rotation));
        }

        return true;
    }

    /// <summary>
    /// Apply ArcMotion Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionArcMotion action)
    {
        Vector newPos;
        Rotation newRot;

        // Relative transform
        if (action.relative)
        {
            // If user issued a relative action, make sure there are absolute values to work with. (This limitation is due to current lack of FK/IK solvers)
            if (position == null || rotation == null)
            {
                logger.Warning($"Cannot apply \"{action}\", must provide absolute transform values first before applying relative ones...");
                return false;
            }

            // As of right now, we are only supporting relative arc motions that are position-only.
            // Sanity check just in case.
            if (!action.positionOnly)
            {
                logger.Error("Unsupported relative plane-plane ArcMotion");
                return false;
            }


            if (referenceCS == ReferenceCS.World)
            {
                // Translate to endpoint, keep orientation as-is
                newPos = position + action.end.Origin;
                newRot = rotation;
            }
            else
            {
                // Translate relative to tool, keep orientation as-is
                Vector worldVector = Vector.Rotation(action.end.Origin, this.rotation);
                newPos = position + worldVector;
                newRot = rotation;
            }

        }

        // Absolute transform
        else
        {
            newPos = action.end.Origin;

            if (action.positionOnly)
            {
                newRot = rotation;
            }
            else
            {
                newRot = action.end.Orientation;
            }
        }


        prevPosition = position;
        position = newPos;
        prevRotation = rotation;
        rotation = newRot;

        prevAxes = axes;
        axes = null;  // flag joints as null to avoid Joint instructions using obsolete data

        // @TODO: Implement this at some point
        //if (isExtruding) this.ComputeExtrudedLength();

        if (isExtruding)
        {
            logger.Error("Extrusion calculations not implemented for ArcMotion, unexpected extrusions may follow");
        }

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("TCP transform at " + this.position + " " + new Orientation(this.rotation));
        }

        return true;
    }

    /// <summary>
    /// Apply Joints Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionAxes action)
    {
        Joints newJnt;

        // @TODO: implement joint limits checks and general safety...

        // Modify current Joints
        if (action.relative)
        {
            // If user issued a relative action, make sure there are absolute values to work with. 
            // (This limitation is due to current lack of FK/IK solvers)
            if (axes == null)  // could also check for motionType == MotionType.Joints ?
            {
                logger.Warning($"Cannot apply \"{action}\", must provide absolute Joints values first before applying relative ones...");
                return false;
            }

            newJnt = Joints.Add(axes, action.joints);
        }
        else
        {
            newJnt = new Joints(action.joints);
        }

        // Update prev and current axes
        prevAxes = axes;
        axes = newJnt;

        // Flag the lack of other geometric data
        prevPosition = position;
        prevRotation = rotation;
        position = null;
        rotation = null;

        // THIS IS STILL PSEUDO-CODE
        //// Store previous positions
        //prevPosition = position;
        //prevRotation = rotation;

        //// If possible, update pos/rot or flag them as unavailable
        //if (FKavailable)
        //{
        //    position = computeFK(axes);
        //    rotation = computaFK(axes);
        //}
        //else
        //{
        //    position = null;
        //    rotation = null;
        //}

        if (isExtruding) this.ComputeExtrudedLength();

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("Axes at " + this.axes);
        }

        return true;
    }


    #endregion

    #region Apply Msc Action
    // <summary>
    /// Apply Message Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionMessage action)
    {
        // There is basically nothing to do here! Leave the state of the robot as-is.
        // Maybe do some Console output?
        return true;
    }

    /// <summary>
    /// Apply Wait Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionWait action)
    {
        // There is basically nothing to do here! Leave the state of the robot as-is.
        return true;
    }

    /// <summary>
    /// Apply Comment Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionComment action)
    {
        // There is basically nothing to do here! Leave the state of the robot as-is.
        return true;
    }

    /// <summary>
    /// Adds the defined Tool to this cursor's Tool dict, becoming avaliable for Attach/Detach Actions.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionDefineTool action)
    {
        // Sanity
        if (availableTools.ContainsKey(action.tool.name))
        {
            logger.Info($"Robot already had a tool defined as \"{action.tool.name}\"; this will be overwritten.");
            availableTools.Remove(action.tool.name);
        }

        availableTools.Add(action.tool.name, action.tool);

        return true;
    }

    /// <summary>
    /// Apply Attach Tool Action.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionAttachTool action)
    {
        // Sanity: this is a fix for pre-0.8.x compatibility where Attach came with the Tool object, not the name. 
        // Older versions of Machina would yield error searching for `null` key on `availableTools`
        if (action.toolName == null)
        {
            logger.Error($"Obsolete version of AttachTool; please update Machina to latest update.");
            return false;
        }

        // Sanity
        if (!availableTools.ContainsKey(action.toolName))
        {
            logger.Warning($"No tool named \"{action.toolName}\" defined in this robot; please use \"DefineTool\" first.");
            return false;
        }
        // This would not work in case the user had defined a new tool with different values but same name (not great practice, but technically possible anyway...)
        //if (action.toolName == this.tool.name)
        //{
        //    logger.Verbose($"Attaching the same tool? No changes...");
        //    return true;
        //}


        // The cursor has now a tool attached to it 
        Tool prevTool = this.tool;
        this.tool = availableTools[action.toolName];

        // Shim for lack of IK 
        // If coming from axes motion, no need to transform the TCP
        if (this.position == null || this.rotation == null)
        {
            logger.Warning($"Attaching tool without TCP values, inconsistent results may follow...?");
        }
        // Otherwise transform the TCP
        else
        {
            if (prevTool != null)
            {
                logger.Debug($"Detaching tool {prevTool.name} before attaching {this.tool.name}.");
                UndoToolTransformOnCursor(this, prevTool, logger, _logRelativeActions);
            }

            ApplyToolTransformToCursor(this, this.tool, logger, _logRelativeActions);
        }

        return true;
    }

    /// <summary>
    /// Apply Detach Tool action
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionDetachTool action)
    {
        if (this.tool == null)
        {
            logger.Verbose("Robot had no tool attached");
            return false;
        }

        // Shim for lack of IK 
        // If coming from axes motion, no need to undo the tool's transform on the TCP
        if (this.position == null || this.rotation == null)
        {
            // Really nothing to do here right?
        }
        // Otherwise undo the tool's transforms
        else
        {
            UndoToolTransformOnCursor(this, this.tool, logger, _logRelativeActions);
        }

        // "Detach" the tool
        this.tool = null;

        return true;
    }

    /// <summary>
    /// Apply ActionIODigital write action to this cursor.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionIODigital action)
    {


        if (digitalOutputs.ContainsKey(action.pinName))
        {
            digitalOutputs[action.pinName] = action.on;
        }
        else
        {
            digitalOutputs.Add(action.pinName, action.on);
        }

        return true;
    }

    /// <summary>
    /// Apply ActionIOAnalog write action to this cursor. 
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionIOAnalog action)
    {

        if (analogOutputs.ContainsKey(action.pinName))
        {
            analogOutputs[action.pinName] = action.value;
        }
        else
        {
            analogOutputs.Add(action.pinName, action.value);
        }

        return true;
    }

    #endregion

    #region Apply Temp-Extrude Actions

    /// <summary>
    /// Apply ActionTemperature write action to this cursor.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionTemperature action)
    {
        if (action.relative)
            this.partTemperature[action.robotPart] += action.temperature;
        else
            this.partTemperature[action.robotPart] = action.temperature;

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("Temperature set to " + this.partTemperature[action.robotPart]);
        }

        return true;
    }

    /// <summary>
    /// Apply ActionExtrusion write action to this cursor.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionExtrusion action)
    {
        this.isExtruding = action.extrude;

        return true;
    }

    /// <summary>
    /// Apply ActionExtrusionRate write action to this cursor. 
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionExtrusionRate action)
    {
        if (action.relative)
            this.extrusionRate += action.rate;
        else
            this.extrusionRate = action.rate;

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("Extrusion rate set to " + this.extrusionRate);
        }

        return true;
    }

    /// <summary>
    /// This is just to write start/end boilerplates for 3D printers. 
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool ApplyAction(ActionInitialization action)
    {
        // nothing to do here really... 
        return true;
    }

    #endregion

    #region Apply External Axes Action
    public bool ApplyAction(ActionExternalAxis action)
    {
        // Cartesian targets
        if (action.target == ExternalAxesTarget.All || action.target == ExternalAxesTarget.Cartesian)
        {
            if (this.externalAxesCartesian == null)
            {
                this.externalAxesCartesian = new ExternalAxes();
            }


            if (action.relative)
            {
                if (this.externalAxesCartesian[action.axisNumber - 1] == null)
                {
                    logger.Error("Cannot increase cartesian external axis, value has not been initialized. Try `ExternalAxisTo()` instead.");
                    return false;
                }

                this.externalAxesCartesian[action.axisNumber - 1] += action.value;
            }
            else
            {
                this.externalAxesCartesian[action.axisNumber - 1] = action.value;
            }
        }

        // Joint targets 
        if (action.target == ExternalAxesTarget.All || action.target == ExternalAxesTarget.Joint)
        {
            if (this.externalAxesJoints == null)
            {
                this.externalAxesJoints = new ExternalAxes();
            }

            if (action.relative)
            {
                if (this.externalAxesJoints[action.axisNumber - 1] == null)
                {
                    logger.Error("Cannot increase joint external axis, value has not been initialized. Try `ExternalAxisTo()` instead.");
                    return false;
                }

                this.externalAxesJoints[action.axisNumber - 1] += action.value;
            }
            else
            {
                this.externalAxesJoints[action.axisNumber - 1] = action.value;
            }
        }

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("External Axis " + action.axisNumber + " set to " + this.externalAxesJoints[action.axisNumber - 1]);
        }

        return true;
    }
    #endregion

    #region Apply Arm Angel Action
    public bool ApplyAction(ActionArmAngle action)
    {
        if (action.relative)
        {
            if (this.armAngle == null)
            {
                logger.Error("Cannot increase arm-angle, value has not been initialized. Try `ArmAngleTo()` instead.");
                return false;
            }
            else
            {
                this.armAngle += action.angle;
            }
        }
        else
        {
            this.armAngle = action.angle;
        }

        if (_logRelativeActions && action.relative)
        {
            logger.Verbose("Arm-Angle set to " + this.armAngle);
        }

        return true;
    }
    #endregion

    #endregion
                                    
    #region Utilities
    /// <summary>
    /// Update the current extrudedLength.
    /// </summary>
    public void ComputeExtrudedLength()
    {
        if (this.prevPosition == null || this.position == null)
        {
            logger.Info($"Cannot compute new extrusion length: cursor position missing");
            return;
        }

        this.prevExtrudedLength = this.extrudedLength;
        this.extrudedLength += this.extrusionRate * this.prevPosition.DistanceTo(this.position);
    }

    /// <summary>
    /// Modify a cursor's TCP transform according to a tool. Useful for Attach operations.
    /// </summary>
    /// <param name="tool"></param>
    internal void ApplyToolTransformToCursor(RobotCursor cursor, Tool tool, RobotLogger logger, bool log)
    {
        // Now transform the cursor position to the tool's transformation params:
        Vector worldVector = Vector.Rotation(tool.TCPPosition, cursor.rotation);
        Vector newPos = cursor.position + worldVector;
        Rotation newRot = Rotation.Combine(cursor.rotation, tool.TCPOrientation);  // postmultiplication

        cursor.prevPosition = cursor.position;
        cursor.position = newPos;
        cursor.prevRotation = cursor.rotation;
        cursor.rotation = newRot;
        //cursor.prevAxes = cursor.axes;  // why was this here? joints don't change on tool attachment...

        if (log)
        {
            logger.Verbose("Cursor TCP changed to " + cursor.position + " " + new Orientation(cursor.rotation) + " due to tool attachment");
        }

    }

    /// <summary>
    /// Undo tool-based TCP transformations on a cursor. Useful for Detach operations.
    /// </summary>
    /// <param name="tool"></param>
    internal void UndoToolTransformOnCursor(RobotCursor cursor, Tool tool, RobotLogger logger, bool log)
    {
        // TODO: should this method be static??
        // TODO: at some point in the future, check for translationFirst here
        Rotation newRot = Rotation.Combine(cursor.rotation, Rotation.Inverse(tool.TCPOrientation));  // postmultiplication by the inverse rotation
        Vector worldVector = Vector.Rotation(tool.TCPPosition, cursor.rotation);
        Vector newPos = cursor.position - worldVector;

        cursor.prevPosition = cursor.position;
        cursor.position = newPos;
        cursor.prevRotation = cursor.rotation;
        cursor.rotation = newRot;
        //this.prevAxes = this.axes;
        //this.axes = null;  // axes were null anyway...? 

        if (log)
        {
            logger.Verbose("Cursor TCP changed to " + cursor.position + " " + new Orientation(cursor.rotation) + " due to tool removal");
        }
    }

    /// <summary>
    /// For the current state of the cursor, out the pos/rot of the through point of an arc action.
    /// </summary>
    /// <param name="action"></param>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    internal bool ComputeThroughPlane(ActionArcMotion action, out Vector throughPos, out Rotation throughRot)
    {
        if (action.relative)
        {
            throughPos = this.position - action.end.Origin + action.through.Origin;

            if (action.positionOnly)
            {
                throughRot = this.rotation;
            }
            else
            {
                logger.Error("Unsupported relative frame-frame actions!");
                throughRot = null;
                return false;
            }
        }
        else
        {
            throughPos = action.through.Origin;
            if (action.positionOnly)
            {
                throughRot = this.rotation;
            }
            else
            {
                throughRot = action.through.Orientation;
            }
        }

        return true;
    }


    public override string ToString() => $"{name}: {motionType} p{position} r{rotation} j{axes} a{acceleration} v{speed} p{precision} {(this.tool == null ? "" : "t" + this.tool)}";


    #endregion
}
