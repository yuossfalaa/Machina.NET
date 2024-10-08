using System;
using Machina.Attributes;

namespace Machina;

//Robot part Hold Motion Settings Like Speed / acceleration  
public partial class Robot
{
    #region Motion Settings Methods
    /// <summary>
    /// Sets the motion type (linear, joint...) for future issued Actions.
    /// </summary>
    /// <param name="motionType"></param>
    public bool MotionMode(MotionType motionType)
    {
        return _control.IssueActionManager.IssueMotionRequest(motionType);
    }

    /// <summary>
    /// Sets the motion type (linear, joint...) for future issued Actions.
    /// </summary>
    /// <param name="motionType">"linear", "joint", etc.</param>
    [ParseableFromString]
    public bool MotionMode(string motionType)
    {
        MotionType mt;
        try
        {
            mt = (MotionType)Enum.Parse(typeof(MotionType), motionType, true);
            if (Enum.IsDefined(typeof(MotionType), mt))
            {
                return _control.IssueActionManager.IssueMotionRequest(mt);
            }
        }
        catch
        {
            logger.Error($"{motionType} is not a valid target part for motion type changes, please specify one of the following: ");
            foreach (string str in Enum.GetNames(typeof(MotionType)))
            {
                logger.Error(str);
            }
        }
        return false;
    }

    /// <summary>
    /// Increase the speed at which new Actions will be executed. This value will be applied to linear motion in mm/s, and rotational or angular motion in deg/s.
    /// </summary>
    /// <param name="speedInc">Speed increment in mm/s or deg/s.</param>
    [ParseableFromString]
    public bool Speed(double speedInc)
    {
        return _control.IssueActionManager.IssueSpeedRequest(speedInc, true);
    }

    /// <summary>
    /// Set the speed at which new Actions will be executed. This value will be applied to linear motion in mm/s, and rotational or angular motion in deg/s.
    /// </summary>
    /// <param name="speed">Speed value in mm/s or deg/s.</param>
    [ParseableFromString]
    public bool SpeedTo(double speed)
    {
        return _control.IssueActionManager.IssueSpeedRequest(speed, false);
    }

    /// <summary>
    /// Increase the acceleration at which new Actions will be executed. This value will be applied to linear motion in mm/s^2, and rotational or angular motion in deg/s^2.
    /// </summary>
    /// <param name="accInc">Acceleration increment in mm/s^2 or deg/s^2.</param>
    /// <returns></returns>
    [ParseableFromString]
    public bool Acceleration(double accInc)
    {
        return _control.IssueActionManager.IssueAccelerationRequest(accInc, true);
    }

    /// <summary>
    /// Set the acceleration at which new Actions will be executed. This value will be applied to linear motion in mm/s^2, and rotational or angular motion in deg/s^2.
    /// </summary>
    /// <param name="acceleration">Acceleration value in mm/s^2 or deg/s^2.</param>
    /// <returns></returns>
    [ParseableFromString]
    public bool AccelerationTo(double acceleration)
    {
        return _control.IssueActionManager.IssueAccelerationRequest(acceleration, false);
    }

    /// <summary>
    /// Increase the default precision value new Actions will be given. 
    /// Precision is measured as the radius of the smooth interpolation
    /// between motion targets. This is refered to as "Zone", "Approximate
    /// Positioning" or "Blending Radius" in different platforms. 
    /// </summary>
    /// <param name="radiusInc">Smoothing radius increment in mm.</param>
    [ParseableFromString]
    public bool Precision(double radiusInc)
    {
        return _control.IssueActionManager.IssuePrecisionRequest(radiusInc, true);
    }

    /// <summary>
    /// Set the default precision value new Actions will be given. 
    /// Precision is measured as the radius of the smooth interpolation
    /// between motion targets. This is refered to as "Zone", "Approximate
    /// Positioning" or "Blending Radius" in different platforms. 
    /// </summary>
    /// <param name="radius">Smoothing radius in mm.</param>
    [ParseableFromString]
    public bool PrecisionTo(double radius)
    {
        return _control.IssueActionManager.IssuePrecisionRequest(radius, false);
    }

    /// <summary>
    /// Sets the reference system used for relative transformations.
    /// </summary>
    /// <param name="refcs"></param>
    public bool Coordinates(ReferenceCS refcs)
    {
        return _control.IssueActionManager.IssueCoordinatesRequest(refcs);
    }

    /// <summary>
    /// Sets the reference system used for relative transformations ("local", "global", etc.)
    /// </summary>
    /// <param name="type"></param>
    public bool Coordinates(string type)
    {
        ReferenceCS refcs;
        try
        {
            refcs = (ReferenceCS)Enum.Parse(typeof(ReferenceCS), type, true);
            if (Enum.IsDefined(typeof(ReferenceCS), refcs))
            {
                return Coordinates(refcs);
            }
        }
        catch
        {
            logger.Error($"{type} is not a Coordinate System, please specify one of the following: ");
            foreach (string str in Enum.GetNames(typeof(ReferenceCS)))
            {
                logger.Error(str);
            }
        }

        return false;
    }
    #endregion
}
