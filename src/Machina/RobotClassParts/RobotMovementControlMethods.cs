using System;
using Machina.Attributes;
using Machina.Types.Geometry;

namespace Machina;

// robot part that holds all Methods For Basic Movements 
//Note: other movement exist , and in other parts of the class
public partial class Robot
{
    #region Movement Control Methods
    /// <summary>
    /// Issue a relative movement action request on current coordinate system.
    /// </summary>
    /// <param name="direction"></param>
    /// <returns></returns>
    public bool Move(Vector direction)
    {
        return _control.IssueActionManager.IssueTranslationRequest(direction, true);
    }

    /// <summary>
    /// Issue a relative movement action request on current coordinate system.
    /// </summary>
    /// <param name="incX"></param>
    /// <param name="incY"></param>
    /// <param name="incZ"></param>
    /// <returns></returns>
    [ParseableFromString]
    public bool Move(double incX, double incY, double incZ = 0)
    {
        return Move(new Vector(incX, incY, incZ));
    }

    /// <summary>
    /// Issue an absolute movement action request.
    /// </summary>
    /// <param name="position"></param>
    /// <returns></returns>
    public bool MoveTo(Point position)
    {
        return _control.IssueActionManager.IssueTranslationRequest(position, false);
    }

    /// <summary>
    /// Issue an absolute movement action request.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <returns></returns>
    [ParseableFromString]
    public bool MoveTo(double x, double y, double z)
    {
        return MoveTo(new Vector(x, y, z));
    }

    /// <summary>
    /// Issue a RELATIVE rotation action request according to the current reference system.
    /// </summary>
    /// <param name="rotation"></param>
    /// <returns></returns>
    public bool Rotate(Rotation rotation)
    {
        return _control.IssueActionManager.IssueRotationRequest(rotation, true);
    }

    /// <summary>
    /// Issue a RELATIVE rotation action request according to the current reference system.
    /// </summary>
    /// <param name="vector"></param>
    /// <param name="angDegs"></param>
    /// <returns></returns>
    public bool Rotate(Vector vector, double angDegs)
    {
        return Rotate(new Rotation(vector.X, vector.Y, vector.Z, angDegs, true));
    }

    /// <summary>
    /// Issue a RELATIVE rotation action request according to the current reference system.
    /// </summary>
    /// <param name="rotVecX"></param>
    /// <param name="rotVecY"></param>
    /// <param name="rotVecZ"></param>
    /// <param name="angDegs"></param>
    /// <returns></returns>
    [ParseableFromString]
    public bool Rotate(double rotVecX, double rotVecY, double rotVecZ, double angDegs)
    {
        return Rotate(new Rotation(rotVecX, rotVecY, rotVecZ, angDegs, true));
    }

    /// <summary>
    /// Issue an ABSOLUTE reorientation request according to the current reference system.
    /// </summary>
    /// <param name="rotation"></param>
    /// <returns></returns>
    public bool RotateTo(Rotation rotation)
    {
        return _control.IssueActionManager.IssueRotationRequest(rotation, false);
    }

    /// <summary>
    /// Issue an ABSOLUTE reorientation request according to the current reference system.
    /// </summary>
    /// <param name="cs"></param>
    /// <returns></returns>
    public bool RotateTo(Orientation cs)
    {
        return RotateTo((Rotation)cs);
    }

    /// <summary>
    /// Issue an ABSOLUTE reorientation request according to the current reference system.
    /// </summary>
    /// <param name="vecX"></param>
    /// <param name="vecY"></param>
    /// <returns></returns>
    public bool RotateTo(Vector vecX, Vector vecY)
    {
        return RotateTo((Rotation)new Orientation(vecX, vecY));
    }

    /// <summary>
    /// Issue an ABSOLUTE reorientation request according to the current reference system.
    /// </summary>
    /// <param name="x0"></param>
    /// <param name="x1"></param>
    /// <param name="x2"></param>
    /// <param name="y0"></param>
    /// <param name="y1"></param>
    /// <param name="y2"></param>
    /// <returns></returns>
    [ParseableFromString]
    public bool RotateTo(double x0, double x1, double x2, double y0, double y1, double y2)
    {
        return RotateTo((Rotation)new Orientation(x0, x1, x2, y0, y1, y2));
    }

