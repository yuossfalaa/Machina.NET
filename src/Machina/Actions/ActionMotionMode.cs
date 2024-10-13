using System;

namespace Machina;

/// <summary>
/// An Action to change current MotionType.
/// </summary>
public class ActionMotionMode : Action
{
    public MotionType motionType;
    public override ActionType Type => ActionType.MotionMode;

    public ActionMotionMode(MotionType motionType) : base()
    {
        this.motionType = motionType;
    }

    public override string ToString()
    {
        return string.Format("Set motion type to '{0}'", motionType);
    }

    public override string ToInstruction()
    {
        return $"MotionMode(\"{(Enum.GetName(typeof(MotionType), this.motionType))}\");";
    }
    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
