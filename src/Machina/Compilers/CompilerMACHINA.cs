﻿using System.Collections.Generic;
using System.Text;
using Machina.Types.Data;

namespace Machina;
                                                              
/// <summary>
/// A simple compiler that returns a program in native Machina language, 
/// i.e. serializing each Action into its own Instruction form.
/// </summary>
class CompilerMACHINA : Compiler
{
    #region Variables
    internal override Encoding Encoding => Encoding.UTF8;

    internal override char CC => '/';

    #endregion

    #region ctor
    internal CompilerMACHINA() : base() { }
    #endregion

    #region Public Methods
    /// <summary>
    /// Creates a textual program representation of a set of Actions using the Machina Common Language.
    /// </summary>
    /// <param name="programName"></param>
    /// <param name="writePointer"></param>
    /// <param name="block">Use actions in waiting queue or buffer?</param>
    /// <returns></returns>
    public override RobotProgram UNSAFEFullProgramFromBuffer(string programName, RobotCursor writer, bool block, bool inlineTargets, bool humanComments)
    {
        // The program files to be returned
        RobotProgram robotProgram = new RobotProgram(programName, CC);


        // Which pending Actions are used for this program?
        // Copy them without flushing the buffer.
        List<Action> actions = block ?
            writer.actionBuffer.GetBlockPending(false) :
            writer.actionBuffer.GetAllPending(false);


        // ACTION LINES GENERATION
        List<string> actionLines = new List<string>();

        // DATA GENERATION
        // Use the write RobotCursor to generate the data
        int it = 0;
        string line = null;
        foreach (Action a in actions)
        {
            // Move writerCursor to this action state
            writer.ApplyNextAction();  // for the buffer to correctly manage them 

            line = a.ToInstruction();
            actionLines.Add(line);

            // Move on
            it++;
        }

        // PROGRAM ASSEMBLY
        // Initialize a module list
        List<string> module =
        [
            // Banner
            .. GenerateDisclaimerHeader(programName),
            "",
            // Code lines
            .. actionLines,
        ];


        RobotProgramFile mainFile = new RobotProgramFile(programName, "machina", Encoding, CC);
        mainFile.SetContent(module);
        robotProgram.Add(mainFile);

        return robotProgram;
    }

    #endregion
}
