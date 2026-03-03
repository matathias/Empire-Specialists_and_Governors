using FactionColonies;
using Verse;

namespace FactionColonies.Specialists
{
    public static class LogUtil
    {
        public const string slug = "[Empire-Specialists]";

        public static void LogMessage(string message, LogMessageType messageType = LogMessageType.Message, bool forceLog = false, bool errorOnce = false, int errorOnceKey = 0)
        {
            switch (messageType)
            {
                case LogMessageType.Message:
                    if (FCSSettings.PrintDebug || forceLog)
                    {
                        Log.Message(slug + " " + message);
                    }
                    break;
                case LogMessageType.Warning:
                    Log.Warning(slug + " " + message);
                    break;
                case LogMessageType.Error:
                    if (errorOnce)
                    {
                        Log.ErrorOnce(slug + " " + message, errorOnceKey);
                    }
                    else
                    {
                        Log.Error(slug + " " + message);
                    }
                    break;
            }
        }

        public static void Message(string message)
        {
            LogMessage(message, LogMessageType.Message);
        }
        /// <summary>
        /// Prints a non-warning, non-error message to the log even if the user has disabled Verbose Logging.
        /// </summary>
        public static void MessageForce(string message)
        {
            LogMessage(message, LogMessageType.Message, true);
        }
        public static void Warning(string message)
        {
            LogMessage(message, LogMessageType.Warning);
        }
        public static void Error(string message)
        {
            LogMessage(message, LogMessageType.Error);
        }
        public static void ErrorOnce(string message, int key)
        {
            LogMessage(message, LogMessageType.Error, true, true, key);
        }
    }
}
