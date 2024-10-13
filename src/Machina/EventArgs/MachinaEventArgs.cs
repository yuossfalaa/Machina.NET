using System;

namespace Machina;

public abstract class MachinaEventArgs : EventArgs
{
    /// <summary>
    /// The arguments on this event must be serializable to a JSON object.
    /// </summary>
    /// <returns></returns>
    public abstract string ToJSONString();

}
