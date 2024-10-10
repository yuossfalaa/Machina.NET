using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Machina.Types.Data;
using Machina.Types.Geometry;

namespace Machina;

/// <summary>
/// A compiler for ZMorph 3D printers. 
/// </summary>
internal class CompilerZMORPH : Compiler
{
    #region Variables
    internal override Encoding Encoding => Encoding.ASCII;

    internal override char CC => ';';

    // A 'multidimensional' Dict to store permutations of (part, wait) to their corresponding GCode command
    // https://stackoverflow.com/a/15826532/1934487
    private readonly Dictionary<Tuple<RobotPartType, bool>, String> tempToGCode = new Dictionary<Tuple<RobotPartType, bool>, String>()
    {
        { new Tuple<RobotPartType, bool>(RobotPartType.Extruder, true), "M109" },
        { new Tuple<RobotPartType, bool>(RobotPartType.Extruder, false), "M104" },
        { new Tuple<RobotPartType, bool>(RobotPartType.Bed, true), "M190" },
        { new Tuple<RobotPartType, bool>(RobotPartType.Bed, false), "M140" },
        { new Tuple<RobotPartType, bool>(RobotPartType.Chamber, true), "M191" },
        { new Tuple<RobotPartType, bool>(RobotPartType.Chamber, false), "M141" }
    };

    // On extrusion length reset, keep track of the reset point ("G92")
    double extrusionLengthResetPosition = 0;

    // Every n mm of extrusion, reset "E" to zero: "G92 E0"
    double extrusionLengthResetEvery = 10;

    // Made this class members to be able to insert more than one line of code per Action
    // @TODO: make adding several lines of code per Action more programmatic
    List<string> initializationLines,
                 customDeclarationLines,
                 instructionLines,
                 closingLines;
    #endregion

    #region Ctor
    // Base constructor
    internal CompilerZMORPH() : base() { }

    #endregion

    #region Public Methods

    /// <summary>
    /// Creates a textual program representation of a set of Actions using native GCode Laguage.
    /// </summary>
    /// <param name="programName"></param>
    /// <param name="writer"></param>
    /// <param name="block">Use actions in waiting queue or buffer?</param>
    /// <returns></returns>
    public override RobotProgram UNSAFEFullProgramFromBuffer(string programName, RobotCursor writer, bool block, bool inlineTargets, bool humanComments)
    {
        // The program files to be returned
        RobotProgram robotProgram = new RobotProgram(programName, CC);


        addActionString = humanComments;

        // Which pending Actions are used for this program?
        // Copy them without flushing the buffer.
        List<Action> actions = block ?
            writer.actionBuffer.GetBlockPending(false) :
            writer.actionBuffer.GetAllPending(false);

        // CODE LINES GENERATION
        // TARGETS AND INSTRUCTIONS
        this.initializationLines = new List<string>();
        this.customDeclarationLines = new List<string>();
        this.instructionLines = new List<string>();
        this.closingLines = new List<string>();

        this.initializationLines.AddRange(GenerateDisclaimerHeader(programName));

        GenrateData(writer, actions);
        List<string> module = MakeProgrameAssembly();

        RobotProgramFile mainFile = new(programName, "gcode", Encoding, CC);
        mainFile.SetContent(module);
        robotProgram.Add(mainFile);

        return robotProgram;
    }

    #endregion

    #region Utilities
    internal bool GenerateInstructionDeclaration(Action action, RobotCursor cursor, out string declaration)
    {
        string dec = getDec(action, cursor);

        dec = addTrailingComments(action, dec);

        declaration = dec;

        return dec != null;
    }

