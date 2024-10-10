using Machina.Attributes;

namespace Machina;

public partial class Robot
{
    #region Custom Messages Method
    /// <summary>
    /// Send a string message to the device, to be displayed based on device's capacities.
    /// </summary>
    /// <param name="message"></param>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool Message(string message)
    {
        return _control.IssueActionManager.IssueMessageRequest(message);
    }

    /// <summary>
    /// Display an internal comment in the compilation code. 
    /// Useful for internal annotations, reminders, etc. 
    /// </summary>
    /// <param name="comment"></param>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool Comment(string comment)
    {
        return _control.IssueActionManager.IssueCommentRequest(comment);
    }

    /// <summary>
    /// Insert a line of custom code directly into a compiled program. 
    /// This is useful for obscure instructions that are not covered by Machina's API. 
    /// Note that this Action cannot be checked for validity by Machina, and you are responsible for correct syntax.
    /// This Action is non-streamable. 
    /// </summary>
    /// <param name="statement">Code in the machine's native language.</param>
    /// <param name="isDeclaration">Is this a declaration, like a variable or a workobject? If so, this statement will be placed at the beginning of the program.</param>
    /// <returns></returns>
    public bool CustomCode(string statement, bool isDeclaration = false) =>
            _control.IssueActionManager.IssueCustomCodeRequest(statement, isDeclaration);
    #endregion
}
