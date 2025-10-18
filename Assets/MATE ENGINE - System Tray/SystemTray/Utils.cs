using System;
using System.Collections.Generic;

namespace Utils
{
    public static partial class TrayIcon
    {
        private static ushort _id = 0;

        private static ushort GetUniqueID()
        {
            if (_id == 0)
            {
                long ticks = DateTime.UtcNow.Ticks;
                _id = (ushort)(ticks % ushort.MaxValue);
            }
            return ++_id;
        }

        private static void ProcessMenuActions(List<(string, Action)> actions)
        {
            MenuActions = new Dictionary<string, Action>();
            ActionMappings = new Dictionary<uint, string>();

            if (actions == null)
                return;

            foreach (var (label, callback) in actions)
            {
                if (label == LEFT_CLICK)
                {
                    OnLeftClick = callback;
                    continue;
                }

                uint uid = GetUniqueID();
                ActionMappings[uid] = label;
                MenuActions[label] = callback;
            }
        }

        private static string TruncateString(string str, int maxLength)
        {
            if (string.IsNullOrEmpty(str)) return "";
            return str.Length < maxLength ? str : str.Substring(0, maxLength - 1);
        }
    }
}
