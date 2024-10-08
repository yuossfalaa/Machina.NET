namespace Machina;
// robot part that holds connection methods
public partial class Robot
{
    #region Connection INIT Method

    /// <summary>
    /// Scans the network for robotic devices, real or virtual, and performs all necessary 
    /// operations to connect to it. This is necessary for 'online' modes such as 'execute' and 'stream.'
    /// </summary>
    /// <param name="robotId">If multiple devices are connected, choose this id from the list.</param>
    /// <returns></returns>
    public bool Connect(int robotId = 0)
    {
        return _control.ConnectToDevice(robotId);
    }

    /// <summary>
    /// Tries to establish connection to a remote device for 'online' modes.
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    /// <returns></returns>
    public bool Connect(string ip, int port)
    {
        return _control.ConnectToDevice(ip, port);
    }

    /// <summary>
    /// Performs all necessary instructions to disconnect from and dispose a robot device, real or virtual. 
    /// This is necessary before leaving current execution thread.
    /// </summary>
    public bool Disconnect()
    {
        return _control.DisconnectFromDevice();
    }

    /// <summary>
    /// Returns a string representation of the IP of the currently connected robot device.
    /// </summary>
    /// <returns></returns>
    public string GetIP()
    {
        return _control.GetControllerIP();
    }
    #endregion
}
