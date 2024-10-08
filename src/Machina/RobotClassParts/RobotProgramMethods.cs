using Machina.Types.Data;

namespace Machina;

// robot part with program methods in it
public partial class Robot
{

    #region Robot Program Methods
    /// <summary>
    /// Create a program in the device's native language with all the buffered Actions and return it as a RobotProgram,
    /// representing the different program files. Note all buffered Actions will be removed from the queue.
    /// </summary>
    /// <param name="inlineTargets">Write inline targets on action statements, or declare them as independent variables?</param>
    /// <param name="humanComments">If true, a human-readable description will be added to each line of code</param>
    /// <returns></returns>
    public RobotProgram Compile(bool inlineTargets = true, bool humanComments = true)
    {
        return _control.Export(inlineTargets, humanComments);
    }

    /// <summary>
    /// Saves a RobotProgram to a folder in the system, generating all the required files and extensions.
    /// </summary>
    /// <param name="program"></param>
    /// <param name="folderPath"></param>
    /// <returns></returns>
    public bool SaveProgram(RobotProgram program, string folderPath)
    {
        return program.SaveToFolder(folderPath, logger);
    }

    #endregion
}
