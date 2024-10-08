using System.Collections.Generic;
using Machina.Types.Geometry;

namespace Machina;

// Robot Part with Getters in it
public partial class Robot
{
    #region Getters
    /// <summary>
    /// Returns the module files necessary to load on the device for a successful connection.
    /// </summary>
    /// <param name="parameters">Values necessary to be replaced on the modules, such as {"HOSTNAME","192.168.125.1"} or {"PORT","7000"}.</param>
    /// <returns>A dict with filename-filecontent pairs.</returns>
    public Dictionary<string, string> GetDeviceDriverModules(Dictionary<string, string> parameters) => _control.GetDeviceDriverModules(parameters);

    /// <summary>
    /// Returns a Point representation of the Robot's TCP position in mm and World coordinates.
    /// </summary>
    /// <returns></returns>
    public Point GetCurrentPosition() => _control.GetCurrentPosition();

    /// <summary>
    /// Returns a Rotation representation of the Robot's TCP orientation in quaternions.
    /// </summary>
    /// <returns></returns>
    public Rotation GetCurrentRotation() => _control.GetCurrentRotation();

    /// <summary>
    /// Returns a Joint object representing the rotations in the robot axes.
    /// </summary>
    /// <returns></returns>
    public Joints GetCurrentAxes() => _control.GetCurrentAxes();

    /// <summary>
    /// Retuns an ExternalAxes object representing the values of the external axes. If a value is null, that axis is not valid.
    /// </summary>
    /// <returns></returns>
    public ExternalAxes GetCurrentExternalAxes() => _control.GetCurrentExternalAxes();

    public double GetCurrentSpeed() => _control.GetCurrentSpeedSetting();

    public double GetCurrentAcceleration() => _control.GetCurrentAccelerationSetting();

    public double GetCurrentPrecision() => _control.GetCurrentPrecisionSetting();

    public MotionType GetCurrentMotionMode() => _control.GetCurrentMotionTypeSetting();

    /// <summary>
    /// Returns the Tool object currently attached to this Robot, null if none.
    /// </summary>
    /// <returns>The Tool object currently attached to this Robot, null if none.</returns>
    public Tool GetCurrentTool() => _control.GetCurrentTool();

    public override string ToString() => $"Robot[\"{this.Name}\", {this.Brand}]";



    #endregion
}
