using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Machina.Types.Geometry;
using Machina.Types.Data;

namespace Machina;

/// <summary>
/// A compiler for ABB 6-axis industrial robotic arms.
/// </summary>
internal class CompilerABB : Compiler
{
    #region Variables
    internal override Encoding Encoding => Encoding.ASCII;

    internal override char CC => '!';

    /// <summary>
    /// A Set of RAPID's predefined zone values. 
    /// </summary>
    private static HashSet<int> PredefinedZones = new HashSet<int>()
    {
        0, 1, 5, 10, 15, 20, 30, 40, 50, 60, 80, 100, 150, 200
    };


    // Amount of different VZ types
    private Dictionary<double, string> velNames, velDecs, zoneNames, zoneDecs;
    private Dictionary<double, bool> zonePredef;

    // TOOL DECLARATIONS
    private Dictionary<Tool, string> toolNames, toolDecs;

    List<string> introLines, velocityLines, zoneLines, toolLines, customLines, variableLines, instructionLines;

    #endregion

    #region ctor
    internal CompilerABB() : base() { }
    #endregion

    #region Public Methods
    /// <summary>
    /// Creates a textual program representation of a set of Actions using native RAPID Laguage.
    /// WARNING: this method is EXTREMELY UNSAFE; it performs no IK calculations, assigns default [0,0,0,0] 
    /// robot configuration and assumes the robot controller will figure out the correct one.
    /// </summary>
    /// <param name="programName"></param>
    /// <param name="writer"></param>
    /// <param name="block">Use actions in waiting queue or buffer?</param>
    /// <returns></returns>
    //public override List<string> UNSAFEProgramFromBuffer(string programName, RobotCursor writePointer, bool block)
    public override RobotProgram UNSAFEFullProgramFromBuffer(string programName, RobotCursor writer, bool block, bool inlineTargets, bool humanComments)
    {
        // The program files to be returned
        RobotProgram robotProgram = new(programName, CC);

        // HEADER file
        RobotProgramFile pgfFile = new RobotProgramFile(programName, "pgf", Encoding, CC);

        List<string> header =
        [
            "<?xml version=\"1.0\" encoding=\"ISO-8859-1\" ?>",
            "<Program>",
            $"    <Module>{programName}.mod</Module>",
            "</Program>",
        ];

        pgfFile.SetContent(header);
        robotProgram.Add(pgfFile);

        // PROGRAM FILE
        addActionString = humanComments;

        // Which pending Actions are used for this program?
        // Copy them without flushing the buffer.
        List<Action> actions = block ?
            writer.actionBuffer.GetBlockPending(false) :
            writer.actionBuffer.GetAllPending(false);


        // CODE LINES GENERATION
        InitilizeAllVariables();

        // DATA GENERATION
        bool usesIO = GenrateData(ref writer, inlineTargets, actions);


        // Generate V+Z+T
        GenerateVZT();

        // Generate IO warning
        if (usesIO)
        {
            introLines.Add(string.Format("  {0} NOTE: your program is interfacing with the robot's IOs." +
                "Make sure to properly configure their names/properties through system preferences in the ABB robot controller.",
                CC));
        }
        List<string> module = GetModuelList(programName);

        RobotProgramFile modFile = new(programName, "mod", Encoding, CC);
        modFile.SetContent(module);
        robotProgram.Add(modFile);

        return robotProgram;
    }
    #endregion

    #region Utilities
    internal static bool GenerateVariableDeclaration(Action action, RobotCursor cursor, int id, out List<string> declarations)
    {
        declarations = new List<string>();
        switch (action.Type)
        {
            case ActionType.Translation:
            case ActionType.Rotation:
            case ActionType.Transformation:
                declarations.Add(string.Format("  CONST robtarget target{0} := {1};", id, GetRobTargetValue(cursor)));
                break;

            case ActionType.Axes:
                declarations.Add(string.Format("  CONST jointtarget target{0} := {1};", id, GetJointTargetValue(cursor)));
                break;

            case ActionType.ArcMotion:
                ActionArcMotion aam = action as ActionArcMotion;
                cursor.ComputeThroughPlane(aam, out Vector throughPos, out Rotation throughRot);
                declarations.Add(string.Format("  CONST robtarget target{0} := {1};", id, GetRobTargetValue(cursor, throughPos, throughRot)));
                declarations.Add(string.Format("  CONST robtarget target{0} := {1};", id + 1, GetRobTargetValue(cursor)));
                break;
        }

        return declarations.Count != 0;
    }

