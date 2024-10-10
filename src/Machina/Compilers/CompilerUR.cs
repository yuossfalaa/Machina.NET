using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Machina.Types.Geometry;
using Machina.Types.Data;


namespace Machina;

/// <summary>
/// A compiler for Universal Robots 6-axis robotic arms.
/// </summary>
internal class CompilerUR : Compiler
{
    #region Variables
    internal override Encoding Encoding => Encoding.ASCII;

    internal override char CC => '#';

    #endregion

    #region ctor
    internal CompilerUR() : base() { }


    #endregion

    #region Public Methods
    /// <summary>
    /// Creates a textual program representation of a set of Actions using native UR Script.
    /// </summary>
    /// <param name="programName"></param>
    /// <param name="writePointer"></param>
    /// <param name="block">Use actions in waiting queue or buffer?</param>
    /// <returns></returns>
    public override RobotProgram UNSAFEFullProgramFromBuffer(string programName, RobotCursor writer, bool block, bool inlineTargets, bool humanComments)
    {
        // The program files to be returned
        RobotProgram robotProgram = new(programName, CC);

        // Which pending Actions are used for this program?
        // Copy them without flushing the buffer.
        List<Action> actions = block ?
            writer.actionBuffer.GetBlockPending(false) :
            writer.actionBuffer.GetAllPending(false);


        // CODE LINES GENERATION
        // TARGETS AND INSTRUCTIONS
        List<string> customDeclarationLines = [];
        List<string> variableLines = [];
        List<string> instructionLines = [];

        // DATA GENERATION
        // Use the write RobotCursor to generate the data
        GenerateData(writer, inlineTargets, humanComments, actions, ref customDeclarationLines, ref variableLines, ref instructionLines);

        // PROGRAM ASSEMBLY
        // Initialize a module list
        List<string> module = GenerateModuleList(programName, customDeclarationLines, variableLines, instructionLines);

        RobotProgramFile mainFile = new(programName, "script", Encoding, CC);
        mainFile.SetContent(module);
        robotProgram.Add(mainFile);

        return robotProgram;
    }

    #endregion

    #region Internal Methods
    internal static bool GenerateVariableDeclaration(Action action, RobotCursor cursor, int id, out string declaration)
    {
        string dec = null;
        switch (action.Type)
        {
            case ActionType.Translation:
            case ActionType.Rotation:
            case ActionType.Transformation:
                dec = string.Format("  target{0}={1}", id, GetPoseTargetValue(cursor));
                break;

            case ActionType.Axes:
                dec = string.Format("  target{0}={1}", id, GetJointTargetValue(cursor));
                break;
        }

        declaration = dec;
        return dec != null;
    }

    internal bool GenerateInstructionDeclarationFromVariable(
        Action action, RobotCursor cursor, int id, bool humanComments,
        out string declaration)
    {
        string dec = GenerateDecFromVariable(action, cursor, id);

        if (humanComments && action.Type != ActionType.Comment)
        {
            dec = string.Format("{0}  {1} [{2}]",
                dec,
                CC,
                action.ToString());
        }
        declaration = dec;
        return dec != null;
    }


    internal bool GenerateInstructionDeclaration(
        Action action, RobotCursor cursor, bool humanComments,
        out string declaration)
    {
        string dec = GenerateDec(action, cursor);

        if (humanComments && action.Type != ActionType.Comment)
        {
            dec = string.Format("{0}  {1} [{2}]",
                dec,
                CC,
                action.ToString());
        }
        declaration = dec;
        return dec != null;
    }

    /// <summary>
    /// Returns an UR pose representation of the current state of the cursor.
    /// </summary>
    /// <returns></returns>
    internal static string GetPoseTargetValue(RobotCursor cursor)
    {
        RotationVector axisAng = cursor.rotation.GetRotationVector(true);
        return string.Format(CultureInfo.InvariantCulture,
            "p[{0},{1},{2},{3},{4},{5}]",
            Math.Round(0.001 * cursor.position.X, Geometry.STRING_ROUND_DECIMALS_M),
            Math.Round(0.001 * cursor.position.Y, Geometry.STRING_ROUND_DECIMALS_M),
            Math.Round(0.001 * cursor.position.Z, Geometry.STRING_ROUND_DECIMALS_M),
            Math.Round(axisAng.X, Geometry.STRING_ROUND_DECIMALS_RADS),
            Math.Round(axisAng.Y, Geometry.STRING_ROUND_DECIMALS_RADS),
            Math.Round(axisAng.Z, Geometry.STRING_ROUND_DECIMALS_RADS));
    }

