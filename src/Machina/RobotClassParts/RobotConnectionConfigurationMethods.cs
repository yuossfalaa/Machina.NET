using System;

namespace Machina;

// holds robot part for connection config Methods
public partial class Robot
{
    #region Connection Configuration Methods
    /// What was this even for? Exports checks?
    public bool IsBrand(string brandName)
    {
        return this.Brand.ToString().Equals(brandName, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsBrand(RobotType brand)
    {
        return brand == this.Brand;
    }

    /// <summary>
    /// Configure how Actions are streamed to the controller.
    /// </summary>
    /// <param name="minActionOnController">When Machina detects that the controller has these many Actions or less buffered, it will start streaming new Actions.</param>
    /// <param name="maxActionsOnController">When Maxhina detects that the controller has these many Actions or more buffered, it will stop streaming and wait for them to reach minActionOnController to stream more.</param>
    /// <returns></returns>
    public bool StreamConfiguration(int minActionOnController, int maxActionsOnController)
    {
        return _control.ConfigureBuffer(minActionOnController, maxActionsOnController);
    }

    /// <summary>
    /// Sets the control type the robot will operate under, like "offline" or "online".
    /// </summary>
    /// <param name="controlType"></param>
    /// <returns></returns>
    public bool ControlMode(ControlType controlType)
    {
        return _control.SetControlMode(controlType);
    }

    /// <summary>
    /// Sets the control type the robot will operate under, like "offline" or "online".
    /// </summary>
    /// <param name="controlType"></param>
    /// <returns></returns>
    public bool ControlMode(string controlType)
    {
        ControlType ct;
        try
        {
            ct = (ControlType)Enum.Parse(typeof(ControlType), controlType, true);
            if (Enum.IsDefined(typeof(ControlType), ct))
            {
                return _control.SetControlMode(ct);
            }
        }
        catch
        {
            logger.Error($"{controlType} is not a valid ControlMode type, please specify one of the following:");
            foreach (string str in Enum.GetNames(typeof(ControlType)))
            {
                logger.Error(str);
            }
        }
        return false;
    }

    /// <summary>
    /// Sets who will be in charge of managing the connection to the device,
    /// i.e. having "Machina" try to load driver modules to the controller or 
    /// leave that task to the "User" (default).
    /// </summary>
    /// <param name="connectionManager">"User" or "Machina"</param>
    /// <returns></returns>
    public bool ConnectionManager(string connectionManager)
    {
        ConnectionType cm;
        try
        {
            cm = (ConnectionType)Enum.Parse(typeof(ConnectionType), connectionManager, true);
            if (Enum.IsDefined(typeof(ConnectionType), cm))
                return _control.SetConnectionMode(cm);
        }
        catch
        {
            logger.Error($"{connectionManager} is not a valid ConnectionManagerType type, please specify one of the following:");
            foreach (string str in Enum.GetNames(typeof(ConnectionType)))
                logger.Error(str);
        }
        return false;
    }

    /// <summary>
    /// Sets who will be in charge of managing the connection to the device,
    /// i.e. having "Machina" try to load driver modules to the controller or 
    /// leave that task to the "User" (default).
    /// </summary>
    /// <param name="connectionManager">"User" or "Machina"</param>
    /// <returns></returns>
    public bool ConnectionManager(ConnectionType connectionManager)
    {
        return _control.SetConnectionMode(connectionManager);
    }

    /// <summary>
    /// If the controller needs special user logging, set the credentials here. 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public bool SetUser(string name, string password)
    {
        return _control.SetUserCredentials(name, password);
    }

    #endregion
}