    /// <summary>
    /// Issue a compound RELATIVE local Translation + Rotation request
    /// according to the current reference system.
    /// Note that, if using local coordinates, order of Actions will matter. 
    /// TODO: wouldn't they matter too if they were in global coordinates?
    /// TODO II: should this be changed to simply mean a sort of plane to plane transform? 
    /// Such change would make this more intuitive...
    /// </summary>
    /// <param name="direction"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    public bool Transform(Vector direction, Rotation rotation)
    {

        return _control.IssueActionManager.IssueTransformationRequest(direction, rotation, true, true);
    }

    /// <summary>
    /// Issue a compound RELATIVE local Rotation + Translation request
    /// according to the current reference system.
    /// Note that, if using local coordinates, order of Actions will matter. 
    /// TODO: wouldn't they matter too if they were in global coordinates?
    /// TODO II: should this be changed to simply mean a sort of plane to plane transform? 
    /// Such change would make this more intuitive...
    /// </summary>
    /// <param name="rotation"></param>
    /// <param name="direction"></param>
    /// <returns></returns>
    public bool Transform(Rotation rotation, Vector direction)
    {

        return _control.IssueActionManager.IssueTransformationRequest(direction, rotation, true, false);
    }

    /// <summary>
    /// Issue a compound ABSOLUTE global Translation + Rotation request
    /// according to the current reference system.
    /// </summary>
    /// <param name="position"></param>
    /// <param name="rotation"></param>
    /// <returns></returns>
    public bool TransformTo(Point position, Orientation orientation)
    {
        return _control.IssueActionManager.IssueTransformationRequest(position, orientation, false, true);
    }

    /// <summary>
    /// Issue a compound ABSOLUTE global Translation + Rotation request
    /// according to the current reference system.
    /// </summary>
    /// <param name="rotation"></param>
    /// <param name="position"></param>
    /// <returns></returns>
    public bool TransformTo(Orientation orientation, Point position)
    {

        return _control.IssueActionManager.IssueTransformationRequest(position, orientation, false, false);
    }

    /// <summary>
    /// Issue a compound ABSOLUTE global Translation + Rotation request
    /// according to the current reference system.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="vX0"></param>
    /// <param name="vX1"></param>
    /// <param name="vX2"></param>
    /// <param name="vY0"></param>
    /// <param name="vY1"></param>
    /// <param name="vY2"></param>
    /// <returns></returns>
    [ParseableFromString]
    public bool TransformTo(double x, double y, double z, double vX0, double vX1, double vX2, double vY0, double vY1, double vY2)
    {
        return _control.IssueActionManager.IssueTransformationRequest(new Vector(x, y, z), new Orientation(vX0, vX1, vX2, vY0, vY1, vY2), false, true);
    }

    /// <summary>
    /// Issue a RELATIVE arc motion request according to the current reference system.
    /// Orientation will remain constant throughout the motion.
    /// </summary>
    /// <param name="through">Point to fit the arc motion through.</param>
    /// <param name="end">Point to end the arc motion at.</param>
    /// <returns></returns>
    public bool ArcMotion(Point through, Point end)
    {
        Plane throughP = new Plane(
            through.X, through.Y, through.Z,
            1, 0, 0,
            0, 1, 0);  // world XY, won't be used anyway

        Plane endP = new Plane(
            end.X, end.Y, end.Z,
            1, 0, 0,
            0, 1, 0);  // world XY, won't be used anyway

        return _control.IssueActionManager.IssueArcMotionRequest(throughP, endP, true, true);
    }

    /// <summary>
    /// Issue a RELATIVE arc motion request according to the current reference system.
    /// Orientation will remain constant throughout the motion.
    /// </summary>
    /// <param name="throughX"></param>
    /// <param name="throughY"></param>
    /// <param name="throughZ"></param>
    /// <param name="endX"></param>
    /// <param name="endY"></param>
    /// <param name="endZ"></param>
    /// <returns></returns>
    [ParseableFromString]
    public bool ArcMotion(
        double throughX, double throughY, double throughZ,
        double endX, double endY, double endZ)
    {
        Plane through = new Plane(
            throughX, throughY, throughZ,
            1, 0, 0,
            0, 1, 0);  // world XY, won't be used anyway

        Plane end = new Plane(
            endX, endY, endZ,
            1, 0, 0,
            0, 1, 0);  // world XY, won't be used anyway

        return _control.IssueActionManager.IssueArcMotionRequest(through, end, true, true);
    }