    /// <summary>
    /// Returns a UR joint representation of the current state of the cursor.
    /// </summary>
    /// <returns></returns>
    internal static string GetJointTargetValue(RobotCursor cursor)
    {
        Joints jrad = new Joints(cursor.axes);  // use a shallow copy
        jrad.Scale(Geometry.TO_RADS);  // convert to radians
        return string.Format(CultureInfo.InvariantCulture,
            "[{0},{1},{2},{3},{4},{5}]",
            Math.Round(jrad.J1, Geometry.STRING_ROUND_DECIMALS_RADS),
            Math.Round(jrad.J2, Geometry.STRING_ROUND_DECIMALS_RADS),
            Math.Round(jrad.J3, Geometry.STRING_ROUND_DECIMALS_RADS),
            Math.Round(jrad.J4, Geometry.STRING_ROUND_DECIMALS_RADS),
            Math.Round(jrad.J5, Geometry.STRING_ROUND_DECIMALS_RADS),
            Math.Round(jrad.J6, Geometry.STRING_ROUND_DECIMALS_RADS));
    }

    /// <summary>
    /// Returns a UR representation of a Tool object.
    /// </summary>
    /// <param name="cursor"></param>
    /// <returns></returns>
    internal static string GetToolValue(Tool tool)
    {
        if (tool == null)
        {
            throw new ArgumentException("Cursor has no tool attached");
        }

        RotationVector axisAng = tool.TCPOrientation.Q.ToRotationVector(true);

        return string.Format(CultureInfo.InvariantCulture,
            "p[{0},{1},{2},{3},{4},{5}]",
            Math.Round(0.001 * tool.TCPPosition.X, Geometry.STRING_ROUND_DECIMALS_M),
            Math.Round(0.001 * tool.TCPPosition.Y, Geometry.STRING_ROUND_DECIMALS_M),
            Math.Round(0.001 * tool.TCPPosition.Z, Geometry.STRING_ROUND_DECIMALS_M),
            Math.Round(axisAng.X, Geometry.STRING_ROUND_DECIMALS_RADS),
            Math.Round(axisAng.Y, Geometry.STRING_ROUND_DECIMALS_RADS),
            Math.Round(axisAng.Z, Geometry.STRING_ROUND_DECIMALS_RADS));

    }
    #endregion

    #region Private Methods

    private void GenerateData(RobotCursor writer, bool inlineTargets, bool humanComments, List<Action> actions,
        ref List<string> customDeclarationLines, ref List<string> variableLines, ref List<string> instructionLines)
    {
        int it = 0;
        string line = null;
        foreach (Action a in actions)
        {
            // Move writerCursor to this action state
            writer.ApplyNextAction();  // for the buffer to correctly manage them 

            if (a.Type == ActionType.CustomCode && (a as ActionCustomCode).isDeclaration)
            {
                customDeclarationLines.Add("  " + (a as ActionCustomCode).statement);
            }

            if (inlineTargets)
            {
                if (GenerateInstructionDeclaration(a, writer, humanComments, out line))  // there will be a number jump on target-less instructions, but oh well...
                {
                    instructionLines.Add(line);
                }
            }
            else
            {
                // Generate lines of code
                if (GenerateVariableDeclaration(a, writer, it, out line))  // there will be a number jump on target-less instructions, but oh well...
                {
                    variableLines.Add(line);
                }

                if (GenerateInstructionDeclarationFromVariable(a, writer, it, humanComments, out line))  // there will be a number jump on target-less instructions, but oh well...
                {
                    instructionLines.Add(line);
                }
            }

            // Move on
            it++;
        }
    }

    private List<string> GenerateModuleList(string programName, List<string> customDeclarationLines, List<string> variableLines, List<string> instructionLines)
    {

        List<string> module =
        [
            // Banner
            .. GenerateDisclaimerHeader(programName),
            "",
            // MODULE HEADER
            "def " + programName + "():",
            "",
        ];

        // Custom declarations
        if (customDeclarationLines.Count != 0)
        {
            module.AddRange(customDeclarationLines);
            module.Add("");
        }

        // Targets
        if (variableLines.Count != 0)
        {
            module.AddRange(variableLines);
            module.Add("");
        }

        // MAIN PROCEDURE
        // Instructions
        if (instructionLines.Count != 0)
        {
            module.AddRange(instructionLines);
            module.Add("");
        }

        module.Add("end");
        module.Add("");

        // MODULE KICKOFF
        module.Add(programName + "()");
        return module;
    }

