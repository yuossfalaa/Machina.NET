using Machina.Attributes;

namespace Machina;

public partial class Robot
{
    #region Control Methods
    /// <summary>
    /// Turns extrusion in 3D printers on/off.
    /// </summary>
    /// <param name="extrude">True/false for on/off.</param>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool Extrude(bool extrude = true)
    {
        return _control.IssueActionManager.IssueExtrudeRequest(extrude);
    }

    /// <summary>
    /// Initialize this device for action. Initialization uses device-specific
    /// common initialization routines, like homing and calibration, to set the 
    /// device ready for typical procedures like 3D printing. 
    /// </summary>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool Initialize()
    {
        return _control.IssueActionManager.IssueInitializationRequest(true);
    }

    /// <summary>
    /// Terminate this device. Termination uses device-specific
    /// common termination routines, like cooling or turning fans off, to prepare
    /// the device for idleness.
    /// </summary>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool Terminate()
    {
        return _control.IssueActionManager.IssueInitializationRequest(false);
    }

    /// <summary>
    /// Issue an Action represented by a string in the Machina Common Language, such as `Do("Move(100,0,0);")`.
    /// The Action will parse the string and use reflection to figure out the most suitable Action
    /// to associate to this. 
    /// </summary>
    /// <param name="actionStatement"></param>
    /// <returns></returns>
    public bool Do(string actionStatement)
    {
        // Should `Do` just generate the corresponding Action, or should it be an Action in itself...?
        // If it was, it would be hard to by-pass creating another Action when the reflected method is called...
        // So keep it as a "Setting" method for the time being...? Although it should prob be it's own Action,
        // just by design...

        // Also, should this method also be `[ParseableFromString]`?? So meta...! lol

        return _control.IssueApplyActionRequestFromStringStatement(actionStatement);
    }


    /// <summary>
    /// Issues an Action object to this robot. This is useful when a list of Actions
    /// is already available, and needs to be applied to this Robot.
    /// </summary>
    /// <param name="action"></param>
    /// <returns></returns>
    public bool Issue(Action action)
    {
        return _control.IssueApplyActionRequest(action);
    }



    #endregion

}
