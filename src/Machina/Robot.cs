using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;


namespace Machina;

/// <summary>
/// The core Class in Machina. Represents a state and action-based virtual robot, 
/// and exposes the public API for robot manipulation and control.
/// </summary>

//Note for Developers : This a partial Class all other parts can be found in the folder ./RobotClassParts.
//it was divided to make it easier to edit as it became huge.


//main robot part with all variables and all functions that depend on other functions 
public partial class Robot
{
    #region Private Vars
    /// <summary>
    /// The main Control object, acts as an interface to all classes that
    /// manage robot control.
    /// </summary>
    private Control _control;

    #endregion

    #region Events
    /// <summary>
    /// Will be raised whenever an Action has been successfully issued and is scheduled for release to the device or compilation. 
    /// </summary>
    public event ActionIssuedHandler ActionIssued;
    public delegate void ActionIssuedHandler(object sender, ActionIssuedEventArgs args);
    internal virtual void OnActionIssued(ActionIssuedEventArgs args) => ActionIssued?.Invoke(this, args);

    /// <summary>
    /// Will be raised whenever an Action has been released to the device and is scheduled for execution.
    /// </summary>
    public event ActionReleasedHandler ActionReleased;
    public delegate void ActionReleasedHandler(object sender, ActionReleasedEventArgs args);
    internal virtual void OnActionReleased(ActionReleasedEventArgs args) => ActionReleased?.Invoke(this, args);

    /// <summary>
    /// Will be raised whenever an Action has completed execution on the device. 
    /// </summary>
    public event ActionExecutedHandler ActionExecuted;
    public delegate void ActionExecutedHandler(object sender, ActionExecutedEventArgs args);
    internal virtual void OnActionExecuted(ActionExecutedEventArgs args) => ActionExecuted?.Invoke(this, args);

    /// <summary>
    /// Will be raised whenever new information is available about the real-time information about the state of the device.
    /// </summary>
    public event MotionUpdateHandler MotionUpdate;
    public delegate void MotionUpdateHandler(object sender, MotionUpdateEventArgs args);
    internal virtual void OnMotionUpdate(MotionUpdateEventArgs args) => MotionUpdate?.Invoke(this, args);


    #endregion

    #region Public Vars
    /// <summary>
    /// Build number.
    /// </summary>
    public static readonly int Build = 1600;

    /// <summary>
    /// Version number.
    /// </summary>
    public static readonly string Version = "0.9.0." + Build;

    /// <summary>
    /// A nickname for this Robot.
    /// </summary>
    public string Name { get; internal set; }

    /// <summary>
    /// What brand of robot is this?
    /// </summary>
    public RobotType Brand { get; internal set; }

    /// <summary>
    /// An internal logging class to be used by children objects to log messages from this Robot.
    /// </summary>
    internal RobotLogger logger;

    public RobotLogger Logger => logger;
    #endregion

    #region Constructor Methods
    /// <summary>
    /// Internal constructor.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="make"></param>
    private Robot(string name, RobotType make)
    {
        this.Name = name;
        this.Brand = make;
        this.logger = new RobotLogger(this);

        if (_reflectedAPI == null || _reflectedAPI.Count == 0)
        {
            LoadReflectedAPI();
        }

        _control = new Control(this);
    }


    /// <summary>
    /// Create a new instance of a Robot.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="make"></param>
    /// <returns></returns>
    static public Robot Create(string name, RobotType make)
    {
        if (!Utilities.Strings.IsValidVariableName(name))
        {
            Machina.Logger.Error($"\"{name}\" is not a valid robot name, please start with a letter.");
            return null;
        }

        return new Robot(name, make);
    }

    /// <summary>
    /// Create a new instance of a Robot.
    /// </summary>
    /// <returns></returns>
    static public Robot Create() => Robot.Create("Machina", "HUMAN");

    /// <summary>
    /// Create a new instance of a Robot.
    /// </summary>
    /// <param name="name"></param>
    /// <param name="make"></param>
    /// <returns></returns>
    static public Robot Create(string name, string make)
    {
        try
        {
            RobotType rt = Utilities.Parsing.ParseEnumValue<RobotType>(make);
            return new Robot(name, rt);
        }
        catch
        {
            Machina.Logger.Error($"{make} is not a RobotType, please specify one of the following: ");
            foreach (string str in Enum.GetNames(typeof(RobotType)))
            {
                Machina.Logger.Error(str);
            }
            return null;
        }
    }

    #endregion

    #region Internal Methods

    /// <summary>
    /// A (Name, MethodInfo) dict of reflected methods from the main Robot class, 
    /// that can be invoked from parsed strings from primitive values. 
    /// Such methods are flagged with attributes, and loaded at runtime. 
    /// </summary>
    internal static Dictionary<string, MethodInfo> _reflectedAPI;

    /// <summary>
    /// A (Name, MethodInfo) dict of CASE-INSENSITIVE reflected methods from the main Robot class, 
    /// that can be invoked from parsed strings from primitive values. 
    /// Such methods are flagged with attributes, and loaded at runtime. 
    /// </summary>
    internal static Dictionary<string, MethodInfo> _reflectedAPICaseInsensitive;

    /// <summary>
    /// Used to load all methods from the API that can be parseable from a string, using reflection. 
    /// </summary>
    internal static void LoadReflectedAPI()
    {
        // https://stackoverflow.com/a/14362272/1934487
        Type robotType = typeof(Robot);
        _reflectedAPI = robotType
            .GetMethods()
            .Where(x => x.GetCustomAttributes().OfType<Attributes.ParseableFromStringAttribute>().Any())
            .ToDictionary(y => y.Name);

        // This one is to issue warnings for badly cased instructions.  
        _reflectedAPICaseInsensitive = robotType
            .GetMethods()
            .Where(x => x.GetCustomAttributes().OfType<Attributes.ParseableFromStringAttribute>().Any())
            .ToDictionary(y => y.Name, StringComparer.InvariantCultureIgnoreCase);

        Machina.Logger.Debug("Loaded reflected API");
        foreach (var pair in _reflectedAPI)
        {
            Machina.Logger.Debug(pair.Key + " --> " + pair.Value);
        }
    }
    #endregion
}
