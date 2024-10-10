namespace Machina;
                                                      
/// <summary>
/// An Action representing a string message sent to the device to be displayed.
/// </summary>
public class ActionMessage : Action
{
    public string message;
    public override ActionType Type => ActionType.Message;

    public ActionMessage(string message) : base()
    {
        this.message = message;
    }

    public override string ToString()
    {
        return string.Format("Display message \"{0}\"", message);
    }

    public override string ToInstruction()
    {
        return $"Message(\"{this.message}\");";
    }
}
