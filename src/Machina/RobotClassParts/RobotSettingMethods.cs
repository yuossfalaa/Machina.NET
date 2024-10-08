﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Machina.Attributes;

namespace Machina;

// robot part with general Setting Methods
public partial class Robot
{
    #region Setting Methods
    /// <summary>
    /// Buffers current state settings (speed, precision, motion type...), and opens up for 
    /// temporary settings changes to be reverted by PopSettings().
    /// </summary>
    [ParseableFromString]
    public bool PushSettings()
    {
        return _control.IssueActionManager.IssuePushPopRequest(true);
    }

    /// <summary>
    /// Reverts the state settings (speed, precision, motion type...) to the previously buffered
    /// state by PushSettings().
    /// </summary>
    [ParseableFromString]
    public bool PopSettings()
    {
        return _control.IssueActionManager.IssuePushPopRequest(false);
    }

    #endregion
}
