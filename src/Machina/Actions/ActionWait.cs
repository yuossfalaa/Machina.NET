namespace Machina;


/// <summary>
/// An Action represening the device staying idle for a period of time.
/// </summary>
public class ActionWait : Action
{
    public long millis;

    public override ActionType Type => ActionType.Wait;

    public ActionWait(long millis) : base()
    {
        this.millis = millis;
    }

    public override string ToString()
    {
        return string.Format("Wait {0} ms", millis);
    }

    public override string ToInstruction()
    {
        return $"Wait({this.millis});";
    }
    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
