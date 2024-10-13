﻿using System;

using Machina.Types.Geometry;

namespace Machina;
                                                                                                               
/// <summary>
/// An Action representing a combined Translation and Rotation Transformation.
/// </summary>
public class ActionTransformation : Action
{
    public Vector translation;
    public Rotation rotation;
    public bool relative;
    public bool translationFirst;  // for relative transforms, translate or rotate first?

    public override ActionType Type => ActionType.Transformation;

    public ActionTransformation(double x, double y, double z, double vx0, double vx1, double vx2, double vy0,
        double vy1, double vy2, bool relative, bool translationFirst) : base()
    {
        this.translation = new Vector(x, y, z);
        this.rotation = new Orientation(vx0, vx1, vx2, vy0, vy1, vy2);
        this.relative = relative;
        this.translationFirst = translationFirst;
    }

    public ActionTransformation(Vector translation, Rotation rotation, bool relative, bool translationFirst) : base()
    {
        this.translation = new Vector(translation);  // shallow copy
        this.rotation = new Rotation(rotation);  // shallow copy
        this.relative = relative;
        this.translationFirst = translationFirst;
    }

    public override string ToString()
    {
        string str;
        if (relative)
        {
            if (translationFirst)
                str = string.Format("Transform: move {0} mm and rotate {1} deg around {2}", translation, rotation.Angle, rotation.Axis);
            else
                str = string.Format("Transform: rotate {0} deg around {1} and move {2} mm", rotation.Angle, rotation.Axis, translation);
        }
        else
        {
            str = string.Format("Transform: move to {0} mm and rotate to {1}", translation, new Orientation(rotation));
        }
        return str;
    }

    public override string ToInstruction()
    {
        if (this.relative)
        {
            return null;  // @TODO
        }

        Orientation ori = new Orientation(this.rotation);

        return string.Format("TransformTo({0},{1},{2},{3},{4},{5},{6},{7},{8});",
                Math.Round(this.translation.X, Geometry.STRING_ROUND_DECIMALS_MM),
                Math.Round(this.translation.Y, Geometry.STRING_ROUND_DECIMALS_MM),
                Math.Round(this.translation.Z, Geometry.STRING_ROUND_DECIMALS_MM),
                Math.Round(ori.XAxis.X, Geometry.STRING_ROUND_DECIMALS_MM),
                Math.Round(ori.XAxis.Y, Geometry.STRING_ROUND_DECIMALS_MM),
                Math.Round(ori.XAxis.Z, Geometry.STRING_ROUND_DECIMALS_MM),
                Math.Round(ori.YAxis.X, Geometry.STRING_ROUND_DECIMALS_MM),
                Math.Round(ori.YAxis.Y, Geometry.STRING_ROUND_DECIMALS_MM),
                Math.Round(ori.YAxis.Z, Geometry.STRING_ROUND_DECIMALS_MM)
            );
    }
    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
