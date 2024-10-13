namespace Machina;
                                                                                            
/// <summary>
/// Apply general initialization parameters to the device.
/// </summary>
public class ActionInitialization : Action
{
    public bool initialize;
    public override ActionType Type => ActionType.Initialization;

    public ActionInitialization(bool initialize) : base()
    {
        this.initialize = initialize;
    }

    public override string ToString()
    {
        return $"{(this.initialize ? "Initialize" : "Terminate")} this device.";
    }

    public override string ToInstruction() => null;
    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
