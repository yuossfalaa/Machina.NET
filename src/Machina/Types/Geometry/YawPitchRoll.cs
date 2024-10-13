﻿using System;
using System.Globalization;

namespace Machina.Types.Geometry;
                                                                                                
/// <summary>
/// A class representing a Yaw-Pitch-Roll rotation, e.g. Euler Angles over intrinsic
/// ZY'X'' axes (Tait-Bryan angles). See http://en.wikipedia.org/wiki/Euler_angles.
/// </summary>
public class YawPitchRoll : Geometry
{
    /// <summary>
    /// Rotation around the X axis in degrees.
    /// </summary>
    public double XAngle { get; internal set; }

    /// <summary>
    /// Rotation around the Y axis in degrees.
    /// </summary>
    public double YAngle { get; internal set; }

    /// <summary>
    /// Rotation around the Z axis in degrees. 
    /// </summary>
    public double ZAngle { get; internal set; }

    /// <summary>
    /// Alias for rotation around X axis.
    /// </summary>
    public double Roll { get { return this.XAngle; } }

    /// <summary>
    /// Alias for rotation around Y axis.
    /// </summary>
    public double Pitch { get { return this.YAngle; } }

    /// <summary>
    /// Alias for rotation around Z axis.
    /// </summary>
    public double Yaw { get { return this.ZAngle; } }

    /// <summary>
    /// Alias for rotation around X axis.
    /// </summary>
    public double Bank { get { return this.XAngle; } }

    /// <summary>
    /// Alias for rotation around Y axis.
    /// </summary>
    public double Attitude { get { return this.YAngle; } }

    /// <summary>
    /// Alias for rotation around Z axis.
    /// </summary>
    public double Heading { get { return this.ZAngle; } }

    /// <summary>
    /// Test if this YawPitchRoll is approximately equal to another. 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsSimilar(YawPitchRoll other)
    {
        return Math.Abs(this.XAngle - other.XAngle) < EPSILON2
            && Math.Abs(this.YAngle - other.YAngle) < EPSILON2
            && Math.Abs(this.ZAngle - other.ZAngle) < EPSILON2;
    }

    /// <summary>
    /// Create a zero rotation.
    /// </summary>
    public YawPitchRoll() : this(0, 0, 0) { }

    /// <summary>
    /// Create an Euler Angles ZY'X'' intrinsic rotation from its constituent components in degrees.
    /// </summary>
    /// <param name="xAngle"></param>
    /// <param name="yAngle"></param>
    /// <param name="zAngle"></param>
    public YawPitchRoll(double xAngle, double yAngle, double zAngle)
    {
        this.XAngle = xAngle;
        this.YAngle = yAngle;
        this.ZAngle = zAngle;
    }


    /// <summary>
    /// Is this rotation equivalent to another? I.e. is the resulting orientation the same?
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsEquivalent(YawPitchRoll other)
    {
        // Quick and dirty (and expensive?) test, compare underlying Quaternions...
        return this.ToQuaternion().IsEquivalent(other.ToQuaternion());
    }

    /// <summary>
    /// Returns the Quaternion representation of this rotation.
    /// </summary>
    /// <returns></returns>
    public Quaternion ToQuaternion()
    {
        // From Shoemake, Ken. "Animating rotation with quaternion curves." ACM SIGGRAPH computer graphics. Vol. 19. No. 3. ACM, 1985.
        // (using a different Euler convention than EuclideanSpace)
        double cX = Math.Cos(0.5 * TO_RADS * this.XAngle),
               cY = Math.Cos(0.5 * TO_RADS * this.YAngle),
               cZ = Math.Cos(0.5 * TO_RADS * this.ZAngle),
               sX = Math.Sin(0.5 * TO_RADS * this.XAngle),
               sY = Math.Sin(0.5 * TO_RADS * this.YAngle),
               sZ = Math.Sin(0.5 * TO_RADS * this.ZAngle);

        return new Quaternion(cX * cY * cZ + sX * sY * sZ,
                              sX * cY * cZ - cX * sY * sZ,
                              cX * sY * cZ + sX * cY * sZ,
                              cX * cY * sZ - sX * sY * cZ, false);
    }

    /// <summary>
    /// Returns the Rotation Matrix representation os this rotation.
    /// </summary>
    /// <returns></returns>
    public RotationMatrix ToRotationMatrix()
    {
        // From https://en.wikipedia.org/wiki/Euler_angles#Tait.E2.80.93Bryan_angles
        double cX = Math.Cos(TO_RADS * this.XAngle),
               cY = Math.Cos(TO_RADS * this.YAngle),
               cZ = Math.Cos(TO_RADS * this.ZAngle),
               sX = Math.Sin(TO_RADS * this.XAngle),
               sY = Math.Sin(TO_RADS * this.YAngle),
               sZ = Math.Sin(TO_RADS * this.ZAngle);

        return new RotationMatrix(cY * cZ, sX * sY * cZ - cX * sZ, cX * sY * cZ + sX * sZ,
                                  cY * sZ, sX * sY * sZ + cX * cZ, cX * sY * sZ - sX * cZ,
                                      -sY, sX * cY, cX * cY, false);
    }

    /// <summary>
    /// Returns the Axis-Angle representation of this rotation.
    /// </summary>
    /// <returns></returns>
    public AxisAngle ToAxisAngle()
    {
        // This is just basically converting it to a quaternion, and then axis-angle: this.ToQuaternion().ToAxisAngle();
        double x, y, z, angle;
        double s;

        double cX = Math.Cos(0.5 * TO_RADS * this.XAngle),
               cY = Math.Cos(0.5 * TO_RADS * this.YAngle),
               cZ = Math.Cos(0.5 * TO_RADS * this.ZAngle),
               sX = Math.Sin(0.5 * TO_RADS * this.XAngle),
               sY = Math.Sin(0.5 * TO_RADS * this.YAngle),
               sZ = Math.Sin(0.5 * TO_RADS * this.ZAngle);

        angle = 2 * Math.Acos(cX * cY * cZ + sX * sY * sZ);
        s = Math.Sin(0.5 * angle);
        if (s < EPSILON2)
        {
            // This AxisAngle represents no rotation (full turn).
            x = y = z = 0;
        }
        else
        {
            // Untangle the underlying quaternion ;)
            x = (sX * cY * cZ - cX * sY * sZ) / s;
            y = (cX * sY * cZ + sX * cY * sZ) / s;
            z = (cX * cY * sZ - sX * sY * cZ) / s;
        }

        return new AxisAngle(x, y, z, TO_DEGS * angle, false);
    }

    /// <summary>
    /// Returns a Rotation Vector representation of this rotation.
    /// </summary>
    /// <returns></returns>
    public RotationVector ToRotationVector()
    {
        return this.ToAxisAngle().ToRotationVector();
    }


    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, 
            "EulerZYX[Z:{0}, Y:{1}, X:{2}]",
            Math.Round(this.ZAngle, STRING_ROUND_DECIMALS_RADS),
            Math.Round(this.YAngle, STRING_ROUND_DECIMALS_RADS),
            Math.Round(this.XAngle, STRING_ROUND_DECIMALS_RADS));
    }

}
