using Machina.Attributes;
using Machina.Types.Geometry;

namespace Machina;

public partial class Robot
{
    #region Tools Control Methods
    /// <summary>
    /// Define a Tool object on the Robot's internal library to make it avaliable for future Attach/Detach actions.
    /// </summary>
    /// <param name="tool"></param>
    /// <returns></returns>
    public bool DefineTool(Tool tool)
    {
        if (!Utilities.Strings.IsValidVariableName(tool.name))
        {
            logger.Error($"\"{tool.name}\" is not a valid tool name, please start with a letter.");
            return false;
        }

        Tool copy = Tool.Create(tool);
        return _control.IssueActionManager.IssueDefineToolRequest(copy);
    }

    /// <summary>
    /// Define a Tool object on the Robot's internal library to make it avaliable for future Attach/Detach actions.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="TCPPosition"></param>
    /// <param name="TCPOrientation"></param>
    /// <returns></returns>
    public bool DefineTool(string name, Point TCPPosition, Orientation TCPOrientation)
    {
        if (!Utilities.Strings.IsValidVariableName(name))
        {
            logger.Error($"\"{name}\" is not a valid tool name, please start with a letter.");
            return false;
        }

        Tool tool = Tool.Create(name, TCPPosition, TCPOrientation);
        return _control.IssueActionManager.IssueDefineToolRequest(tool);
    }

    /// <summary>
    /// Define a Tool object on the Robot's internal library to make it avaliable for future Attach/Detach actions.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="TCPPosition"></param>
    /// <param name="TCPOrientation"></param>
    /// <param name="weightKg"></param>
    /// <param name="centerOfGravity"></param>
    /// <returns></returns>
    public bool DefineTool(string name, Point TCPPosition, Orientation TCPOrientation, double weightKg, Point centerOfGravity)
    {
        if (!Utilities.Strings.IsValidVariableName(name))
        {
            logger.Error($"\"{name}\" is not a valid tool name, please start with a letter.");
            return false;
        }

        Tool tool = Tool.Create(name, TCPPosition, TCPOrientation, weightKg, centerOfGravity);
        return _control.IssueActionManager.IssueDefineToolRequest(tool);
    }

    /// <summary>
    /// Define a Tool object on the Robot's internal library to make it avaliable for future Attach/Detach actions.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="tcpX"></param>
    /// <param name="tcpY"></param>
    /// <param name="tcpZ"></param>
    /// <param name="tcp_vX0"></param>
    /// <param name="tcp_vX1"></param>
    /// <param name="tcp_vX2"></param>
    /// <param name="tcp_vY0"></param>
    /// <param name="tcp_vY1"></param>
    /// <param name="tcp_vY2"></param>
    /// <param name="weight"></param>
    /// <param name="cogX"></param>
    /// <param name="cogY"></param>
    /// <param name="cogZ"></param>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool DefineTool(string name, double tcpX, double tcpY, double tcpZ,
        double tcp_vX0, double tcp_vX1, double tcp_vX2, double tcp_vY0, double tcp_vY1, double tcp_vY2,
        double weight, double cogX, double cogY, double cogZ)
    {
        if (!Utilities.Strings.IsValidVariableName(name))
        {
            logger.Error($"\"{name}\" is not a valid tool name, please start with a letter.");
            return false;
        }

        Tool tool = Tool.Create(name,
            tcpX, tcpY, tcpZ,
            tcp_vX0, tcp_vX1, tcp_vX2,
            tcp_vY0, tcp_vY1, tcp_vY2,
            weight,
            cogX, cogY, cogZ);
        return _control.IssueActionManager.IssueDefineToolRequest(tool);
    }

    /// <summary>
    /// Attach a Tool to the flange of this Robot. From this moment on, 
    /// all actions will refer to the new Tool Center Point (TCP) of this Tool.
    /// Note that the Tool must have been previously defined via "DefineTool".
    /// </summary>
    /// <param name="toolName"></param>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool AttachTool(string toolName)
    {
        if (!Utilities.Strings.IsValidVariableName(toolName))
        {
            logger.Error($"\"{toolName}\" is not a valid tool name, please start with a letter.");
            return false;
        }

        return _control.IssueActionManager.IssueAttachRequest(toolName);
    }

    /// <summary>
    /// Detach all Tools from the flange of this Robot. From this moment on, 
    /// all actions will refer to the flange as the Tool Center Point (TCP).
    /// </summary>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool DetachTool()
    {
        return _control.IssueActionManager.IssueDetachRequest();
    }


    /// <summary>
    /// Attach a Tool to the flange of this Robot.
    /// From this moment, all Actions like Move or Rotate will refer
    /// to the Tool Center Point (TCP).
    /// </summary>
    /// <param name="tool"></param>
    /// <returns></returns>
    [System.Obsolete("Use AttachTool() instead.")]
    public bool Attach(Tool tool)
    {
        logger.Warning("Attach is deprecated, Use AttachTool() instead.");

        bool success = _control.IssueActionManager.IssueDefineToolRequest(tool);
        success &= _control.IssueActionManager.IssueAttachRequest(tool.name);
        return success;

        //return c.IssueAttachRequest(tool);
    }

    /// <summary>
    /// Detach all Tools from the flange of this Robot.
    /// From this moment, all Actions like Move or Rotate will refer
    /// to the Flange Center Point (FCP).
    /// </summary>
    /// <returns></returns>
    [System.Obsolete("Detach is deprecated, Use DetachTool() instead.")]
    public bool Detach()
    {
        logger.Warning("Detach is deprecated, Use DetachTool() instead.");
        return _control.IssueActionManager.IssueDetachRequest();
    }


    /// <summary>
    /// Writes to the digital IO pin.
    /// </summary>
    /// <param name="pinNumber"></param>
    /// <param name="isOn"></param>
    /// <param name="toolPin">Is this pin on the tool?</param>
    public bool WriteDigital(int pinNumber, bool isOn, bool toolPin = false)
    {
        return _control.IssueActionManager.IssueWriteToDigitalIORequest(pinNumber.ToString(), isOn, toolPin);
    }

    /// <summary>
    /// Writes to the digital IO pin.
    /// </summary>
    /// <param name="pinId">Pin name.</param>
    /// <param name="isOn"></param>
    /// <param name="toolPin">Is this pin on the tool?</param>
    [ParseableFromStringAttribute]
    public bool WriteDigital(string pinId, bool isOn, bool toolPin = false)
    {
        return _control.IssueActionManager.IssueWriteToDigitalIORequest(pinId, isOn, toolPin);
    }

    /// <summary>
    /// Writes to the analog IO pin.
    /// </summary>
    /// <param name="pinNumber"></param>
    /// <param name="value"></param>
    /// <param name="toolPin">Is this pin on the tool?</param>
    public bool WriteAnalog(int pinNumber, double value, bool toolPin = false)
    {
        return _control.IssueActionManager.IssueWriteToAnalogIORequest(pinNumber.ToString(), value, toolPin);
    }

    /// <summary>
    /// Writes to the analog IO pin.
    /// </summary>
    /// <param name="pinId">Pin name.</param>
    /// <param name="value"></param>
    /// <param name="toolPin">Is this pin on the tool?</param>
    [ParseableFromStringAttribute]
    public bool WriteAnalog(string pinId, double value, bool toolPin = false)
    {
        return _control.IssueActionManager.IssueWriteToAnalogIORequest(pinId, value, toolPin);
    }
    #endregion

}
