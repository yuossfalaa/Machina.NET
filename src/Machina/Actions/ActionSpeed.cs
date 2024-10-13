﻿namespace Machina;
                                
/// <summary>
/// An Action to change the current speed setting.
/// </summary>
public class ActionSpeed : Action
{
    public double speed;
    public bool relative;

    public override ActionType Type => ActionType.Speed;

    public ActionSpeed(double speed, bool relative) : base()
    {
        this.speed = speed;
        this.relative = relative;
    }

    public override string ToString()
    {
        return relative ?
            string.Format("{0} motion speed by {1} mm/s or deg/s", this.speed < 0 ? "Decrease" : "Increase", speed) :
            string.Format("Set motion speed to {0} mm/s or deg/s", speed);
    }

    public override string ToInstruction()
    {
        return relative ?
            $"Speed({this.speed});" :
            $"SpeedTo({this.speed});";
    }
    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
