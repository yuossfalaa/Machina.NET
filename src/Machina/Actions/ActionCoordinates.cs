namespace Machina;
/// <summary>
/// An Action to change current Reference Coordinate System.
/// </summary>
public class ActionCoordinates : Action
{
    public ReferenceCS referenceCS;
    public override ActionType Type => ActionType.Coordinates;

    public ActionCoordinates(ReferenceCS referenceCS) : base()
    {
        this.referenceCS = referenceCS;
    }

    public override string ToString()
    {
        return string.Format("Set reference coordinate system to '{0}'", referenceCS);
    }

    public override string ToInstruction() => null;

    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
