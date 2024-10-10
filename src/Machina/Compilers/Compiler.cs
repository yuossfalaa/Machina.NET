using System;
using System.Collections.Generic;
using System.Text;
using Machina.Types.Data;

namespace Machina;

/// <summary>
/// An abstract class that features methods to translate high-level robot actions into
/// platform-specific programs. 
/// </summary>
/// 
internal abstract class Compiler
{
    /// <summary>
    /// Add a trailing action id to each declaration?
    /// </summary>
    internal bool addActionID = false;

    /// <summary>
    /// Add a trailing human representation of the action after the code line
    /// </summary>
    internal bool addActionString = false;

    /// <summary>
    /// Comment character (CC) used for comments by the compiler
    /// </summary>
    internal abstract char CC { get; }

    /// <summary>
    /// Encoding for text files produced by the compiler.
    /// </summary>
    internal abstract Encoding Encoding { get; }

    /// <summary>
    /// An empty constructor really...
    /// </summary>
    internal Compiler() { }

    /// <summary>
    /// Creates a RobotProgram as a textual representation of a set of Actions using a brand-specific RobotCursor.
    /// WARNING: this method is EXTREMELY UNSAFE; it performs no IK calculations, assigns default [0,0,0,0] 
    /// robot configuration and assumes the robot controller will figure out the correct one.
    /// </summary>
    /// <param name="programName"></param>
    /// <param name="writePointer"></param>
    /// <returns></returns>
    public abstract RobotProgram UNSAFEFullProgramFromBuffer(string programName, RobotCursor writer, bool block, bool inlineTargets, bool humanComments);

    public List<String> GenerateDisclaimerHeader(string programName)
    {
        // @TODO: convert this to a StringBuilder
        List<string> header =
        [
            // UTF chars don't convert well to ASCII... :(
            $@"{CC}{CC} ###\   ###\ #####\  ######\##\  ##\##\###\   ##\ #####\ ",
            $@"{CC}{CC} ####\ ####\##\\\##\##\\\\\\##\  ##\##\####\  ##\##\\\##\",
            $@"{CC}{CC} ##\####\##\#######\##\     #######\##\##\##\ ##\#######\",
            $@"{CC}{CC} ##\\##\\##\##\\\##\##\     ##\\\##\##\##\\##\##\##\\\##\",
            $@"{CC}{CC} ##\ \\\ ##\##\  ##\\######\##\  ##\##\##\ \####\##\  ##\",
            $@"{CC}{CC} \\\     \\\\\\  \\\ \\\\\\\\\\  \\\\\\\\\  \\\\\\\\  \\\",
            $"{CC}{CC} ",
            $"{CC}{CC} Program name: {programName}",
            $"{CC}{CC} Created: {DateTime.Now.ToString()}",
            $"{CC}{CC} ",
            $"{CC}{CC} DISCLAIMER: WORKING WITH ROBOTS CAN BE DANGEROUS!",
            $"{CC}{CC} When using robots in a real-time interactive environment, please make sure:",
            $"{CC}{CC}     - You have been adequately trained to use that particular machine,",
            $"{CC}{CC}     - you are in good physical and mental condition,",
            $"{CC}{CC}     - you are operating the robot under the utmost security measures,",
            $"{CC}{CC}     - you are following the facility's and facility staff's security protocols,",
            $"{CC}{CC}     - and the robot has the appropriate guarding in place, including, but not reduced to:",
            $"{CC}{CC}         e -stops, physical barriers, light curtains, etc.",
            $"{CC}{CC} The Machina software framework and its generated code is provided as is;",
            $"{CC}{CC} use at your own risk. This product is not intended for any use that may",
            $"{CC}{CC} involve potential risks of death (including lifesaving equipment),",
            $"{CC}{CC} personal injury, or severe property or environmental damage.",
            $"{CC}{CC} Machina is in a very early stage of development. You are using this software",
            $"{CC}{CC} at your own risk, no warranties are provided herewith, and unexpected",
            $"{CC}{CC} results / bugs may arise during its use. Always test and simulate your",
            $"{CC}{CC} applications thoroughly before running them on a real device.",
            $"{CC}{CC} The author/s shall not be liable for any injuries, damages or losses",
            $"{CC}{CC} consequence of using this software in any way whatsoever.",
            $"{CC}{CC} ",
            $"{CC}{CC} ",
            $"{CC}{CC} Copyright(c) {DateTime.Now.Year} Jose Luis Garcia del Castillo y Lopez",
            $"{CC}{CC} https://github.com/RobotExMachina",
            $"{CC}{CC} ",
            $"{CC}{CC} MIT License",
            $"{CC}{CC} ",
            $"{CC}{CC} Permission is hereby granted, free of charge, to any person obtaining a copy",
            $"{CC}{CC} of this software and associated documentation files(the \"Software\"), to deal",
            $"{CC}{CC} in the Software without restriction, including without limitation the rights",
            $"{CC}{CC} to use, copy, modify, merge, publish, distribute, sublicense, and / or sell",
            $"{CC}{CC} copies of the Software, and to permit persons to whom the Software is",
            $"{CC}{CC} furnished to do so, subject to the following conditions:",
            $"{CC}{CC} ",
            $"{CC}{CC} The above copyright notice and this permission notice shall be included in all",
            $"{CC}{CC} copies or substantial portions of the Software.",
            $"{CC}{CC} ",
            $"{CC}{CC} THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR",
            $"{CC}{CC} IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,",
            $"{CC}{CC} FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE",
            $"{CC}{CC} AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER",
            $"{CC}{CC} LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,",
            $"{CC}{CC} OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE",
            $"{CC}{CC} SOFTWARE.",
            ""
        ];

        return header;
    }
}
