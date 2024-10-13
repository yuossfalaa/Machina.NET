namespace Machina;

/// <summary>
/// A "Console" class that can be attached to objects to track their log messages.
/// </summary>
public class RobotLogger
{
    internal object _sender;

    internal RobotLogger(object sender)
    {
        _sender = sender;
    }

    public void Error(string msg) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.ERROR, msg));
    public void Error(object obj) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.ERROR, obj.ToString()));

    public void Warning(string msg) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.WARNING, msg));
    public void Warning(object obj) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.WARNING, obj.ToString()));

    public void Info(string msg) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.INFO, msg));
    public void Info(object obj) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.INFO, obj.ToString()));

    public void Verbose(string msg) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.VERBOSE, msg));
    public void Verbose(object obj) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.VERBOSE, obj.ToString()));

    public void Debug(string msg) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.DEBUG, msg));
    public void Debug(object obj) => Logger.OnCustomLogging(new LoggerArgs(_sender, LogLevel.DEBUG, obj.ToString()));







}
