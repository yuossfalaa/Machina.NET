using System;
using Machina.Attributes;

namespace Machina;

//hold some settings that can be Useful for 3D printing operations
public partial class Robot
{
    #region 3D related Settings Methods
    /// <summary>
    /// Increments the working temperature of one of the device's parts. Useful for 3D printing operations. 
    /// </summary>
    /// <param name="temp">Temperature increment in °C.</param>
    /// <param name="devicePart">Device's part that will change temperature, e.g. "extruder", "bed", etc.</param>
    /// <param name="waitToReachTemp">If true, execution will wait for the part to heat up and resume when reached the target.</param>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool Temperature(double temp, string devicePart, bool waitToReachTemp = true)
    {
        RobotPartType tt;
        try
        {
            tt = (RobotPartType)Enum.Parse(typeof(RobotPartType), devicePart, true);
            if (Enum.IsDefined(typeof(RobotPartType), tt))
            {
                return _control.IssueActionManager.IssueTemperatureRequest(temp, tt, waitToReachTemp, true);
            }
        }
        catch
        {
            logger.Error($"{devicePart} is not a valid target part for temperature changes, please specify one of the following: ");
            foreach (string str in Enum.GetNames(typeof(RobotPartType)))
            {
                logger.Error(str);
            }
        }
        return false;
    }

    /// <summary>
    /// Sets the working temperature of one of the device's parts. Useful for 3D printing operations. 
    /// </summary>
    /// <param name="temp">Temperature increment in °C.</param>
    /// <param name="devicePart">Device's part that will change temperature, e.g. "extruder", "bed", etc.</param>
    /// <param name="waitToReachTemp">If true, execution will wait for the part to heat up and resume when reached the target.</param>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool TemperatureTo(double temp, string devicePart, bool waitToReachTemp = true)
    {
        RobotPartType tt;
        try
        {
            tt = (RobotPartType)Enum.Parse(typeof(RobotPartType), devicePart, true);
            if (Enum.IsDefined(typeof(RobotPartType), tt))
            {
                return _control.IssueActionManager.IssueTemperatureRequest(temp, tt, waitToReachTemp, false);
            }
        }
        catch
        {
            logger.Error($"{devicePart} is not a valid target part for temperature changes, please specify one of the following: ");
            foreach (string str in Enum.GetNames(typeof(RobotPartType)))
            {
                logger.Error(str);
            }
        }
        return false;
    }

    /// <summary>
    /// Increases the extrusion rate of filament for 3D printers.
    /// </summary>
    /// <param name="rateInc">Increment of mm of filament per mm of movement.</param>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool ExtrusionRate(double rateInc)
    {
        return _control.IssueActionManager.IssueExtrusionRateRequest(rateInc, true);
    }

    /// <summary>
    /// Sets the extrusion rate of filament for 3D printers.
    /// </summary>
    /// <param name="rate">mm of filament per mm of movement.</param>
    /// <returns></returns>
    [ParseableFromStringAttribute]
    public bool ExtrusionRateTo(double rate)
    {
        return _control.IssueActionManager.IssueExtrusionRateRequest(rate, false);
    }
    #endregion

}