    /// <summary>
    /// Issue an ABSOLUTE arc motion request according to the current reference system.
    /// Orientation will remain constant throughout the motion.
    /// </summary>
    /// <param name="through">Point to fit the arc motion through.</param>
    /// <param name="end">Point to end the arc motion at.</param>
    /// <returns></returns>
    public bool ArcMotionTo(Point through, Point end)
    {
        Plane throughP = new Plane(
            through.X, through.Y, through.Z,
            1, 0, 0,
            0, 1, 0);  // world XY, won't be used anyway

        Plane endP = new Plane(
            end.X, end.Y, end.Z,
            1, 0, 0,
            0, 1, 0);  // world XY, won't be used anyway

        return _control.IssueActionManager.IssueArcMotionRequest(throughP, endP, false, true);
    }

    /// <summary>
    /// Issue an ABSOLUTE arc motion request according to the current reference system.
    /// Orientation will remain constant throughout the motion.
    /// </summary>
    /// <param name="throughX"></param>
    /// <param name="throughY"></param>
    /// <param name="throughZ"></param>
    /// <param name="endX"></param>
    /// <param name="endY"></param>
    /// <param name="endZ"></param>
    /// <returns></returns>
    [ParseableFromString]
    public bool ArcMotionTo(
        double throughX, double throughY, double throughZ,
        double endX, double endY, double endZ)
    {
        Plane through = new Plane(
            throughX, throughY, throughZ,
            1, 0, 0,
            0, 1, 0);  // world XY, won't be used anyway

        Plane end = new Plane(
            endX, endY, endZ,
            1, 0, 0,
            0, 1, 0);  // world XY, won't be used anyway

        return _control.IssueActionManager.IssueArcMotionRequest(through, end, false, true);
    }

    /// <summary>
    /// Issue an ABSOLUTE arc motion request according to the current reference system.
    /// Orientation will also change through the fit planes.
    /// </summary>
    /// <param name="through">Plane to fit the arc motion through.</param>
    /// <param name="end">Plane to end the arc motion at.</param>
    /// <returns></returns>
    public bool ArcMotionTo(Plane through, Plane end)
    {
        // Use deep copies to avoid reference conflicts 
        // @TODO: really need to convert geo types to structs soon!
        return _control.IssueActionManager.IssueArcMotionRequest(through.Clone(), end.Clone(), false, false);
    }

    /// <summary>
    /// Issue an ABSOLUTE arc motion request according to the current reference system.
    /// Orientation will also change through the fit planes.
    /// </summary>
    /// <param name="throughX"></param>
    /// <param name="throughY"></param>
    /// <param name="throughZ"></param>
    /// <param name="throughVX0"></param>
    /// <param name="throughVX1"></param>
    /// <param name="throughVX2"></param>
    /// <param name="throughVY0"></param>
    /// <param name="throughVY1"></param>
    /// <param name="throughVY2"></param>
    /// <param name="endX"></param>
    /// <param name="endY"></param>
    /// <param name="endZ"></param>
    /// <param name="endVX0"></param>
    /// <param name="endVX1"></param>
    /// <param name="endVX2"></param>
    /// <param name="endVY0"></param>
    /// <param name="endVY1"></param>
    /// <param name="endVY2"></param>
    /// <returns></returns>
    public bool ArcMotionTo(
        double throughX, double throughY, double throughZ,
        double throughVX0, double throughVX1, double throughVX2,
        double throughVY0, double throughVY1, double throughVY2,
        double endX, double endY, double endZ,
        double endVX0, double endVX1, double endVX2,
        double endVY0, double endVY1, double endVY2)
    {
        //This would be nice to have [ParseableFromString] too, but we currently cannot discriminate between multiple functions with the same name... 
        Plane through = new Plane(
            throughX, throughY, throughZ,
            throughVX0, throughVX1, throughVX2,
            throughVY0, throughVY1, throughVY2);

        Plane end = new Plane(
            endX, endY, endZ,
            endVX0, endVX1, endVX2,
            endVY0, endVY1, endVY2);

        return _control.IssueActionManager.IssueArcMotionRequest(through, end, false, false);
    }


