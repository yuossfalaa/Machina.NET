﻿using System.Collections.Generic;
using System.Text;
using Machina.Types.Data;

namespace Machina;

/// <summary>
/// A quick compiler for human-readable instructions.
/// </summary>
internal class CompilerHuman : Compiler
{
    #region Variables
    internal override Encoding Encoding => Encoding.UTF8;

    internal override char CC => '/';
    #endregion

    #region ctor
    internal CompilerHuman() : base() { }

    #endregion

    #region Public Methods
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

            line = string.Format("[{0}] {1}", it, a.ToString());
            actionLines.Add(line);

            // Move on
            it++;
        }


        // PROGRAM ASSEMBLY
        // Initialize a module list
        List<string> module = new List<string>();

        // Banner
        module.AddRange(GenerateDisclaimerHeader(programName));
        module.Add("");

        // Code lines
        module.AddRange(actionLines);

        // MAIN file
        RobotProgramFile pFile = new RobotProgramFile(programName, "txt", Encoding, CC);
        pFile.SetContent(module);
        robotProgram.Add(pFile);

        return robotProgram;
    } 
    #endregion
}