    #endregion

    #region Generate Dec From Variable
    private string GenerateDecFromVariable(Action action, RobotCursor cursor, int id)
    {
        return action.Type switch
        {
            ActionType.Translation or ActionType.Rotation or ActionType.Transformation => GenerateDecForTranaslation_Rotation_TransforFromVariable(cursor, id),
            ActionType.Axes => GenrateDecForAxesFromVariable(cursor, id),
            ActionType.Message => GenerateDecForMessageActionFromVariable(action),
            ActionType.Wait => GenerateDecForWaitActionFromVariable(action),
            ActionType.Comment => GenerateDecForCommentActionFromVariable(action),
            ActionType.DefineTool => GenerateDecForDefineToolActionFromVariable(action),
            ActionType.AttachTool => GenerateDecForAttachToolActionFromVariable(action, cursor),
            ActionType.DetachTool => GenerateDecForDetachToolActionFromVariable(action),
            ActionType.IODigital => GenerateDecForIODigitalActionFromVariable(action),
            ActionType.IOAnalog => GenerateDecForIOAnalogActionFromVariable(action),
            ActionType.CustomCode => GenerateDecForCustomCodeActionFromVariable(action),
            _ => null
        };
    }

    private static string GenerateDecForCustomCodeActionFromVariable(Action action)
    {
        ActionCustomCode acc = action as ActionCustomCode;
        if (!acc.isDeclaration)
        {
            return $"  {acc.statement}";
        }

        return null;
    }

    private string GenerateDecForIOAnalogActionFromVariable(Action action)
    {
        string dec = null;
        ActionIOAnalog aioa = (ActionIOAnalog)action;
        if (!aioa.isDigit)
        {
            dec = $"  {CC} ERROR on \"{aioa}\": only integer pin names are possible";
        }
        else if (aioa.pinNum < 0 || aioa.pinNum > 1)
        {
            dec = $"  {CC} ERROR on \"{aioa}\": IO number not available";
        }
        else if (aioa.value < 0 || aioa.value > 1)
        {
            dec = $"  {CC} ERROR on \"{aioa}\": value out of range [0.0, 1.0]";
        }
        else
        {
            dec = string.Format(CultureInfo.InvariantCulture,
                "  set_standard_analog_out({0}, {1})",
                //aioa.isToolPin ? "tool" : "standard",  // there is no analog out on the tool!
                aioa.pinNum,
                Math.Round(aioa.value, Geometry.STRING_ROUND_DECIMALS_VOLTAGE));

        }

        return dec;
    }

    private string GenerateDecForIODigitalActionFromVariable(Action action)
    {
        string dec = null;
        ActionIODigital aiod = (ActionIODigital)action;
        if (!aiod.isDigit)
        {
            dec = $"  {CC} ERROR on \"{aiod}\": only integer pin names are possible";
        }
        else if (aiod.pinNum < 0 || aiod.pinNum > 7)
        {
            dec = $"  {CC} ERROR on \"{aiod}\": IO number not available";
        }
        else
        {
            dec = $"  set_{(aiod.isToolPin ? "tool" : "standard")}_digital_out({aiod.pinNum}, {(aiod.on ? "True" : "False")})";
        }

        return dec;
    }

    private static string GenerateDecForDetachToolActionFromVariable(Action action)
    {
        string dec = null;
        ActionDetachTool ad = (ActionDetachTool)action;
        dec = string.Format("  set_tcp(p[0,0,0,0,0,0])");  // @TODO: should need to add a "set_payload(m, CoG)" dec afterwards...
        return dec;
    }

    private static string GenerateDecForAttachToolActionFromVariable(Action action, RobotCursor cursor)
    {
        string dec = null;
        ActionAttachTool aa = (ActionAttachTool)action;
        dec = string.Format("  set_tcp({0})",  // @TODO: should need to add a "set_payload(m, CoG)" dec afterwards...
            GetToolValue(cursor.tool));
        return dec;
    }

    private string GenerateDecForDefineToolActionFromVariable(Action action)
    {
        string dec = null;
        ActionDefineTool adt = action as ActionDefineTool;
        dec = string.Format("  {0} Tool \"{1}\" defined",  // this action has no actual instruction, just add a comment
            CC,
            adt.tool.name);
        return dec;
    }

    private string GenerateDecForCommentActionFromVariable(Action action)
    {
        string dec = null;
        ActionComment ac = (ActionComment)action;
        dec = string.Format("  {0} {1}",
            CC,
            ac.comment);
        return dec;
    }

