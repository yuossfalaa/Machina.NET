using System;
using System.Collections.Generic;

namespace Machina
{
    /// <summary>
    /// A buffer manager for Settings objects.
    /// </summary>
    class SettingsBuffer
    {
        private static int limit = 32;
        private List<Settings> buffer = new List<Settings>();

        // Keep a copy of the last pop to check changes in RobotCursor state
        private Settings _lastPoppped;
        public Settings LastPopped => _lastPoppped;

        private Settings _settingsBeforeLastPopped;
        public Settings SettingsBeforeLastPop => _settingsBeforeLastPopped;

        // Constructor
        public SettingsBuffer() { }

        public bool Push(RobotCursor cursor)
        {
            if (buffer.Count >= limit)
            {
                cursor.logger.Error("Too many Pushes without Pops?");
                throw new InvalidOperationException("TOO MANY PUSHES WITHOUT POPS?");
            }
            buffer.Add(cursor.GetSettings());
            return true;
        }

        public Settings Pop(RobotCursor cursor)
        {
            if (buffer.Count == 0)
            {
                return null; 
            }

            _settingsBeforeLastPopped = cursor.GetSettings();
            _lastPoppped = buffer[^1];
            buffer.RemoveAt(buffer.Count - 1);
            return _lastPoppped;
        }

        public void DebugBuffer()
        {
            int it = 0;
            foreach (Settings s in buffer) Logger.Debug(it++ + ": " + s);
        }

    }
}
