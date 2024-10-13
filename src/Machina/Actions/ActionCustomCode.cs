namespace Machina;
                           
/// <summary>
/// Insert a line of custom code in the program. 
/// </summary>                                                    
public class ActionCustomCode : Action
{
    public string statement;
    public bool isDeclaration;

    public override ActionType Type => ActionType.CustomCode;

    public ActionCustomCode(string statement, bool isDeclaration) : base()
    {
        this.statement = statement;
        this.isDeclaration = isDeclaration;
    }

    public override string ToString()
    {
        return this.isDeclaration ?
            $"Add custom declaration: \"{this.statement}\"" :
            $"Add custom instruction: \"{this.statement}\"";
    }

    public override string ToInstruction() =>
            $"CustomCode(\"{this.statement}\",{this.isDeclaration});";

    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