    /// <summary>
    /// Issue a request to increment the angular values of the robot joint axes rotations.
    /// Values expressed in degrees.
    /// </summary>
    /// <param name="incJoints"></param>
    /// <returns></returns>
    public bool Axes(Joints incJoints)
    {
        return _control.IssueActionManager.IssueJointsRequest(incJoints, true);
    }

    /// <summary>
    /// Issue a request to increment the angular values of the robot joint axes rotations.
    /// Values expressed in degrees.
    /// </summary>
    /// <param name="incJ1"></param>
    /// <param name="incJ2"></param>
    /// <param name="incJ3"></param>
    /// <param name="incJ4"></param>
    /// <param name="incJ5"></param>
    /// <param name="incJ6"></param>
    /// <returns></returns>
    [ParseableFromString]
    public bool Axes(double incJ1, double incJ2, double incJ3, double incJ4, double incJ5, double incJ6)
    {
        return _control.IssueActionManager.IssueJointsRequest(new Joints(incJ1, incJ2, incJ3, incJ4, incJ5, incJ6), true);
    }

    /// <summary>
    /// Issue a request to set the angular values of the robot joint axes rotations.
    /// Values expressed in degrees.
    /// </summary>
    /// <param name="joints"></param>
    /// <returns></returns>
    /// 
    public bool AxesTo(Joints joints)
    {
        return _control.IssueActionManager.IssueJointsRequest(joints, false);
    }

    /// <summary>
    /// Issue a request to set the angular values of the robot joint axes rotations.
    /// Values expressed in degrees.
    /// </summary>
    /// <param name="j1"></param>
    /// <param name="j2"></param>
    /// <param name="j3"></param>
    /// <param name="j4"></param>
    /// <param name="j5"></param>
    /// <param name="j6"></param>
    /// <returns></returns>
    [ParseableFromString]
    public bool AxesTo(double j1, double j2, double j3, double j4, double j5, double j6)
    {
        return _control.IssueActionManager.IssueJointsRequest(new Joints(j1, j2, j3, j4, j5, j6), false);
    }

    /// <summary>
    /// Increase the value of one of the robot's external axis. 
    /// Values expressed in degrees or milimeters, depending on the nature of the external axis.
    /// Note that the effect of this change of external axis will go in effect on the next motion Action.
    /// </summary>
    /// <param name="axisNumber">Axis number from 1 to 6.</param>
    /// <param name="increment">Increment value in mm or degrees.</param>
    [ParseableFromString]
    public bool ExternalAxis(int axisNumber, double increment)
    {
        if (axisNumber == 0)
        {
            logger.Error("Please enter an axis number between 1-6");
            return false;
        }
        return _control.IssueActionManager.IssueExternalAxisRequest(axisNumber, increment, ExternalAxesTarget.All, true);
    }

    /// <summary>
    /// Increase the value of one of the robot's external axis. 
    /// Values expressed in degrees or milimeters, depending on the nature of the external axis.
    /// Note that the effect of this change of external axis will go in effect on the next motion Action.
    /// </summary>
    /// <param name="axisNumber">Axis number from 1 to 6.</param>
    /// <param name="increment">Increment value in mm or degrees.</param>
    /// <param name="externalAxesTarget">Apply this change to all external axes, or only cartesian/joint targets.</param>
    /// <returns></returns>
    public bool ExternalAxis(int axisNumber, double increment, string externalAxesTarget)
    {
        ExternalAxesTarget eat;
        try
        {
            eat = (ExternalAxesTarget)Enum.Parse(typeof(ExternalAxesTarget), externalAxesTarget, true);
            if (Enum.IsDefined(typeof(ExternalAxesTarget), eat))
            {
                return ExternalAxis(axisNumber, increment, eat);
            }
        }
        catch
        {
            logger.Error($"{externalAxesTarget} is not a valid ExternalAxesTarget type, please specify one of the following:");
            foreach (string str in Enum.GetNames(typeof(ExternalAxesTarget)))
            {
                logger.Error(str);
            }
        }
        return false;
    }