    internal bool GenerateInstructionDeclarationFromVariable(
        Action action,
        RobotCursor cursor,
        int id,
        out string declaration)
    {
        string dec = GenerateDecFromVariable(action, cursor, id);

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

        declaration = dec;
        return dec != null;
    }

    internal bool GenerateInstructionDeclaration(
        Action action,
        RobotCursor cursor,
        out string declaration)
    {
        string dec = GenerateDec(action, cursor);

        if (addActionString && action.Type != ActionType.Comment)
        {
            dec = string.Format("{0}{1}  {2} [{3}]",
                dec,
                dec == null ? "  " : "",  // add indentation to align with code
                CC,
                action.ToString());
        }
        else if (addActionID)
        {
            dec = string.Format("{0}{1}  {2} [{3}]",
                dec,
                dec == null ? "  " : "",  // add indentation to align with code
                CC,
                action.Id);
        }

        declaration = dec;
        return dec != null;
    }

    /// <summary>
    /// Returns an RAPID robtarget representation of the current state of the cursor.
    /// WARNING: this method is UNSAFE; it performs no IK calculations, assigns default [0,0,0,0] 
    /// robot configuration and assumes the robot controller will figure out the correct one.
    /// </summary>
    /// <returns></returns>
    static internal string GetRobTargetValue(RobotCursor cursor)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "[{0}, {1}, {2}, {3}]",
            cursor.position.ToString(false),
            cursor.rotation.Q.ToString(false),
            "[0,0,0,0]",  // no IK at this moment
            GetExternalJointsRobTargetValue(cursor));
    }

    /// <summary>
    /// Returns an RAPID robtarget representation of the SPECIFIED POS/ROT, not the current cursor state.
    /// WARNING: this method is UNSAFE; it performs no IK calculations, assigns default [0,0,0,0] 
    /// robot configuration and assumes the robot controller will figure out the correct one.
    /// </summary>
    /// <returns></returns>
    static internal string GetRobTargetValue(RobotCursor cursor, Vector position, Rotation rotation)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "[{0}, {1}, {2}, {3}]",
            position.ToString(false),
            rotation.Q.ToString(false),
            "[0,0,0,0]",  // no IK at this moment
            GetExternalJointsRobTargetValue(cursor));
    }

    /// <summary>
    /// Returns an RAPID jointtarget representation of the current state of the cursor.
    /// </summary>
    /// <returns></returns>
    static internal string GetJointTargetValue(RobotCursor cursor)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "[{0}, {1}]",
            cursor.axes,
            GetExternalJointsJointTargetValue(cursor));
    }

    /// <summary>
    /// Returns a RAPID representation of cursor speed.
    /// </summary>
    /// <param name="speed"></param>
    /// <returns></returns>
    static internal string GetSpeedValue(RobotCursor cursor)
    {
        // ABB format: [TCP linear speed in mm/s, TCP reorientation speed in deg/s, linear external axis speed in mm/s, rotational external axis speed in deg/s]
        // Default linear speeddata are [vel, 500, 5000, 1000], which feels like a lot. 
        // Just use the speed data as linear or rotational value, and stay safe. 
        string vel = Math.Round(cursor.speed, Geometry.STRING_ROUND_DECIMALS_MM).ToString(CultureInfo.InvariantCulture);

        return string.Format("[{0},{1},{2},{3}]", vel, vel, vel, vel);

        //// Default speed declarations in ABB always use 500 deg/s as rot speed, but it feels too fast (and scary). 
        //// Using either rotationSpeed value or the same value as lin motion here.
        //return string.Format("[{0},{1},{2},{3}]", 
        //    cursor.speed, 
        //    cursor.rotationSpeed > Geometry.EPSILON2 ? cursor.rotationSpeed : cursor.speed, 
        //    5000, 
        //    1000);

    }

    /// <summary>
    /// Returns a RAPID representatiton of cursor zone.
    /// </summary>
    /// <param name="cursor"></param>
    /// <returns></returns>
    static internal string GetZoneValue(RobotCursor cursor)
    {
        if (cursor.precision < Geometry.EPSILON)
            return "fine";

        // Following conventions for default RAPID zones.
        double high = 1.5 * cursor.precision;
        double low = 0.10 * cursor.precision;
        return string.Format(CultureInfo.InvariantCulture,
            "[FALSE,{0},{1},{2},{3},{4},{5}]",
            cursor.precision, high, high, low, high, low);
    }

    /// <summary>
    /// Returns a RAPID representation of a Tool object.
    /// </summary>
    /// <param name="cursor"></param>
    /// <returns></returns>
    static internal string GetToolValue(Tool tool)  
    {
        ArgumentNullException.ThrowIfNull(tool);

        return string.Format(CultureInfo.InvariantCulture,
            "[TRUE, [{0},{1}], [{2},{3},{4},0,0,0]]",
            tool.TCPPosition,
            tool.TCPOrientation.Q.ToString(false),
            tool.Weight,
            tool.CenterOfGravity,
            "[1,0,0,0]");  // no internial axes by default
    }


    /// <summary>
    /// Gets the cursors extax representation for a Cartesian target.
    /// </summary>
    /// <param name="cursor"></param>
    /// <returns></returns>
    static internal string GetExternalJointsRobTargetValue(RobotCursor cursor)
    {
        // If user initializes arm-angle, this extax is just the arm-angle value
        if (cursor.armAngle != null)
        {
            return GetExternalAxesValue(new ExternalAxes(cursor.armAngle));
        }

        // Otherwise, use externalAxes
        return GetExternalAxesValue(cursor.externalAxesCartesian);
    }

    static internal string GetExternalJointsJointTargetValue(RobotCursor cursor) =>
            GetExternalAxesValue(cursor.externalAxesJoints);

    /// <summary>
    /// Gets the RAPID extax representation from an ExternalAxes object.
    /// </summary>
    /// <param name="extax"></param>
    /// <returns></returns>
    static internal string GetExternalAxesValue(ExternalAxes extax)
    {
        if (extax == null)
        {
            return "[9E9,9E9,9E9,9E9,9E9,9E9]";
        }

        string extj = "[";
        double? val;
        for (int i = 0; i < extax.Length; i++)
        {
            val = extax[i];
            extj += (val == null) ? "9E9" : Math.Round((double)val, Geometry.STRING_ROUND_DECIMALS_MM).ToString(CultureInfo.InvariantCulture);
            if (i < extax.Length - 1)
            {
                extj += ",";
            }
        }
        extj += "]";
        return extj;
    }

    static internal string SafeDoubleName(double value) => Math.Round(value, Geometry.STRING_ROUND_DECIMALS_MM).ToString(CultureInfo.InvariantCulture).Replace('.', '_');

    #endregion

    #region Generate Dec
    private string GenerateDec(Action action, RobotCursor cursor)
    {
        return action.Type switch
        {
            ActionType.Acceleration => GenerateDecAccelerationAction(cursor),
            // @TODO: push/pop management should be done PROGRAMMATICALLY, not this CHAPUZa...
            ActionType.PushPop => GenerateDecPushPopAction(action, cursor),
            ActionType.Translation or ActionType.Rotation or ActionType.Transformation => GenerateDecTRTActions(cursor),
            ActionType.ArcMotion => GenerateDecArcMotionAction(action, cursor),
            ActionType.Axes => GenerateDecAxesAction(cursor),
            ActionType.Message => GenerateDecMessageAction(action),
            ActionType.Wait => GenerateDecWaitAction(action),
            ActionType.Comment => GenerateDecCommentAction(action),
            ActionType.DefineTool => GenerateDecDefineTool(action),
            ActionType.AttachTool => GenerateDecAttachTool(action),
            ActionType.DetachTool => GenerateDecDetachTool(action),
            ActionType.IODigital => GenerateDecIODigital(action),
            ActionType.IOAnalog => GenerateDecIOAnalog(action),
            ActionType.CustomCode => GenerateDecCustomCode(action),
            _ => null,
        };
    }

    private static string GenerateDecCustomCode(Action action)
    {
        ActionCustomCode acc = action as ActionCustomCode;
        if (!acc.isDeclaration)
        {
            return "    " + acc.statement;
        }
        return null;
    }

    private static string GenerateDecIOAnalog(Action action)
    {
        ActionIOAnalog aioa = (ActionIOAnalog)action;
        return $"    SetAO {aioa.pinName}, {aioa.value};";
    }

    private static string GenerateDecIODigital(Action action)
    {
        ActionIODigital aiod = (ActionIODigital)action;
        return $"    SetDO {aiod.pinName}, {(aiod.on ? "1" : "0")};";
    }

    private string GenerateDecDetachTool(Action action)
    {
        ActionDetachTool ad = (ActionDetachTool)action;
        return string.Format("    {0} All tools detached",  // this action has no actual RAPID instruction, just add a comment
            CC);
    }

    private string GenerateDecAttachTool(Action action)
    {
        ActionAttachTool aa = (ActionAttachTool)action;
        return string.Format("    {0} Tool \"{1}\" attached",  // this action has no actual RAPID instruction, just add a comment
            CC,
            aa.toolName);
    }

    private string GenerateDecDefineTool(Action action)
    {
        ActionDefineTool adt = action as ActionDefineTool;
        return string.Format("    {0} Tool \"{1}\" defined",  // this action has no actual RAPID instruction, just add a comment
            CC,
            adt.tool.name);
    }

    private string GenerateDecCommentAction(Action action)
    {
        ActionComment ac = (ActionComment)action;
        return string.Format("    {0} {1}",
            CC,
            ac.comment);
    }

    private static string GenerateDecWaitAction(Action action)
    {
        ActionWait aw = (ActionWait)action;
        return string.Format(CultureInfo.InvariantCulture,
            "    WaitTime {0};",
            0.001 * aw.millis);
    }

    private static string GenerateDecMessageAction(Action action)
    {
        ActionMessage am = (ActionMessage)action;
        return string.Format("    TPWrite \"{0}\";",
            am.message.Length <= 80 ?
                am.message :
                am.message.Substring(0, 80));  // ABB strings can only be 80 chars long
    }

    private string GenerateDecAxesAction(RobotCursor cursor)
    {
        return string.Format("    MoveAbsJ {0}, {1}, {2}, {3}\\{4};",
            GetJointTargetValue(cursor),
            velNames[cursor.speed],
            zoneNames[cursor.precision],
            cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
            "WObj:=WObj0");
    }

    private string GenerateDecArcMotionAction(Action action, RobotCursor cursor)
    {
        ActionArcMotion aam = action as ActionArcMotion;
        cursor.ComputeThroughPlane(aam, out Vector throughPos, out Rotation throughRot);
        return string.Format("    MoveC {0}, {1}, {2}, {3}, {4}\\{5};",
            GetRobTargetValue(cursor, throughPos, throughRot),
            GetRobTargetValue(cursor),
            velNames[cursor.speed],
            zoneNames[cursor.precision],
            cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
            "WObj:=WObj0");
    }

    private string GenerateDecTRTActions(RobotCursor cursor)
    {
        return string.Format("    {0} {1}, {2}, {3}, {4}\\{5};",
            cursor.motionType == MotionType.Joint ? "MoveJ" : "MoveL",
            GetRobTargetValue(cursor),
            velNames[cursor.speed],
            zoneNames[cursor.precision],
            cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
            "WObj:=WObj0");
    }

    private static string GenerateDecPushPopAction(Action action, RobotCursor cursor)
    {
        // Find if there was a change in acceleration, and set the corresponsing instruction...
        ActionPushPop app = action as ActionPushPop;
        if (app.push) return null;  // only necessary for pops
        if (Math.Abs(cursor.acceleration - cursor.settingsBuffer.SettingsBeforeLastPop.Acceleration) < Geometry.EPSILON) return null;  // no change
                                                                                                                                       // If here, there was a change, so...
        bool zeroAcc = cursor.acceleration < Geometry.EPSILON;
        return string.Format("    WorldAccLim {0};",
                zeroAcc
                    ? "\\Off"
                    : "\\On := " + Math.Round(0.001 * cursor.acceleration,
            Geometry.STRING_ROUND_DECIMALS_M)).ToString(CultureInfo.InvariantCulture);
    }

    private static string GenerateDecAccelerationAction(RobotCursor cursor)
    {
        bool zero = cursor.acceleration < Geometry.EPSILON;
        return string.Format("    WorldAccLim {0};",
            zero ? "\\Off" : "\\On := " + Math.Round(0.001 * cursor.acceleration, Geometry.STRING_ROUND_DECIMALS_M)).ToString(CultureInfo.InvariantCulture);
    }
    #endregion

    #region Generate Dec From Variable

    private string GenerateDecFromVariable(Action action, RobotCursor cursor, int id)
    {
        return action.Type switch
        {
            ActionType.Acceleration => GenerateDecFromVarAccelerationAction(cursor),
            // @TODO: push/pop management should be done PROGRAMMATICALLY, not this CHAPUZA...
            ActionType.PushPop => GenerateDecFromVarPushPopAction(action, cursor),
            ActionType.Translation or ActionType.Rotation or ActionType.Transformation => GenerateDecFromVarTRTAction(cursor, id),
            ActionType.ArcMotion => GenerateDecFromVarArcMotion(cursor, id),
            ActionType.Axes => GenerateDecFromVarAxes(cursor, id),
            ActionType.Message => GenerateDecFromVarMessageAction(action),
            ActionType.Wait => GenerateDecFromVarWaitAction(action),
            ActionType.Comment => GenerateDecFromVarCommentAction(action),
            ActionType.DefineTool => GenerateDecFromVarDefineTool(action),
            ActionType.AttachTool => GenerateDecFromVarAttachTool(action),
            ActionType.DetachTool => GenerateDecFromVarDetachTool(action),
            ActionType.IODigital => GenerateDecFromVarIODigital(action),
            ActionType.IOAnalog => GenerateDecFromVarIOAnalog(action),
            ActionType.CustomCode => GenerateDecFromVarCustomCode(action),
            _ => null,
        };
    }

    private static string GenerateDecFromVarCustomCode(Action action)
    {
        ActionCustomCode acc = action as ActionCustomCode;
        if (!acc.isDeclaration)
        {
            return "    " + acc.statement;
        }
        return null;
    }

    private static string GenerateDecFromVarIOAnalog(Action action)
    {
        ActionIOAnalog aioa = (ActionIOAnalog)action;
        return $"    SetAO {aioa.pinName}, {aioa.value};";
    }

    private static string GenerateDecFromVarIODigital(Action action)
    {
        ActionIODigital aiod = (ActionIODigital)action;
        return $"    SetDO {aiod.pinName}, {(aiod.on ? "1" : "0")};";
    }

    private string GenerateDecFromVarDetachTool(Action action)
    {
        ActionDetachTool ad = (ActionDetachTool)action;
        return string.Format("    {0} All tools detached",  // this action has no actual RAPID instruction, just add a comment
            CC);
    }

    private string GenerateDecFromVarAttachTool(Action action)
    {
        ActionAttachTool aa = (ActionAttachTool)action;
        return string.Format("    {0} Tool \"{1}\" attached",  // this action has no actual RAPID instruction, just add a comment
            CC,
            aa.toolName);
    }

    private string GenerateDecFromVarDefineTool(Action action)
    {
        ActionDefineTool adt = action as ActionDefineTool;
        return string.Format("    {0} Tool \"{1}\" defined",  // this action has no actual RAPID instruction, just add a comment
            CC,
            adt.tool.name);
    }

    private string GenerateDecFromVarCommentAction(Action action)
    {
        ActionComment ac = (ActionComment)action;
        return string.Format("    {0} {1}",
            CC,
            ac.comment);
    }

    private static string GenerateDecFromVarWaitAction(Action action)
    {
        ActionWait aw = (ActionWait)action;
        return string.Format(CultureInfo.InvariantCulture,
            "    WaitTime {0};",
            0.001 * aw.millis);
    }

    private static string GenerateDecFromVarMessageAction(Action action)
    {
        ActionMessage am = (ActionMessage)action;
        return string.Format("    TPWrite \"{0}\";",
            am.message.Length <= 80 ?
                am.message :
                am.message.Substring(0, 80));  // ABB strings can only be 80 chars long
    }

    private string GenerateDecFromVarAxes(RobotCursor cursor, int id)
    {
        return string.Format("    MoveAbsJ target{0}, {1}, {2}, {3}\\{4};",
                            id,
                            velNames[cursor.speed],
                            zoneNames[cursor.precision],
                            cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
                            "WObj:=WObj0");
    }

    private string GenerateDecFromVarArcMotion(RobotCursor cursor, int id)
    {
        return string.Format("    MoveC target{0}, target{1}, {2}, {3}, {4}\\{5};",
            id,
            id + 1,
            velNames[cursor.speed],
            zoneNames[cursor.precision],
            cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
            "WObj:=WObj0");
    }

    private string GenerateDecFromVarTRTAction(RobotCursor cursor, int id)
    {
        return string.Format("    {0} target{1}, {2}, {3}, {4}\\{5};",
            cursor.motionType == MotionType.Joint ? "MoveJ" : "MoveL",
            id,
            velNames[cursor.speed],
            zoneNames[cursor.precision],
            cursor.tool == null ? "Tool0" : toolNames[cursor.tool],
            "WObj:=WObj0");
    }

    private static string GenerateDecFromVarPushPopAction(Action action, RobotCursor cursor)
    {
        // Find if there was a change in acceleration, and set the corresponsing instruction...
        ActionPushPop app = action as ActionPushPop;
        if (app.push) return null;  // only necessary for pops
        if (Math.Abs(cursor.acceleration - cursor.settingsBuffer.SettingsBeforeLastPop.Acceleration) < Geometry.EPSILON) return null;  // no change
                                                                                                                                     // If here, there was a change, so...
        bool zeroAcc = cursor.acceleration < Geometry.EPSILON;
        return string.Format("    WorldAccLim {0};",
            zeroAcc ? "\\Off" : "\\On := " + Math.Round(0.001 * cursor.acceleration, Geometry.STRING_ROUND_DECIMALS_M)).ToString(CultureInfo.InvariantCulture);
    }

    private static string GenerateDecFromVarAccelerationAction(RobotCursor cursor)
    {
        bool zero = cursor.acceleration < Geometry.EPSILON;
        return string.Format("    WorldAccLim {0};",
            zero ? "\\Off" : "\\On := " + Math.Round(0.001 * cursor.acceleration, Geometry.STRING_ROUND_DECIMALS_M)).ToString(CultureInfo.InvariantCulture);
    }

    #endregion

    #region Private Methods
    private List<string> GetModuelList(string programName)
    {

        // PROGRAM ASSEMBLY
        // Initialize a module list
        List<string> module =
        [
            // MODULE HEADER
            "MODULE " + programName,
            "",
            // Banner (must go after MODULE, or will yield RAPID syntax errors)
            .. GenerateDisclaimerHeader(programName),
            "",
        ];

        // INTRO LINES
        if (introLines.Count != 0)
        {
            module.AddRange(introLines);
            module.Add("");
        }

        // VARIABLE DECLARATIONS
        // Tools
        if (toolLines.Count != 0)
        {
            module.AddRange(toolLines);
            module.Add("");
        }

        // Velocities
        if (velocityLines.Count != 0)
        {
            module.AddRange(velocityLines);
            module.Add("");
        }

        // Zones
        if (zoneLines.Count != 0)
        {
            module.AddRange(zoneLines);
            module.Add("");
        }

        // Custom code
        if (customLines.Count != 0)
        {
            module.AddRange(customLines);
            module.Add("");
        }

        // Targets
        if (variableLines.Count != 0)
        {
            module.AddRange(variableLines);
            module.Add("");
        }

        // MAIN PROCEDURE
        module.Add("  PROC main()");
        module.Add(@"    ConfJ \Off;");
        module.Add(@"    ConfL \Off;");
        module.Add("");

        // Instructions
        if (instructionLines.Count != 0)
        {
            module.AddRange(instructionLines);
            module.Add("");
        }

        module.Add("  ENDPROC");
        module.Add("");

        // MODULE FOOTER
        module.Add("ENDMODULE");
        return module;
    }

    private void GenerateVZT()
    {
        foreach (Tool t in toolNames.Keys)
        {
            toolLines.Add(string.Format(CultureInfo.InvariantCulture, "  PERS tooldata {0} := {1};", toolNames[t], toolDecs[t]));
        }
        foreach (var v in velNames.Keys)
        {
            velocityLines.Add(string.Format(CultureInfo.InvariantCulture, "  CONST speeddata {0} := {1};", velNames[v], velDecs[v]));
        }
        foreach (var z in zoneNames.Keys)
        {
            if (!zonePredef[z])  // no need to add declarations for predefined zones
            {
                zoneLines.Add(string.Format(CultureInfo.InvariantCulture, "  CONST zonedata {0} := {1};", zoneNames[z], zoneDecs[z]));
            }
        }
    }

    private bool GenrateData(ref RobotCursor writer, bool inlineTargets, List<Action> actions)
    {
        // Use the write robot pointer to generate the data
        int it = 0;
        string line;
        var declarationLines = new List<string>();
        bool usesIO = false;
        foreach (Action a in actions)
        {
            // Move writerCursor to this action state
            writer.ApplyNextAction();  // for the buffer to correctly manage them 

            // For ABB robots, check if any IO command is issued, and display a warning about configuring their names in the controller.
            if (!usesIO && (writer.lastAction.Type == ActionType.IODigital || writer.lastAction.Type == ActionType.IOAnalog))
            {
                usesIO = true;
            }

            // Check velocity + zone and generate data accordingly
            if (!velNames.ContainsKey(writer.speed))
            {
                velNames.Add(writer.speed, "vel" + SafeDoubleName(writer.speed));
                velDecs.Add(writer.speed, GetSpeedValue(writer));
            }

            if (!zoneNames.ContainsKey(writer.precision))
            {
                // If precision is very close to an integer, make it integer and/or use predefined zones
                bool predef = false;
                int roundZone = 0;
                if (Math.Abs(writer.precision - Math.Round(writer.precision)) < Geometry.EPSILON)
                {
                    roundZone = (int)Math.Round(writer.precision);
                    predef = PredefinedZones.Contains(roundZone);
                }
                zonePredef.Add(writer.precision, predef);
                zoneNames.Add(writer.precision, predef ? "z" + roundZone : "zone" + SafeDoubleName(writer.precision));  // use predef syntax or clean new one
                zoneDecs.Add(writer.precision, predef ? "" : GetZoneValue(writer));
            }

            if (writer.tool != null && !toolNames.ContainsKey(writer.tool))
            {
                toolNames.Add(writer.tool, writer.tool.name);
                toolDecs.Add(writer.tool, GetToolValue(writer.tool));
            }

            if (a.Type == ActionType.CustomCode && (a as ActionCustomCode).isDeclaration)
            {
                customLines.Add($"  {(a as ActionCustomCode).statement}");
            }



            // Generate program
            if (inlineTargets)
            {
                // Generate lines of code
                if (GenerateInstructionDeclaration(a, writer, out line))
                {
                    instructionLines.Add(line);
                }
            }
            else
            {
                // Generate lines of code
                if (GenerateVariableDeclaration(a, writer, it, out declarationLines))
                {
                    variableLines.AddRange(declarationLines);
                }

                if (GenerateInstructionDeclarationFromVariable(a, writer, it, out line))
                {
                    instructionLines.Add(line);
                }
            }

            // Move on (only for var-decs);
            it += declarationLines.Count;
        }

        return usesIO;
    }

    private void InitilizeAllVariables()
    {
        // VELOCITY & ZONE DECLARATIONS
        // Amount of different VZ types
        velNames = [];
        velDecs = [];
        zoneNames = [];
        zoneDecs = [];
        zonePredef = [];

        // TOOL DECLARATIONS
        toolNames = [];
        toolDecs = [];

        // Intro
        introLines = [];

        // Declarations
        velocityLines = [];
        zoneLines = [];
        toolLines = [];
        customLines = [];

        // TARGETS AND INSTRUCTIONS
        variableLines = [];
        instructionLines = [];
    }
    #endregion
}
