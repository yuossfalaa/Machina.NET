namespace Machina;

/// <summary>
/// A class to bind Machina logging information to custom outputs via events.
/// </summary>
public static class Logger
{

    /// <summary>
    /// Subscribe to this event to receive formatted logging messages. 
    /// Designed to be linked to stdouts, like: "Machina.Logger.WriteLine += Console.WriteLine;"
    /// </summary>
    public static event WriteLineHandler WriteLine;
    public delegate void WriteLineHandler(string msg);

    /// <summary>
    /// Subscribe to this event to receive logging messages, including source and levels. 
    /// All messages will be broadcasted to this logger, regardless of level.
    /// </summary>
    public static event CustomLoggingHandler CustomLogging;
    public delegate void CustomLoggingHandler(LoggerArgs e);

    /// <summary>
    /// Define the level of logging desired for the WriteLine logger: 0 None, 1 Error, 2 Warning, 3 Info (default), 4 Verbose or 5 Debug.
    /// </summary>
    /// <param name="level"></param>
    public static void SetLogLevel(int level)
    {
        _logLevel = (LogLevel)level;
    }

    /// <summary>
    /// Define the level of logging desired for the WriteLine logger: None, Error, Warning, Info (default), Verbose or Debug.
    /// </summary>
    /// <param name="level"></param>
    public static void SetLogLevel(LogLevel level)
    {
        _logLevel = level;
    }

    /// <summary>
    /// Level of logging for WriteLine. CustomLogging will catch them all.
    /// </summary>
    private static LogLevel _logLevel = LogLevel.INFO;


    public static void Error(string msg)
    {
        OnCustomLogging(new LoggerArgs(null, LogLevel.ERROR, msg));
    }

    public static void Warning(string msg)
    {
        OnCustomLogging(new LoggerArgs(null, LogLevel.WARNING, msg));
    }

    public static void Info(string msg)
    {
        OnCustomLogging(new LoggerArgs(null, LogLevel.INFO, msg));
    }

    public static void Verbose(string msg)
    {
        OnCustomLogging(new LoggerArgs(null, LogLevel.VERBOSE, msg));
    }

    public static void Debug(string msg)
    {
        OnCustomLogging(new LoggerArgs(null, LogLevel.DEBUG, msg));
    }
    
    internal static void OnCustomLogging(LoggerArgs e)
    {
        CustomLogging?.Invoke(e);

        // Try sending it to the simpler WriteLine event
        OnWriteLine(e);
    }

    internal static void OnWriteLine(LoggerArgs e)
    {
        if (WriteLine != null && e.Level <= _logLevel)
        { 
            WriteLine.Invoke(e.ToString());
        }
    }
}
