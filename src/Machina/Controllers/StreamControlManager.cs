using System;
using Machina.Drivers;

namespace Machina.Controllers;

/// <summary>
/// A manager for Control objects running ControlType.Stream
/// </summary>
internal class StreamControlManager(Control parent) : ControlManager(parent)
{
    public override bool Terminate()
    {
        throw new NotImplementedException();
    }

    internal override void SetCommunicationObject()
    {
        // @TODO: shim assignment of correct robot model/brand
        _control.Driver = _control.parentRobot.Brand switch
        {
            RobotType.ABB => new DriverABB(_control),
            RobotType.UR => new DriverUR(_control),
            RobotType.KUKA => new DriverKUKA(_control),
            _ => throw new NotImplementedException(),
        };
    }

    internal override void LinkWriteCursor()
    {
        // Pass the streamQueue object as a shared reference
        _control.Driver.LinkWriteCursor(_control.ReleaseCursor);
    }

    internal override void SetStateCursor()
    {
        _control.SetStateCursor(_control.ExecutionCursor);
    }
}