    /// <summary>
    /// Increase the value of one of the robot's external axis. 
    /// Values expressed in degrees or milimeters, depending on the nature of the external axis.
    /// Note that the effect of this change of external axis will go in effect on the next motion Action.
    /// </summary>
    /// <param name="axisNumber">Axis number from 1 to 6.</param>
    /// <param name="increment">Increment value in mm or degrees.</param>
    /// <param name="externalAxesTarget">Apply this change to all external axes, or only cartesian/joint targets.</param>
    public bool ExternalAxis(int axisNumber, double increment, ExternalAxesTarget externalAxesTarget)
    {
        if (axisNumber == 0)
        {
            logger.Error("Please enter an axis number between 1-6");
            return false;
        }
        return _control.IssueActionManager.IssueExternalAxisRequest(axisNumber, increment, externalAxesTarget, true);
    }

    /// <summary>
    /// Set the value of one of the robot's external axis. 
    /// Values expressed in degrees or milimeters, depending on the nature of the external axis.
    /// Note that the effect of this change of external axis will go in effect on the next motion Action.
    /// </summary>
    /// <param name="axisNumber">Axis number from 1 to 6.</param>
    /// <param name="value">Axis value in mm or degrees.</param>
    [ParseableFromString]
    public bool ExternalAxisTo(int axisNumber, double value)
    {
        if (axisNumber == 0)
        {
            logger.Error("Please enter an axis number between 1-6");
            return false;
        }
        return _control.IssueActionManager.IssueExternalAxisRequest(axisNumber, value, ExternalAxesTarget.All, false);
    }

    /// <summary>
    /// Set the value of one of the robot's external axis. 
    /// Values expressed in degrees or milimeters, depending on the nature of the external axis.
    /// Note that the effect of this change of external axis will go in effect on the next motion Action.
    /// </summary>
    /// <param name="axisNumber">Axis number from 1 to 6.</param>
    /// <param name="value">Axis value in mm or degrees.</param>
    /// <param name="externalAxesTarget">Apply this change to all external axes, or only cartesian/joint targets.</param>
    /// <returns></returns>
    public bool ExternalAxisTo(int axisNumber, double value, string externalAxesTarget)
    {
        ExternalAxesTarget eat;
        try
        {
            eat = (ExternalAxesTarget)Enum.Parse(typeof(ExternalAxesTarget), externalAxesTarget, true);
            if (Enum.IsDefined(typeof(ExternalAxesTarget), eat))
            {
                return ExternalAxisTo(axisNumber, value, eat);
            }
        }
        catch
        {
            logger.Error($"{externalAxesTarget} is not a valid ExternalAxesTarget type, please specify one of the following:");
            foreach (string str in Enum.GetNames(typeof(ExternalAxesTarget)))
            {
                logger.Error(str);
            }
        }
        return false;
    }

    /// <summary>
    /// Set the value of one of the robot's external axis. 
    /// Values expressed in degrees or milimeters, depending on the nature of the external axis.
    /// Note that the effect of this change of external axis will go in effect on the next motion Action.
    /// </summary>
    /// <param name="axisNumber">Axis number from 1 to 6.</param>
    /// <param name="value">Axis value in mm or degrees.</param>
    /// <param name="externalAxesTarget">Apply this change to all external axes, or only cartesian/joint targets.</param>
    public bool ExternalAxisTo(int axisNumber, double value, ExternalAxesTarget externalAxesTarget)
    {
        if (axisNumber == 0)
        {
            logger.Error("Please enter an axis number between 1-6");
            return false;
        }
        return _control.IssueActionManager.IssueExternalAxisRequest(axisNumber, value, externalAxesTarget, false);
    }

    // At the moment, allow only absolute setting, since the controller may change his value to find an IK solution to the target.
    // @TODO: bring back as soon as Machina does the IK.
    //public bool ArmAngle(double increment)
    //{
    //    return c.IssueArmAngleRequest(increment, true);
    //}

    /// <summary>
    /// Set the value of the arm-angle parameter.
    /// This value represents the planar offset around the 7th axis for 7-dof robotic arms.
    /// </summary>
    /// <param name="value">Angular value in degrees.</param>
    /// <returns></returns>
    public bool ArmAngleTo(double value)
    {
        return _control.IssueActionManager.IssueArmAngleRequest(value, false);
    }


    /// <summary>
    /// Issue a request to wait idle before moving to next action. 
    /// </summary>
    /// <param name="timeMillis">Time expressed in milliseconds</param>
    /// <returns></returns>
    [ParseableFromString]
    public bool Wait(long timeMillis)
    {
        return _control.IssueActionManager.IssueWaitRequest(timeMillis);
    }


    #endregion

}
