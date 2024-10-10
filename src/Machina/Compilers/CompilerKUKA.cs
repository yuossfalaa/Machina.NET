using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Machina.Types.Geometry;
using Machina.Types.Data;

namespace Machina;

/// <summary>
/// A compiler for KUKA 6-axis industrial robotic arms.
/// </summary>
internal class CompilerKUKA : Compiler
{

    #region Variables
    internal override Encoding Encoding => Encoding.ASCII;

    internal override char CC => ';';
    List<string> declarationLines, customDeclarationLines, initializationLines, instructionLines;

    #endregion

    #region ctor
    internal CompilerKUKA() : base() { }
    #endregion

    #region Public Methods
    /// <summary>
    /// Creates a textual program representation of a set of Actions using native KUKA Robot Language.
    /// </summary>
    /// <param name="programName"></param>
    /// <param name="writePointer"></param>
    /// <param name="block">Use actions in waiting queue or buffer?</param>
    /// <returns></returns>
    public override RobotProgram UNSAFEFullProgramFromBuffer(string programName, RobotCursor writer, bool block, bool inlineTargets, bool humanComments)
    {
        // The program files to be returned
        RobotProgram robotProgram = new RobotProgram(programName, CC);

        // HEADER file
        RobotProgramFile datFile = new RobotProgramFile(programName, "dat", Encoding, CC);

        // PROGRAM FILE
        addActionString = humanComments;

        // Which pending Actions are used for this program?
        // Copy them without flushing the buffer.
        List<Action> actions = block ?
            writer.actionBuffer.GetBlockPending(false) :
            writer.actionBuffer.GetAllPending(false);

        // CODE LINES GENERATION
        // TARGETS AND INSTRUCTIONS
        InitilizeLists();

        AddinitializationLines(programName);

        // DATA GENERATION
        GenrateData(writer, inlineTargets, actions);

        // PROGRAM ASSEMBLY
        List<string> module = MakeModuelList(programName);

        RobotProgramFile srcFile = new RobotProgramFile(programName, "src", Encoding, CC);
        srcFile.SetContent(module);
        robotProgram.Add(srcFile);

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
                dec = string.Format("  POS target{0}", id);
                break;

            case ActionType.Axes:
                dec = string.Format("  AXIS target{0}", id);
                break;
        }