    private static string GenerateDecForWaitActionFromVariable(Action action)
    {
        string dec = null;
        ActionWait aw = (ActionWait)action;
        dec = string.Format(CultureInfo.InvariantCulture,
            "  sleep({0})",
            0.001 * aw.millis);
        return dec;
    }

    private static string GenerateDecForMessageActionFromVariable(Action action)
    {
        string dec = null;
        ActionMessage am = (ActionMessage)action;
        dec = string.Format("  popup(\"{0}\", title=\"Machina Message\", warning=False, error=False)",
            am.message);
        return dec;
    }

    private static string GenrateDecForAxesFromVariable(RobotCursor cursor, int id)
    {
        return string.Format(CultureInfo.InvariantCulture,
                                "  movej(target{0}, a={1}, v={2}, r={3})",
                                id,
                                Math.Round(Geometry.TO_RADS * cursor.acceleration, Geometry.STRING_ROUND_DECIMALS_RADS),
                                Math.Round(Geometry.TO_RADS * cursor.speed, Geometry.STRING_ROUND_DECIMALS_RADS),
                                Math.Round(0.001 * cursor.precision, Geometry.STRING_ROUND_DECIMALS_M));
    }

    private static string GenerateDecForTranaslation_Rotation_TransforFromVariable(RobotCursor cursor, int id)
    {
        string dec = null;
        // Accelerations and velocities have different meaning for movej and movel instructions: they are either angular or linear respectively.
        // Use speed and acceleration values as deg/s or mm/s (converted to rad and m) in either case. 
        if (cursor.motionType == MotionType.Joint)
        {
            dec = string.Format(CultureInfo.InvariantCulture,
                "  movej(target{0}, a={1}, v={2}, r={3})",
                id,
                Math.Round(Geometry.TO_RADS * cursor.acceleration, Geometry.STRING_ROUND_DECIMALS_RADS),
                Math.Round(Geometry.TO_RADS * cursor.speed, Geometry.STRING_ROUND_DECIMALS_RADS),
                Math.Round(0.001 * cursor.precision, Geometry.STRING_ROUND_DECIMALS_M));
        }
        else
        {
            dec = string.Format(CultureInfo.InvariantCulture,
                "  movep(target{0}, a={1}, v={2}, r={3})",
                id,
                Math.Round(0.001 * cursor.acceleration, Geometry.STRING_ROUND_DECIMALS_M),
                Math.Round(0.001 * cursor.speed, Geometry.STRING_ROUND_DECIMALS_M),
                Math.Round(0.001 * cursor.precision, Geometry.STRING_ROUND_DECIMALS_M));
        }

        return dec;
    }

    #endregion

    #region Generate Dec
    private string GenerateDec(Action action, RobotCursor cursor)
    {
        return action.Type switch
        {
            ActionType.Translation or ActionType.Transformation or ActionType.Rotation => GenerateDecForTTRActions(cursor),
            ActionType.Axes => GenerateDecForAxesAction(cursor),
            ActionType.Message => GenerateDecForMessageAction(action),
            ActionType.Wait => GenerateDecForWaitAction(action),
            ActionType.Comment => GenerateDecForCommentAction(action),
            ActionType.DefineTool => GenerateDecForDefineToolAction(action),
            ActionType.AttachTool => GenerateDecForAttachToolAction(action, cursor),
            ActionType.DetachTool => GenerateDecForDetachToolAction(action),
            ActionType.IODigital => GenerateDecForIODigitalAction(action),
            ActionType.IOAnalog => GenerateDecForIOAnalogAction(action),
            ActionType.CustomCode => GenerateDecForCustomCodeAction(action),
            _ => null
        };
    }

    private static string GenerateDecForCustomCodeAction(Action action)
    {
        ActionCustomCode acc = action as ActionCustomCode;
        if (!acc.isDeclaration)
        {
            return $"  {acc.statement}";
        }
        return null;
    }

    private string GenerateDecForIOAnalogAction(Action action)
    {
        ActionIOAnalog aioa = (ActionIOAnalog)action;
        if (!aioa.isDigit)
        {
            return $"  {CC} ERROR on \"{aioa}\": only integer pin names are possible";
        }
        else if (aioa.pinNum < 0 || aioa.pinNum > 1)
        {
            return $"  {CC} ERROR on \"{aioa}\": IO number not available";
        }
        else if (aioa.value < 0 || aioa.value > 1)
        {
            return $"  {CC} ERROR on \"{aioa}\": value out of range [0.0, 1.0]";
        }
        else
        {
            return string.Format(CultureInfo.InvariantCulture,
                "  set_{0}_analog_out({1}, {2})",
                aioa.isToolPin ? "tool" : "standard",
                aioa.pinNum,
                Math.Round(aioa.value, Geometry.STRING_ROUND_DECIMALS_VOLTAGE));
        }
    }


