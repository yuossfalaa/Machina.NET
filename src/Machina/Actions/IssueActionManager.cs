using System;
using Machina.Types.Geometry;

namespace Machina;

/// <summary>
/// This class holds Methods to issue Actions and then use a control class to apply the action request
/// </summary>
internal class IssueActionManager
{
    private readonly Control _control;

    public IssueActionManager(Control control)
    {
        _control = control;
    }

    // Generic factory method to create and issue the action
    private bool IssueAction<TAction>(params object[] args) where TAction : Action
    {
        var action = (TAction)Activator.CreateInstance(typeof(TAction), args);
        return _control.IssueApplyActionRequest(action);
    }

    public bool IssueSpeedRequest(double speed, bool relative) =>
            IssueAction<ActionSpeed>(speed, relative);

    public bool IssueAccelerationRequest(double acc, bool relative) =>
            IssueAction<ActionAcceleration>(acc, relative);


    public bool IssuePrecisionRequest(double precision, bool relative) =>
            IssueAction<ActionPrecision>(precision, relative);


    public bool IssueMotionRequest(MotionType motionType) =>
            IssueAction<ActionMotionMode>(motionType);


    public bool IssueCoordinatesRequest(ReferenceCS referenceCS) =>
            IssueAction<ActionCoordinates>(referenceCS);


    public bool IssuePushPopRequest(bool push) =>
            IssueAction<ActionPushPop>(push);


    public bool IssueTemperatureRequest(double temp, RobotPartType robotPart, bool waitToReachTemp, bool relative) =>
            IssueAction<ActionTemperature>(temp, robotPart, waitToReachTemp, relative);


    public bool IssueExtrudeRequest(bool extrude) =>
            IssueAction<ActionExtrusion>(extrude);


    public bool IssueExtrusionRateRequest(double rate, bool relative) =>
            IssueAction<ActionExtrusionRate>(rate, relative);

    /// <summary>
    /// Issue a Translation action request that falls back on the state of current settings.
    /// </summary>
    /// <param name="trans"></param>
    /// <param name="relative"></param>
    /// <returns></returns>
    public bool IssueTranslationRequest(Vector trans, bool relative) =>
            IssueAction<ActionTranslation>(trans, relative);


    /// <summary>
    /// Issue a Rotation action request with fully customized parameters.
    /// </summary>
    /// <param name="rot"></param>
    /// <param name="relative"></param>
    /// <returns></returns>
    public bool IssueRotationRequest(Rotation rot, bool relative) =>
            IssueAction<ActionRotation>(rot, relative);


    /// <summary>
    /// Issue a Translation + Rotation action request with fully customized parameters.
    /// </summary>
    /// <param name="trans"></param>
    /// <param name="rot"></param>
    /// <param name="rel"></param>
    /// <param name="translationFirst"></param>
    /// <returns></returns>
    /// <returns></returns>
    public bool IssueTransformationRequest(Vector trans, Rotation rot, bool rel, bool translationFirst) =>
            IssueAction<ActionTransformation>(trans, rot, rel, translationFirst);

    /// <summary>
    /// Issue an arc motion requesti with fully customized parameters.
    /// </summary>
    /// <param name="through"></param>
    /// <param name="end"></param>
    /// <param name="relative"></param>
    /// <param name="positionOnly"></param>
    /// <returns></returns>
    public bool IssueArcMotionRequest(Plane through, Plane end, bool relative, bool positionOnly) =>
        IssueAction<ActionArcMotion>(through, end, relative, positionOnly);

    /// <summary>
    /// Issue a request to set the values of joint angles in configuration space. 
    /// </summary>
    /// <param name="joints"></param>
    /// <param name="relJnts"></param>
    /// <param name="speed"></param>
    /// <param name="zone"></param>
    /// <returns></returns>
    public bool IssueJointsRequest(Joints joints, bool relJnts) =>
            IssueAction<ActionAxes>(joints, relJnts);


    /// <summary>
    /// Issue a request to display a string message on the device.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    public bool IssueMessageRequest(string message) =>
            IssueAction<ActionMessage>(message);


    /// <summary>
    /// Issue a request for the device to stay idle for a certain amount of time.
    /// </summary>
    /// <param name="millis"></param>
    /// <returns></returns>
    public bool IssueWaitRequest(long millis) =>
            IssueAction<ActionWait>(millis);


    /// <summary>
    /// Issue a request to add an internal comment in the compiled code. 
    /// </summary>
    /// <param name="comment"></param>
    /// <returns></returns>
    public bool IssueCommentRequest(string comment) =>
            IssueAction<ActionComment>(comment);


    /// <summary>
    /// Issue a reques to defin a Tool in the robot's internal library, avaliable for Attach/Detach requests.
    /// </summary>
    /// <param name="tool"></param>
    /// <returns></returns>
    public bool IssueDefineToolRequest(Tool tool) =>
            IssueAction<ActionDefineTool>(tool);


    /// <summary>
    /// Issue a request to attach a Tool to the flange of the robot
    /// </summary>
    /// <param name="tool"></param>
    /// <returns></returns>
    public bool IssueAttachRequest(string toolName) =>
            IssueAction<ActionAttachTool>(toolName);


    /// <summary>
    /// Issue a request to return the robot to no tools attached. 
    /// </summary>
    /// <returns></returns>
    public bool IssueDetachRequest() =>
            IssueAction<ActionDetachTool>();


    /// <summary>
    /// Issue a request to turn digital IO on/off.
    /// </summary>
    /// <param name="pinId"></param>
    /// <param name="isOn"></param>
    /// <returns></returns>
    public bool IssueWriteToDigitalIORequest(string pinId, bool isOn, bool toolPin) =>
            IssueAction<ActionIODigital>(pinId, isOn, toolPin);


    /// <summary>
    /// Issue a request to write to analog pin.
    /// </summary>
    /// <param name="pinId"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool IssueWriteToAnalogIORequest(string pinId, double value, bool toolPin) =>
            IssueAction<ActionIOAnalog>(pinId, value, toolPin);


    /// <summary>
    /// Issue a request to add common initialization/termination procedures on the device, 
    /// like homing, calibration, fans, etc.
    /// </summary>
    /// <param name="initiate"></param>
    /// <returns></returns>
    public bool IssueInitializationRequest(bool initiate) =>
            IssueAction<ActionInitialization>(initiate);


    /// <summary>
    /// Issue a request to modify a external axis in this robot.
    /// Note axisNumber is one-based, i.e. axisNumber 1 is _externalAxes[0]
    /// </summary>
    /// <param name="axisNumber"></param>
    /// <param name="value"></param>
    /// <param name="target"></param>
    /// <param name="relative"></param>
    /// <returns></returns>
    public bool IssueExternalAxisRequest(int axisNumber, double value, ExternalAxesTarget target, bool relative) =>
            IssueAction<ActionExternalAxis>(axisNumber, value, target, relative);

    /// <summary>
    /// Issue a request to modify the arm-angle value for 7-dof robotic arms. 
    /// </summary>
    /// <param name="value"></param>
    /// <param name="relative"></param>
    /// <returns></returns>
    public bool IssueArmAngleRequest(double value, bool relative) =>
        IssueAction<ActionArmAngle>(value, relative);


    /// <summary>
    /// Issue a request to add custom code to a compiled program.
    /// </summary>
    /// <param name="statement"></param>
    /// <param name="isDeclaration"></param>
    /// <returns></returns>
    public bool IssueCustomCodeRequest(string statement, bool isDeclaration) =>
            IssueAction<ActionCustomCode>(statement, isDeclaration);


}