        declaration = dec;
        return dec != null;
    }

    internal static bool GenerateVariableInitialization(Action action, RobotCursor cursor, int id, out string declaration)
    {
        string dec = GenerateDec(action, cursor, id);

        declaration = dec;
        return dec != null;
    }

    internal bool GenerateInstructionDeclarationFromVariable(Action action, RobotCursor cursor, int id,
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
        Action action, RobotCursor cursor,
        out string declaration)
    {
        string dec = null;
        switch (action.Type)
        {
            // KUKA does explicit setting of velocities and approximate positioning, so these actions make sense as instructions
            case ActionType.Speed:
                dec = string.Format(CultureInfo.InvariantCulture,
                    //"  $VEL = {{CP {0}, ORI1 100, ORI2 100}}",  // This was reported to not work
                    "  $VEL.CP = {0}",  // @TODO: figure out how to also incorporate ORI1 and ORI2
                    Math.Round(0.001 * cursor.speed, 3 + Geometry.STRING_ROUND_DECIMALS_MM));
                break;

            case ActionType.Precision:
                dec = string.Format(CultureInfo.InvariantCulture,
                    "  $APO.CDIS = {0}",
                    cursor.precision);
                break;

            case ActionType.Translation:
                // @Arastoo
                // making a case for Translation as it requires only XYZ
                // adding ABC would mess with the orientation of the robot
                // GetPositionTargetValue_Translation_Only
                if (cursor.motionType == MotionType.Joint)
                {
                    dec = string.Format("  {0} {1} {2}",
                        cursor.motionType == MotionType.Joint ? "PTP" : "LIN",
                        GetPositionTargetValue_Translation_Only(cursor),
                        cursor.precision >= 1 ? "C_PTP" : "");
                }
                else if (cursor.motionType == MotionType.Linear)
                {
                    dec = string.Format("  {0} {1} {2}",
                        cursor.motionType == MotionType.Joint ? "PTP" : "LIN",
                        GetPositionTargetValue_Translation_Only(cursor),
                        cursor.precision >= 1 ? "C_DIS" : "");
                }
                break;

            case ActionType.Rotation:
            case ActionType.Transformation:

                if (cursor.motionType == MotionType.Joint)
                {
                    dec = string.Format("  {0} {1} {2}",
                        cursor.motionType == MotionType.Joint ? "PTP" : "LIN",
                        GetPositionTargetValue(cursor),
                        cursor.precision >= 1 ? "C_PTP" : "");
                }
                else if (cursor.motionType == MotionType.Linear)
                {
                    dec = string.Format("  {0} {1} {2}",
                        cursor.motionType == MotionType.Joint ? "PTP" : "LIN",
                        GetPositionTargetValue(cursor),
                        cursor.precision >= 1 ? "C_DIS" : "");
                }
                break;

            case ActionType.Axes:
                dec = string.Format("  {0} {1} {2}",
                    "PTP",
                    GetAxisTargetValue(cursor),
                    cursor.precision >= 1 ? "C_DIS" : "");  // @TODO: figure out how to turn this into C_PTP
                break;

            // @TODO: apparently, messages in KRL are kind fo tricky, with several manuals just dedicated to it.
            // Will figure this out later.
            case ActionType.Message:
                ActionMessage am = (ActionMessage)action;
                dec = string.Format("  {0} MESSAGE: \"{1}\" (messages in KRL currently not supported in Machina)",
                    CC,
                    am.message);
                break;

            case ActionType.Wait:
                ActionWait aw = (ActionWait)action;
                dec = string.Format(CultureInfo.InvariantCulture,
                    "  WAIT SEC {0}",
                    0.001 * aw.millis);
                break;

            case ActionType.Comment:
                ActionComment ac = (ActionComment)action;
                dec = string.Format("  {0} {1}",
                    CC,
                    ac.comment);
                break;

            case ActionType.DefineTool:
                ActionDefineTool adt = action as ActionDefineTool;
                dec = string.Format("  {0} Tool \"{1}\" defined",  // this action has no actual instruction, just add a comment
                    CC,
                    adt.tool.name);
                break;

            case ActionType.AttachTool:
                ActionAttachTool at = (ActionAttachTool)action;
                dec = string.Format("  $TOOL = {0}",
                    GetToolValue(cursor.tool));
                break;

            case ActionType.DetachTool:
                ActionDetachTool ad = (ActionDetachTool)action;
                dec = string.Format("  $TOOL = $NULLFRAME");
                break;

            case ActionType.IODigital:
                ActionIODigital aiod = (ActionIODigital)action;
                if (!aiod.isDigit)
                {
                    dec = $"  {CC} ERROR on \"{aiod}\": only integer pin names are possible";
                }
                else if (aiod.pinNum < 1 || aiod.pinNum > 32)  // KUKA starts counting pins by 1
                {
                    dec = $"  {CC} ERROR on \"{aiod}\": IO number not available";
                }
                else
                {
                    dec = $"  $OUT[{aiod.pinNum}] = {(aiod.on ? "TRUE" : "FALSE")}";
                }
                break;

            case ActionType.IOAnalog:
                ActionIOAnalog aioa = (ActionIOAnalog)action;
                if (!aioa.isDigit)
                {
                    dec = $"  {CC} ERROR on \"{aioa}\": only integer pin names are possible";
                }
                else if (aioa.pinNum < 1 || aioa.pinNum > 16)    // KUKA: analog pins [1 to 16]
                {
                    dec = $"  {CC} ERROR on \"{aioa}\": IO number not available";
                }
                else if (aioa.value < -1 || aioa.value > 1)
                {
                    dec = $"  {CC} ERROR on \"{aioa}\": value out of range [-1.0, 1.0]";
                }
                else
                {
                    //dec = $"  $ANOUT[{aioa.pinNum}] = {Math.Round(aioa.value, Geometry.STRING_ROUND_DECIMALS_VOLTAGE)}";
                    dec = string.Format(CultureInfo.InvariantCulture,
                        "  $ANOUT[{0}] = {1}",
                        aioa.pinNum,
                        Math.Round(aioa.value, Geometry.STRING_ROUND_DECIMALS_VOLTAGE));
                }
                break;

            case ActionType.CustomCode:
                ActionCustomCode acc = action as ActionCustomCode;
                if (!acc.isDeclaration)
                {
                    dec = $"  {acc.statement}";
                }
                break;

                //default:
                //    dec = string.Format("  ; ACTION \"{0}\" NOT IMPLEMENTED", action);
                //    break;
        }

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

    /// <summary>
    /// Returns a KRL FRAME representation of the current state of the cursor.
    /// Note POS also accept T and S parameters for unambiguous arm configuration def. @TODO: implement?
    /// </summary>
    /// <returns></returns>
    internal static string GetPositionTargetValue(RobotCursor cursor)
    {
        // adjusting the action's rotation to match the KUKA robot's convention
        cursor.rotation.RotateLocal(new Rotation(0, 1, 0, 90));
        YawPitchRoll euler = cursor.rotation.Q.ToYawPitchRoll();  // @TODO: does this actually work...?

        // @TODO: E6POS: External Axes (Arastoo)
        return string.Format(CultureInfo.InvariantCulture,
            "{{POS: X {0}, Y {1}, Z {2}, A {3}, B {4}, C {5}}}",
            Math.Round(cursor.position.X, Geometry.STRING_ROUND_DECIMALS_MM),
            Math.Round(cursor.position.Y, Geometry.STRING_ROUND_DECIMALS_MM),
            Math.Round(cursor.position.Z, Geometry.STRING_ROUND_DECIMALS_MM),
            // note reversed ZYX order
            Math.Round(euler.ZAngle, Geometry.STRING_ROUND_DECIMALS_DEGS),
            Math.Round(euler.YAngle, Geometry.STRING_ROUND_DECIMALS_DEGS),
            Math.Round(euler.XAngle, Geometry.STRING_ROUND_DECIMALS_DEGS));
    }

    internal static string GetPositionTargetValue_Translation_Only(RobotCursor cursor)
    {
        // adjusting the action's rotation to match the KUKA robot's convention
        cursor.rotation.RotateLocal(new Rotation(0, 1, 0, 90));
        YawPitchRoll euler = cursor.rotation.Q.ToYawPitchRoll();  // @TODO: does this actually work...?

        // @TODO: E6POS: External Axes (Arastoo)
        return string.Format(CultureInfo.InvariantCulture,
            "{{POS: X {0}, Y {1}, Z {2}}}",
            Math.Round(cursor.position.X, Geometry.STRING_ROUND_DECIMALS_MM),
            Math.Round(cursor.position.Y, Geometry.STRING_ROUND_DECIMALS_MM),
            Math.Round(cursor.position.Z, Geometry.STRING_ROUND_DECIMALS_MM)
            );
    }

    /// <summary>
    /// Returns a KRL AXIS joint representation of the current state of the cursor.
    /// </summary>
    /// <returns></returns>
    internal static string GetAxisTargetValue(RobotCursor cursor)
    {
        return string.Format(CultureInfo.InvariantCulture,
            "{{AXIS: A1 {0}, A2 {1}, A3 {2}, A4 {3}, A5 {4}, A6 {5}}}",
            Math.Round(cursor.axes.J1, Geometry.STRING_ROUND_DECIMALS_DEGS),
            Math.Round(cursor.axes.J2, Geometry.STRING_ROUND_DECIMALS_DEGS),
            Math.Round(cursor.axes.J3, Geometry.STRING_ROUND_DECIMALS_DEGS),
            Math.Round(cursor.axes.J4, Geometry.STRING_ROUND_DECIMALS_DEGS),
            Math.Round(cursor.axes.J5, Geometry.STRING_ROUND_DECIMALS_DEGS),
            Math.Round(cursor.axes.J6, Geometry.STRING_ROUND_DECIMALS_DEGS));
    }

    /// <summary>
    /// Returns a KRL representation of a Tool object
    /// </summary>
    /// <param name="cursor"></param>
    /// <returns></returns>
    internal static string GetToolValue(Tool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        // @To Do
        // adjusting the action's rotation to match the KUKA robot's convention
        //cursor.rotation.RotateLocal(new Rotation(0, 1, 0, 90));
        YawPitchRoll euler = tool.TCPOrientation.Q.ToYawPitchRoll();

        return string.Format(CultureInfo.InvariantCulture,
            "{{X {0}, Y {1}, Z {2}, A {3}, B {4}, C {5}}}",
            Math.Round(tool.TCPPosition.X, Geometry.STRING_ROUND_DECIMALS_MM),
            Math.Round(tool.TCPPosition.Y, Geometry.STRING_ROUND_DECIMALS_MM),
            Math.Round(tool.TCPPosition.Z, Geometry.STRING_ROUND_DECIMALS_MM),
            // note reversed ZYX order
            Math.Round(euler.ZAngle, Geometry.STRING_ROUND_DECIMALS_DEGS),
            Math.Round(euler.YAngle, Geometry.STRING_ROUND_DECIMALS_DEGS),
            Math.Round(euler.XAngle, Geometry.STRING_ROUND_DECIMALS_DEGS));
    }
    #endregion

    #region Generate Dec From Variable
    private string GenerateDecFromVariable(Action action, RobotCursor cursor, int id)
    {
        return action.Type switch
        {
            // KUKA does explicit setting of velocities and approximate positioning, so these actions make sense as instructions
            ActionType.Speed => GenrateDecForSpeedActionFromVariable(cursor),
            ActionType.Precision => GenrateDecForPrecisionActionFromVariable(cursor),
            ActionType.Translation => GenrateDecForTranslationActionFromVariable(cursor, id),
            ActionType.Rotation or ActionType.Transformation => GenrateDecForRTActionsFromVariable(cursor, id),
            ActionType.Axes => GenrateDecForAxesActionFromVariable(cursor, id),
            ActionType.Message => GenrateDecForMessageActionFromVariable(action),
            ActionType.Wait => GenrateDecForWaitActionFromVariable(action),
            ActionType.Comment => GenrateDecForCommentActionFromVariable(action),
            ActionType.DefineTool => GenrateDecForDefineToolActionFromVariable(action),
            ActionType.AttachTool => GenrateDecForAttachToolActionFromVariable(action, cursor),
            ActionType.DetachTool => GenrateDecForDetachToolActionFromVariable(action),
            ActionType.IODigital => GenrateDecForIODigitalActionFromVariable(action),
            ActionType.IOAnalog => GenrateDecForIOAnalogActionFromVariable(action),
            ActionType.CustomCode => GenrateDecForCustomCodeActionFromVariable(action),
            _ => null,
        };
    }

    private static string GenrateDecForCustomCodeActionFromVariable(Action action)
    {
        ActionCustomCode acc = action as ActionCustomCode;
        if (!acc.isDeclaration)
        {
            return $"  {acc.statement}";
        }
        return null;
    }

    private string GenrateDecForIOAnalogActionFromVariable(Action action)
    {
        ActionIOAnalog aioa = (ActionIOAnalog)action;
        if (!aioa.isDigit)
        {
            return $"  {CC} ERROR on \"{aioa}\": only integer pin names are possible";
        }
        else if (aioa.pinNum < 1 || aioa.pinNum > 16)    // KUKA: analog pins [1 to 16]
        {
            return $"  {CC} ERROR on \"{aioa}\": IO number not available";
        }
        else if (aioa.value < -1 || aioa.value > 1)
        {
            return $"  {CC} ERROR on \"{aioa}\": value out of range [-1.0, 1.0]";
        }
        else
        {
            return string.Format(CultureInfo.InvariantCulture,
                "  $ANOUT[{0}] = {1}",
                aioa.pinNum,
                Math.Round(aioa.value, Geometry.STRING_ROUND_DECIMALS_VOLTAGE));

        }
    }

    private string GenrateDecForIODigitalActionFromVariable(Action action)
    {
        ActionIODigital aiod = (ActionIODigital)action;
        if (!aiod.isDigit)
        {
            return $"  {CC} ERROR on \"{aiod}\": only integer pin names are possible";
        }
        else if (aiod.pinNum < 1 || aiod.pinNum > 32)  // KUKA starts counting pins by 1
        {
            return $"  {CC} ERROR on \"{aiod}\": IO number not available";
        }
        else
        {
            return $"  $OUT[{aiod.pinNum}] = {(aiod.on ? "TRUE" : "FALSE")}";
        }
    }

    private static string GenrateDecForDetachToolActionFromVariable(Action action)
    {
        ActionDetachTool ad = (ActionDetachTool)action;
        return string.Format("  $TOOL = $NULLFRAME");
    }

    private static string GenrateDecForAttachToolActionFromVariable(Action action, RobotCursor cursor)
    {
        ActionAttachTool at = (ActionAttachTool)action;
        return string.Format("  $TOOL = {0}",
            GetToolValue(cursor.tool));
    }

    private string GenrateDecForDefineToolActionFromVariable(Action action)
    {
        ActionDefineTool adt = action as ActionDefineTool;

        return string.Format("  {0} Tool \"{1}\" defined",  // this action has no actual instruction, just add a comment
            CC,
            adt.tool.name);
    }

    private string GenrateDecForCommentActionFromVariable(Action action)
    {
        ActionComment ac = (ActionComment)action;

        return string.Format("  {0} {1}",
            CC,
            ac.comment);
    }

    private static string GenrateDecForWaitActionFromVariable(Action action)
    {
        ActionWait aw = (ActionWait)action;

        return string.Format(CultureInfo.InvariantCulture,
            "  WAIT SEC {0}",
            0.001 * aw.millis);
    }

    private string GenrateDecForMessageActionFromVariable(Action action)
    {
        // @TODO: apparently, messages in KRL are kind for tricky, with several manuals just dedicated to it.
        //      @Fixed by Arastoo
        // Will figure this out later.
        ActionMessage am = (ActionMessage)action;
        return string.Format("  {0} MESSAGE: \"{1}\" (messages in KRL currently not supported in Machina)",
            CC,
            am.message);
    }

    private static string GenrateDecForAxesActionFromVariable(RobotCursor cursor, int id)
    {
        return string.Format("  {0} target{1} {2}",
            "PTP",
            id,
            cursor.precision >= 1 ? "C_DIS" : "");  // @TODO: figure out how to turn this into C_PTP
    }

    private static string GenrateDecForRTActionsFromVariable(RobotCursor cursor, int id)
    {
        return string.Format("  {0} target{1} {2}",
            cursor.motionType == MotionType.Linear ? "LIN" : "PTP",
            id,
            cursor.precision >= 1 ? "C_DIS" : "");
    }

    private static string GenrateDecForTranslationActionFromVariable(RobotCursor cursor, int id)
    {
        // @Aratoo 
        // creating another case for translation as it should not have ABC euler angles in it.
        // having angles would mess with the orientation of the robot and result in unexpected movements.
        return string.Format("  {0} target{1} {2}",
            cursor.motionType == MotionType.Linear ? "LIN" : "PTP",
            id,
            cursor.precision >= 1 ? "C_DIS" : "");
    }

    private static string GenrateDecForPrecisionActionFromVariable(RobotCursor cursor)
    {
        return string.Format(CultureInfo.InvariantCulture,
                            "  $APO.CDIS = {0}",
                            cursor.precision);
    }

    private static string GenrateDecForSpeedActionFromVariable(RobotCursor cursor)
    {
        return string.Format(CultureInfo.InvariantCulture,
            //"  $VEL = {{CP {0}, ORI1 100, ORI2 100}}",  // This was reported to not work
            "  $VEL.CP = {0}",  // @TODO: figure out how to also incorporate ORI1 and ORI2
            Math.Round(0.001 * cursor.speed, 3 + Geometry.STRING_ROUND_DECIMALS_MM));
    }
    #endregion

    #region Generate Dec
    private static string GenerateDec(Action action, RobotCursor cursor, int id)
    {
        return action.Type switch
        {
            ActionType.Translation => GenerateDecForTranslation(cursor, id),
            ActionType.Rotation or ActionType.Transformation => GenerateDecForRTActions(cursor, id),
            ActionType.Axes => GenerateDecForAxes(cursor, id),
            _ => null,
        };
    }

    private static string GenerateDecForAxes(RobotCursor cursor, int id)
    {
        return string.Format("  target{0} = {1}",
            id,
            GetAxisTargetValue(cursor));
    }

    private static string GenerateDecForRTActions(RobotCursor cursor, int id)
    {
        return string.Format("  target{0} = {1}",
            id,
            GetPositionTargetValue(cursor));
    }

    private static string GenerateDecForTranslation(RobotCursor cursor, int id)
    {
        // @Arastoo
        // making a case for Translation as it requires only XYZ
        // adding ABC would mess with the orientation of the robot
        // GetPositionTargetValue_Translation_Only
        return string.Format("  target{0} = {1}",
                id,
                GetPositionTargetValue_Translation_Only(cursor));
    }
    #endregion

    #region Private Methods
    private void InitilizeLists()
    {
        declarationLines = [];
        customDeclarationLines = [];
        initializationLines = [];
        instructionLines = [];
    }

    private List<string> MakeModuelList(string programName)
    {
        // Initialize a module list
        List<string> module =
        [
            // Banner
            //module.AddRange(GenerateDisclaimerHeader(programName));

            // SOME INTERFACE INITIALIZATION
            // These are all for interface handling, ignored by the compiler (?)
            @"&ACCESS RVP",  // read-write permissions
            @"&REL 1",       // release number (increments on file changes)
            @"&PARAM TEMPLATE = C:\KRC\Roboter\Template\vorgabe",
            @"&PARAM EDITMASK = *",
            // MODULE HEADER
            "DEF " + programName + " ( )",
            "",
            "",
        ];

        // Declarations
        if (declarationLines.Count != 0)
        {
            module.AddRange(declarationLines);
            module.Add("");
        }

        // Custom declarations
        if (customDeclarationLines.Count != 0)
        {
            module.AddRange(customDeclarationLines);
            module.Add("");
        }

        // Initializations
        if (initializationLines.Count != 0)
        {
            module.AddRange(initializationLines);
            module.Add("");
        }

        // Before going to the instructions let's add some default settings 
        // like approximation settings "$ADVANCE"
        module.Add(@"$VEL.CP=0.25");
        module.Add(@"$ADVANCE=3");
        module.Add("");

        // MAIN PROCEDURE
        // Instructions
        if (instructionLines.Count != 0)
        {
            module.AddRange(instructionLines);
            module.Add("");
        }

        module.Add("END");
        return module;
    }

    private void GenrateData(RobotCursor writer, bool inlineTargets, List<Action> actions)
    {
        // Use the write RobotCursor to generate the data
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
                if (GenerateInstructionDeclaration(a, writer, out line))
                {
                    instructionLines.Add(line);
                }
            }
            else
            {
                if (GenerateVariableDeclaration(a, writer, it, out line))  // there will be a number jump on target-less instructions, but oh well...
                {
                    declarationLines.Add(line);
                }

                if (GenerateVariableInitialization(a, writer, it, out line))
                {
                    initializationLines.Add(line);
                }

                if (GenerateInstructionDeclarationFromVariable(a, writer, it, out line))  // there will be a number jump on target-less instructions, but oh well...
                {
                    instructionLines.Add(line);
                }
            }

            // Move on
            it++;
        }
    }

    private void AddinitializationLines(string programName)
    {
        //BCO movement of the robot to allow movement after the BCO
        // Declaring the current position variable
        initializationLines.Add("DECL axis joint_pos_tgt");
        initializationLines.Add("DECL POS current_position");
        initializationLines.Add("DECL POS IK_pose_position");

        initializationLines.Add(";FOLD INI");  // legacy support for user-programming safety
        initializationLines.Add(";FOLD BCO INI");  // legacy support for user-programming safety
        initializationLines.Add("GLOBAL INTERRUPT DECL 3 WHEN $STOPMESS==TRUE DO IR_STOPM ( )");  // legacy support for user-programming safety



        initializationLines.Add("INTERRUPT ON 3");
        initializationLines.Add("BAS (#INITMOV,0 )");  // use base function to initialize sys vars to defaults
        initializationLines.Add(";ENDFOLD (BCO INI)");
        initializationLines.Add(";ENDFOLD (INI)");
        initializationLines.Add("");

        // excecuting the BCO movment
        initializationLines.Add("joint_pos_tgt = $axis_act_meas");
        initializationLines.Add("PTP joint_pos_tgt");

        // excecuting the Status Turn (Robot IK configuration setting)
        initializationLines.Add("current_position = $POS_ACT_MES");
        initializationLines.Add("IK_pose_position = {X 0,Y 0,Z 0,A 0,B 0,C 0, S 'B 010'}");
        initializationLines.Add("IK_pose_position.X = current_position.X");
        initializationLines.Add("IK_pose_position.Y = current_position.Y");
        initializationLines.Add("IK_pose_position.Z = current_position.Z");
        initializationLines.Add("IK_pose_position.A = current_position.A");
        initializationLines.Add("IK_pose_position.B = current_position.B");
        initializationLines.Add("IK_pose_position.C = current_position.C");
        initializationLines.Add("PTP IK_pose_position C_PTP");

        // adding the disclamer messages to the program @Arastoo
        initializationLines.AddRange(GenerateDisclaimerHeader(programName));
    }

    #endregion

}
