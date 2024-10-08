using System;

namespace Machina;

public partial class Robot
{
    #region Debug Methods                        
    /// <summary>
    /// Dumps a bunch of information to the console about the controller, the main task, etc.
    /// </summary>
    public void DebugDump()
    {
        _control.DebugDump();
    }

    /// <summary>
    /// Dumps a list of the remaining buffered Actions.
    /// </summary>
    public void DebugBuffers()
    {
        _control.DebugBuffers();
    }

    /// <summary>
    /// Dumps the state of the internal RobotPointers
    /// </summary>
    public void DebugRobotCursors()
    {
        _control.DebugRobotCursors();
    }

    /// <summary>
    /// Dumps current Settings values
    /// </summary>
    public void DebugSettingsBuffer()
    {
        //_control.DebugSettingsBuffer();
    }

    /// <summary>
    /// Sets Machina to dump all log messages to the Console.
    /// </summary>
    public void DebugMode(bool on)
    {
        if (on)
        {
            Machina.Logger.WriteLine += Console.WriteLine;
            Machina.Logger.SetLogLevel(LogLevel.DEBUG);
        }
        else
        {
            Machina.Logger.WriteLine -= Console.WriteLine;
            Machina.Logger.SetLogLevel(LogLevel.INFO);
        }
    }
    #endregion

}
