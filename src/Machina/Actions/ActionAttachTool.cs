namespace Machina;
                                                                             
/// <summary>
/// Attaches a Tool to the robot flange. Must have beeb previously defined on the Robot.
/// If the robot already had a tool, it will be replaced by this one.
/// </summary>
public class ActionAttachTool : Action
{
    public string toolName;

    public override ActionType Type => ActionType.AttachTool;

    public ActionAttachTool(string name) : base()
    {
        this.toolName = name;
    }

    public override string ToString()
    {
        return string.Format("Attach tool \"{0}\" to robot flange.", this.toolName);
    }

    public override string ToInstruction()
    {
        return $"AttachTool(\"{this.toolName}\");";
    }

    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
