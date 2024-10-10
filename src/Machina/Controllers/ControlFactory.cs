namespace Machina.Controllers;

/// <summary>
/// A static factory class that creates ControlManagers based on ControlType.
/// </summary>
internal static class ControlFactory
{
    internal static ControlManager GetControlManager(Control control)
    {
        return control.ControlMode switch
        {
            ControlType.Stream or ControlType.Online => new StreamControlManager(control),
            ControlType.Offline => new OfflineControlManager(control),
            _ => null,
        };
    }
}
