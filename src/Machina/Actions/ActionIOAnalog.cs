﻿using System;

namespace Machina
{                                                              
    /// <summary>
    /// Writes a value to analog pin #.
    /// </summary>
    public class ActionIOAnalog : Action
    {
        public string pinName;
        public double value;
        public bool isDigit = false;
        public int pinNum = 0;
        public bool isToolPin = false;

        public override ActionType Type => ActionType.IOAnalog;

        public ActionIOAnalog(string pin, double value, bool toolPin) : base()
        {
            this.pinName = pin;
            this.value = value;
            this.isDigit = Int32.TryParse(this.pinName, out this.pinNum);
            this.isToolPin = toolPin;
        }

        public override string ToString()
        {
            return $"Set {(this.isToolPin ? "tool " : "")}analog IO {(this.isDigit ? this.pinNum.ToString() : "\"" + this.pinName + "\"")} to {this.value}";
        }

        public override string ToInstruction()
        {
            return $"WriteAnalog({(this.isDigit ? this.pinNum.ToString() : "\"" + this.pinName + "\"")},{this.value}{(this.isToolPin ? "," + this.isToolPin : "")});";
        }
    }
}
