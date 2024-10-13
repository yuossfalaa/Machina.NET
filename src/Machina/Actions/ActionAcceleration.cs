﻿namespace Machina;
                                                                                               
public class ActionAcceleration : Action
{
    public double acceleration;
    public bool relative;

    public override ActionType Type => ActionType.Acceleration;

    public ActionAcceleration(double acc, bool relative) : base()
    {
        this.acceleration = acc;
        this.relative = relative;
    }

    public override string ToString()
    {
        return relative ?
            string.Format("{0} motion acceleration by {1} mm/s^2 or deg/s^2", this.acceleration < 0 ? "Decrease" : "Increase", this.acceleration) :
            string.Format("Set motion acceleration to {0} mm/s^2 or deg/^s", this.acceleration);
    }

    public override string ToInstruction()
    {
        return relative ?
            $"Acceleration({this.acceleration});" :
            $"AccelerationTo({this.acceleration});";
    }

    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
