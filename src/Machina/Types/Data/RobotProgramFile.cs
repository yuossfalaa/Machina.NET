using System;
using System.Collections.Generic;
using System.Text;

namespace Machina.Types.Data;

/// <summary>
/// Represents a file inside a RobotProgram. Includes filename, extension and content as a string List.
/// </summary>
public class RobotProgramFile
{

    public string Name { get; }
    public string Extension { get; }
    internal Encoding Encoding { get; }
    internal char CommentChar { get; }

    internal List<string> Lines { get; private set; }

    internal RobotProgramFile(string name, string extension, Encoding encoding, char commentChar)
    {
        this.Name = name;
        this.Extension = extension;
        this.Encoding = encoding;
        this.CommentChar = commentChar;
    }

    internal void SetContent(List<string> lines)
    {
        this.Lines = lines;
    }
     
    public override string ToString()
    {
        return $"Robot Program File \"{Name}.{Extension}\" with {Lines.Count} lines of code.";
    }

    internal List<string> ToStringList()
    {
        List<string> lines = [.. GetHeader(), .. Lines, .. GetFooter(), ""];
        return lines;
    }

    private List<string> GetHeader()
    {
        string ccline = new String(CommentChar, 65);

        List<string> header =
        [
            ccline,
            $"{CommentChar}{CommentChar} START OF FILE \"{Name}.{Extension}\"",
            ccline,
            "",
        ];
        return header;
    }

    private List<string> GetFooter()
    {
        string ccline = new String(CommentChar, 65);

        List<string> footer =
        [
            ccline,
            $"{CommentChar}{CommentChar} END OF FILE \"{Name}.{Extension}\"",
            ccline,
            "",
        ];
        return footer;
    }
}
