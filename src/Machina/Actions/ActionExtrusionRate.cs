﻿namespace Machina;
                                                                                                   
/// <summary>
/// Sets the extrusion rate in 3D printers in mm of filament per mm of lineal travel.
/// </summary>
public class ActionExtrusionRate : Action
{
    public double rate;
    public bool relative;

    public override ActionType Type => ActionType.ExtrusionRate;

    public ActionExtrusionRate(double rate, bool relative) : base()
    {
        this.rate = rate;
        this.relative = relative;
    }

    public override string ToString()
    {
        return this.relative ?
            $"{(this.rate < 0 ? "Decrease" : "Increase")} feed rate by {this.rate} mm/s" :
            $"Set feed rate to {this.rate} mm/s";
    }

    public override string ToInstruction() => null;
    internal override bool Apply(RobotCursor robCur)
    {
        return robCur.ApplyAction(this);
    }
}
