namespace Machina;

/// <summary>
/// Detaches any tool currently attached to the robot.
/// </summary>
public class ActionDetachTool : Action
{
    public override ActionType Type => ActionType.DetachTool;

    public ActionDetachTool() : base() { }

    public override string ToString()
    {
        return "Detach all tools from robot.";
    }

    public override string ToInstruction()
    {
        return $"DetachTool();";
    }
}
