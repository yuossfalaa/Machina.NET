﻿using System;
using System.Globalization;

namespace Machina.Types.Geometry;

/// <summary>
/// A class to represent a spatial rotation as a Rotation Vector: an unit rotation
/// axis multiplied by the rotation angle.
/// </summary>
public class RotationVector : Geometry
{
    /// <summary>
    /// X coordinate of the Rotation Vector in degrees.
    /// </summary>
    public double X { get; internal set; }

    /// <summary>
    /// Y coordinate of the Rotation Vector in degrees.
    /// </summary>
    public double Y { get; internal set; }

    /// <summary>
    /// Z coordinate of the Rotation Vector in degrees.
    /// </summary>
    public double Z { get; internal set; }

    /// <summary>
    /// Test if this RotationVector is approximately equal to another. 
    /// </summary>
    /// <param name="other"></param>
    /// <returns></returns>
    public bool IsSimilar(RotationVector other)
    {
        return Math.Abs(this.X - other.X) < EPSILON2
            && Math.Abs(this.Y - other.Y) < EPSILON2
            && Math.Abs(this.Z - other.Z) < EPSILON2;
    }

    /// <summary>
    /// Creates a rotation represented by a RotationVector: an unit rotation
    /// axis multiplied by the rotation angle.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    public RotationVector(double x, double y, double z)
    {
        this.X = x;
        this.Y = y;
        this.Z = z;
    }
    
    /// <summary>
    /// Creates a rotation represented by a RotationVector: an unit rotation
    /// axis multiplied by the rotation angle.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="angle"></param>
    public RotationVector(double x, double y, double z, double angle) : this(x, y, z, angle, true) { }

    /// <summary>
    /// Protected constructor to bypass normalization of input vector.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="z"></param>
    /// <param name="angle"></param>
    /// <param name="normalize"></param>
    internal RotationVector(double x, double y, double z, double angle, bool normalize)
    {
        if (normalize)
        {
            double len = Vector.Length(x, y, z);

            if (len < EPSILON2)
            {
                this.X = 0;
                this.Y = 0;
                this.Z = 0;
            }
            else
            {
                double m = angle / len;
                this.X = x * m;
                this.Y = y * m;
                this.Z = z * m;
            }

        }
        else
        {
            this.X = x * angle;
            this.Y = y * angle;
            this.Z = z * angle;
        }
    }

    /// <summary>
    /// Returns the length of this vector.
    /// </summary>
    /// <returns></returns>
    public double Length()
    {
        return Math.Sqrt(this.X * this.X + this.Y * this.Y + this.Z * this.Z);
    }

    /// <summary>
    /// Returns the squared length of this vector.
    /// </summary>
    /// <returns></returns>
    public double SqLength()
    {
        return this.X * this.X + this.Y * this.Y + this.Z * this.Z;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public bool IsZero()
    {
        double sqlen = this.SqLength();
        return sqlen < EPSILON2;
    }


    /// <summary>
    /// Returns the unit vector representing the axis of this rotation.
    /// </summary>
    /// <returns></returns>
    public Vector GetVector()
    {
        Vector v = new Vector(this.X, this.Y, this.Z);
        v.Normalize();
        return v;
    }

    /// <summary>
    /// Returns the rotation angle of this rotation (the length of the vector).
    /// Note the rotation angle will always be positive.
    /// </summary>
    /// <returns></returns>
    public double GetAngle()
    {
        return Math.Sqrt(this.X * this.X + this.Y * this.Y + this.Z * this.Z);
    }

    /// <summary>
    /// Is this rotation equivalent to a given one? 
    /// Equivalence is defined as rotations around vectors sharing the same axis (including opposite directions)
    /// and an angle with the same modulated equivalence. This in turn means the same spatial orientation after transformation.
    /// See <see cref="AxisAngle.IsEquivalent(AxisAngle)"/>
    /// </summary>
    /// <param name="rv"></param>
    /// <returns></returns>
    public bool IsEquivalent(RotationVector rv)
    {
        return this.ToAxisAngle().IsEquivalent(rv.ToAxisAngle());
    }

    /// <summary>
    /// Returns an Axis-Angle representation of this rotation.
    /// </summary>
    /// <returns></returns>
    public AxisAngle ToAxisAngle()
    {
        double angle = this.GetAngle();
        if (angle < EPSILON2) return new AxisAngle(0, 0, 0, 0, false);

        double x = this.X / angle,
            y = this.Y / angle,
            z = this.Z / angle;
        return new AxisAngle(x, y, z, angle, false);
    }

    /// <summary>
    /// Returns a Quaternion representation of this rotation.
    /// </summary>
    /// <returns></returns>
    public Quaternion ToQuaternion()
    {
        // This could use some optimization... :)
        return this.ToAxisAngle().ToQuaternion();
    }

    /// <summary>
    /// Returns a Rotation Matrix representation of this rotation.
    /// </summary>
    /// <returns></returns>
    public RotationMatrix ToRotationMatrix()
    {
        return this.ToAxisAngle().ToRotationMatrix();
    }

    /// <summary>
    /// Return a YawPitchRoll representation of this rotation.
    /// </summary>
    /// <returns></returns>
    public YawPitchRoll ToYawPitchRoll()
    {
        return this.ToAxisAngle().ToYawPitchRoll();
    }

    public override string ToString()
    {
        return string.Format(CultureInfo.InvariantCulture, 
            "RotationVector[{0}, {1}, {2}]",
            Math.Round(X, STRING_ROUND_DECIMALS_MM),
            Math.Round(Y, STRING_ROUND_DECIMALS_MM),
            Math.Round(Z, STRING_ROUND_DECIMALS_MM));
    }

}