    /// <summary>
    /// Returns a simple XYZ position.
    /// </summary>
    /// <param name="cursor"></param>
    /// <returns></returns>
    internal static string GetPositionTargetValue(RobotCursor cursor)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "X{0} Y{1} Z{2}",
            Math.Round(cursor.position.X, Geometry.STRING_ROUND_DECIMALS_MM),
            Math.Round(cursor.position.Y, Geometry.STRING_ROUND_DECIMALS_MM),
            Math.Round(cursor.position.Z, Geometry.STRING_ROUND_DECIMALS_MM));
    }

    /// <summary>
    /// Computes how much the cursor has moved in this action, and returns how much
    /// filament it should extrude based on extrusion rate.
    /// </summary>
    /// <param name="cursor"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    internal string GetExtrusionTargetValue(RobotCursor cursor)
    {
        double len = cursor.extrudedLength - this.extrusionLengthResetPosition;

        // If extruded over the limit, reset extrude position and start over
        if (len > extrusionLengthResetEvery)
        {
            this.instructionLines.Add($"{CC} Homing extrusion length after {cursor.prevExtrudedLength - this.extrusionLengthResetPosition} mm ({this.extrusionLengthResetEvery} mm limit)");
            this.instructionLines.Add($"G92 E0.0000");
            this.extrusionLengthResetPosition = cursor.prevExtrudedLength;
            len = cursor.extrudedLength - this.extrusionLengthResetPosition;
        }

        return string.Format(CultureInfo.InvariantCulture,
            "E{0}",
            Math.Round(len, 5));
    }

    /// <summary>
    /// Dumps a bunch of initialization boilerplate
    /// </summary>
    /// <param name="cursor"></param>
    internal void StartCodeBoilerplate(RobotCursor cursor)
    {
        // SOME INITIAL BOILERPLATE TO HEAT UP THE PRINTER, CALIBRATE IT, ETC.
        // ZMorph boilerplate
        // HEATUP -> For the user, may not want to use the printer as printer...
        //instructionLines.Add("M140 S60");                // set bed temp and move on to next inst
        //instructionLines.Add("M109 S200");               // set extruder bed temp and wait till heat up
        //instructionLines.Add("M190 S60");                // set bed temp and wait till heat up
        // HOMING
        this.instructionLines.Add("G91");                     // set rel motion
        this.instructionLines.Add("G1 Z1.000 F200.000");      // move up 1mm and accelerate printhead to 200 mm/min
        this.instructionLines.Add("G90");                     // set absolute positioning
        this.instructionLines.Add("G28 X0.000 Y0.00");        // home XY axes
        this.instructionLines.Add("G1 X117.500 Y125.000 F8000.000");  // move to bed center for Z homing
        this.instructionLines.Add("G28 Z0.000");              // home Z
        this.instructionLines.Add("G92 E0.00000");            // set filament position to zero

        // Machina bolierplate
        this.instructionLines.Add("M82");                     // set extruder to absolute mode (this is actually ZMorph, but useful here
        this.instructionLines.Add(string.Format(CultureInfo.InvariantCulture,
            "G1 F{0}",
            Math.Round(cursor.speed * 60.0, Geometry.STRING_ROUND_DECIMALS_MM)));  // initialize feed speed to the writer's state
    }

    /// <summary>
    /// Dumps a bunch of termination boilerplate
    /// </summary>
    /// <param name="cursor"></param>
    internal void EndCodeBoilerplate(RobotCursor cursor)
    {
        // END THE PROGRAM AND LEAVE THE PRINTER READY
        // ZMorph boilerplate
        this.instructionLines.Add("G92 E0.0000");
        this.instructionLines.Add("G91");
        this.instructionLines.Add("G1 E-3.00000 F1800.000");
        this.instructionLines.Add("G90");
        this.instructionLines.Add("G92 E0.00000");
        this.instructionLines.Add("G1 X117.500 Y220.000 Z30.581 F300.000");

        this.instructionLines.Add("T0");         // choose tool 0: is this for multihead? 
        this.instructionLines.Add("M104 S0");    // set extruder temp and move on
        this.instructionLines.Add("T1");         // choose tool 1
        this.instructionLines.Add("M104 S0");    // ibid
        this.instructionLines.Add("M140 S0");    // set bed temp and move on
        this.instructionLines.Add("M106 S0");    // fan speed 0 (off)
        this.instructionLines.Add("M84");        // stop idle hold (?)
        this.instructionLines.Add("M220 S100");  // set speed factor override percentage 
    }

    #endregion

    #region Private Methods
    private List<string> MakeProgrameAssembly()
    {

        // PROGRAM ASSEMBLY
        // Initialize a module list
        List<string> module = new List<string>();

        // Initializations
        if (this.initializationLines.Count != 0)
        {
            module.AddRange(this.initializationLines);
            module.Add("");
        }

        // Custom declarations
        if (this.customDeclarationLines.Count != 0)
        {
            module.AddRange(this.customDeclarationLines);
            module.Add("");
        }

        // MAIN PROCEDURE
        // Instructions
        if (this.instructionLines.Count != 0)
        {
            module.AddRange(this.instructionLines);
            module.Add("");
        }

        // Wrapping up
        if (this.closingLines.Count != 0)
        {
            module.AddRange(this.closingLines);
            module.Add("");
        }

        return module;
    }

    private void GenrateData(RobotCursor writer, List<Action> actions)
    {
        // DATA GENERATION
        // Use the write RobotCursor to generate the data
        string line = null;
        foreach (Action a in actions)
        {
            // Move writerCursor to this action state
            writer.ApplyNextAction();  // for the buffer to correctly manage them

            if (a.Type == ActionType.CustomCode && (a as ActionCustomCode).isDeclaration)
            {
                customDeclarationLines.Add((a as ActionCustomCode).statement);
            }

            // GCode is super straightforward, so no need to pre-declare anything
            if (GenerateInstructionDeclaration(a, writer, out line))
            {
                this.instructionLines.Add(line);
            }
        }
    }

    private string addTrailingComments(Action action, string dec)
    {
        // Add trailing comments or ids if speficied
        if (addActionString && action.Type != ActionType.Comment)
        {
            dec = string.Format("{0}  {1} [{2}]",
                dec,
                CC,
                action.ToString());
        }
        else if (addActionID)
        {
            dec = string.Format("{0}  {1} [{2}]",
                dec,
                CC,
                action.Id);
        }

        return dec;
    }

    #endregion

    #region Generate Dec
    private string getDec(Action action, RobotCursor cursor)
    {
        return action.Type switch
        {
            ActionType.Speed => GenerateDecForSpeedAction(cursor),
            ActionType.Translation or ActionType.Transformation => GenerateDecForTranslation_TransformationAction(cursor),
            // Only available in MakerBot? http://reprap.org/wiki/G-code#M70:_Display_message
            ActionType.Message => GenerateDecForMessageAction(action),
            // In GCode, this is called "Dwell"
            ActionType.Wait => GenerateDecForWaitAction(action),
            ActionType.Comment => GenerateDecForCommentAction(action),
            // Untested, but oh well...
            // http://reprap.org/wiki/G-code#M42:_Switch_I.2FO_pin
            ActionType.IODigital => GenerateDecForIODigitalAction(action),
            ActionType.IOAnalog => GenerateDecForIOAnalogAction(action),
            ActionType.Temperature => GenerateDecForTemperatureAction(action, cursor),
            ActionType.Extrusion or ActionType.ExtrusionRate => GenerateDecForExtrusionRateOrExtrusionAction(action),
            ActionType.Initialization => GenerateDecForInitializationAction(action, cursor),
            ActionType.CustomCode => GenerateDecForCustomCodeAction(action),
            // If action wasn't implemented before, then it doesn't apply to this device
            _ => $"{CC} ACTION \"{action}\" NOT APPLICABLE TO THIS DEVICE",
        };
    }

    private static string GenerateDecForCustomCodeAction(Action action)
    {
        ActionCustomCode acc = action as ActionCustomCode;
        if (!acc.isDeclaration)
        {
            return $"{acc.statement}";
        }
        return null;
    }

    private string GenerateDecForInitializationAction(Action action, RobotCursor cursor)
    {
        ActionInitialization ai = (ActionInitialization)action;
        if (ai.initialize)
            StartCodeBoilerplate(cursor);
        else
            EndCodeBoilerplate(cursor);

        return null;
    }

    private string GenerateDecForExtrusionRateOrExtrusionAction(Action action)
    {
        return $"{CC} {action}";  // has no direct G-code, simply annotate it as a comment
    }

    private string GenerateDecForTemperatureAction(Action action, RobotCursor cursor)
    {
        ActionTemperature at = (ActionTemperature)action;
        return string.Format(CultureInfo.InvariantCulture,
            "{0} S{1}",
            tempToGCode[new Tuple<RobotPartType, bool>(at.robotPart, at.wait)],
            Math.Round(cursor.partTemperature[at.robotPart], Geometry.STRING_ROUND_DECIMALS_TEMPERATURE));
    }

    private string GenerateDecForIOAnalogAction(Action action)
    {
        ActionIOAnalog aioa = (ActionIOAnalog)action;
        if (!aioa.isDigit)
        {
            return $"{CC} ERROR on \"{aioa}\": only integer pin names allowed";
        }
        else if (aioa.value < 0 || aioa.value > 255)
        {
            return $"{CC} ERROR on \"{aioa.ToString()}\": value out of range [0..255]";
        }
        else
        {
            return string.Format(CultureInfo.InvariantCulture,
                "M42 P{0} S{1}",
                aioa.pinNum,
                Math.Round(aioa.value, 0));
        }
    }

    private string GenerateDecForIODigitalAction(Action action)
    {
        ActionIODigital aiod = (ActionIODigital)action;
        if (!aiod.isDigit)
        {
            return $"{CC} ERROR on \"{aiod}\": only integer pin names allowed";
        }
        else
        {
            return $"M42 P{aiod.pinNum} S{(aiod.on ? "1" : "0")}";
        }
    }

    private string GenerateDecForCommentAction(Action action)
    {
        ActionComment ac = (ActionComment)action;
        return string.Format("{0} {1}",
            CC,
            ac.comment);
    }

    private static string GenerateDecForWaitAction(Action action)
    {
        ActionWait aw = (ActionWait)action;
        return string.Format(CultureInfo.InvariantCulture,
            "G4 P{0}",
            aw.millis);
    }

    private static string GenerateDecForMessageAction(Action action)
    {
        ActionMessage am = (ActionMessage)action;
        return string.Format("M70 P1000 ({0})",
            am.message);
    }

    private string GenerateDecForTranslation_TransformationAction(RobotCursor cursor)
    {
        return string.Format("G1 {0}{1}",
                        GetPositionTargetValue(cursor),
                        cursor.isExtruding ? " " + GetExtrusionTargetValue(cursor) : "");
    }

    private static string GenerateDecForSpeedAction(RobotCursor cursor)
    {
        return string.Format(CultureInfo.InvariantCulture,
                            "G1 F{0}",
                            Math.Round(60.0 * cursor.speed, Geometry.STRING_ROUND_DECIMALS_MM));
    }
    #endregion
}