    private string GenerateDecForIODigitalAction(Action action)
    {
        ActionIODigital aiod = (ActionIODigital)action;
        if (!aiod.isDigit)
        {
            return $"  {CC} ERROR on \"{aiod}\": only integer pin names are possible";
        }
        else if (aiod.pinNum < 0 || aiod.pinNum > 7)
        {
            return $"  {CC} ERROR on \"{aiod}\": IO number not available";
        }
        else
        {
            return $"  set_{(aiod.isToolPin ? "tool" : "standard")}_digital_out({aiod.pinNum}, {(aiod.on ? "True" : "False")})";
        }
    }

    private static string GenerateDecForDetachToolAction(Action action)
    {
        ActionDetachTool ad = (ActionDetachTool)action;
        return string.Format("  set_tcp(p[0,0,0,0,0,0])");   // @TODO: should need to add a "set_payload(m, CoG)" dec afterwards...
    }

    private static string GenerateDecForAttachToolAction(Action action, RobotCursor cursor)
    {
        ActionAttachTool aa = (ActionAttachTool)action;
        return string.Format("  set_tcp({0})",   // @TODO: should need to add a "set_payload(m, CoG)" dec afterwards...
            GetToolValue(cursor.tool));
    }

    private string GenerateDecForDefineToolAction(Action action)
    {
        ActionDefineTool adt = action as ActionDefineTool;
        return string.Format("  {0} Tool \"{1}\" defined",  // this action has no actual instruction, just add a comment
             CC,
             adt.tool.name);
    }

    private string GenerateDecForCommentAction(Action action)
    {
        ActionComment ac = (ActionComment)action;
        return string.Format("  {0} {1}",
            CC,
            ac.comment);
    }

    private static string GenerateDecForWaitAction(Action action)
    {
        ActionWait aw = (ActionWait)action;
        return string.Format(CultureInfo.InvariantCulture,
            "  sleep({0})",
            0.001 * aw.millis);
    }

    private static string GenerateDecForMessageAction(Action action)
    {
        ActionMessage am = (ActionMessage)action;
        return string.Format("  popup(\"{0}\", title=\"Machina Message\", warning=False, error=False)",
            am.message);
    }

    private static string GenerateDecForAxesAction(RobotCursor cursor)
    {
        return string.Format(CultureInfo.InvariantCulture,
                     "  movej({0}, a={1}, v={2}, r={3})",
                     GetJointTargetValue(cursor),
                     Math.Round(Geometry.TO_RADS * cursor.acceleration, Geometry.STRING_ROUND_DECIMALS_RADS),
                     Math.Round(Geometry.TO_RADS * cursor.speed, Geometry.STRING_ROUND_DECIMALS_RADS),
                     Math.Round(0.001 * cursor.precision, Geometry.STRING_ROUND_DECIMALS_M));
    }

    private static string GenerateDecForTTRActions(RobotCursor cursor)
    {
        // Accelerations and velocities have different meaning for movej and movel instructions: they are either angular or linear respectively.
        // Use speed and acceleration values as deg/s or mm/s (converted to rad and m) in either case. 
        if (cursor.motionType == MotionType.Joint)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "  movej({0}, a={1}, v={2}, r={3})",
                GetPoseTargetValue(cursor),
                Math.Round(Geometry.TO_RADS * cursor.acceleration, Geometry.STRING_ROUND_DECIMALS_RADS),
                Math.Round(Geometry.TO_RADS * cursor.speed, Geometry.STRING_ROUND_DECIMALS_RADS),
                Math.Round(0.001 * cursor.precision, Geometry.STRING_ROUND_DECIMALS_M));
        }
        else
        {
            return string.Format(CultureInfo.InvariantCulture,
                "  movep({0}, a={1}, v={2}, r={3})",
                GetPoseTargetValue(cursor),
                Math.Round(0.001 * cursor.acceleration, Geometry.STRING_ROUND_DECIMALS_M),
                Math.Round(0.001 * cursor.speed, Geometry.STRING_ROUND_DECIMALS_M),
                Math.Round(0.001 * cursor.precision, Geometry.STRING_ROUND_DECIMALS_M));
        }
    }
    #endregion
}
