﻿using Machina.Types.Geometry;

namespace Machina;


/// <summary>
/// Arguments for MotionUpdate events.
/// </summary>
public class MotionUpdateEventArgs : MachinaEventArgs
{
    /// <summary>
    /// Last known position of the TCP.
    /// </summary>
    public Vector Position { get; }

    /// <summary>
    /// Last known orientation of the TCP.
    /// </summary>
    public Rotation Rotation { get; }

    /// <summary>
    /// Last known robot axes.
    /// </summary>
    public Joints Axes { get; }

    /// <summary>
    /// Last known roobt external axes.
    /// </summary>
    public ExternalAxes ExternalAxes { get; }

    public MotionUpdateEventArgs(Vector pos, Rotation ori, Joints axes, ExternalAxes extax)
    {
        this.Position = pos;
        this.Rotation = ori;
        this.Axes = axes;
        this.ExternalAxes = extax;
    }

    public override string ToString() => ToJSONString();

    public override string ToJSONString()
    {
        return string.Format("{{\"event\":\"motion-update\",\"pos\":{0},\"ori\":{1},\"quat\":{2},\"axes\":{3},\"extax\":{4},\"conf\":{5}}}",
            this.Position?.ToArrayString() ?? "null",
            this.Rotation?.ToOrientation()?.ToArrayString() ?? "null",
            this.Rotation?.Q.ToArrayString() ?? "null",
            this.Axes?.ToArrayString() ?? "null",
            this.ExternalAxes?.ToArrayString() ?? "null",
            "null");  // placeholder for whenever IK solvers are introduced...
    }
}
