namespace Machina;

/// <summary>
/// Custom logging arguments
/// </summary>
public class LoggerArgs
{

    public object Sender { get; }
    public LogLevel Level { get; }
    public string Message { get; }


    public LoggerArgs(object sender, int level, string msg)
    {
        Sender = sender;
        Level = (LogLevel)level;
        Message = msg;
    }

    public LoggerArgs(object sender, LogLevel level, string msg)
    {
        Sender = sender;
        Level = level;
        Message = msg;
    }

    /// <summary>
    /// Formatted representation of this object.
    /// </summary>
    /// <returns></returns>
    public override string ToString()
    {
        string sender = "Machina";

        if (Sender is Robot)
        {
            Robot b = Sender as Robot;
            sender = b.Name;
        }

        return string.Format("{0} {1}: {2}",
            sender,
            Level,
            Message);
    }
}
