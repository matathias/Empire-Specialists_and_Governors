using FactionColonies;
using Verse;

namespace FactionColonies.Specialists
{
    public static class LogSG
    {
        private const string Slug = "[Empire-Specialists]";

        public static void Message(string message)
        {
            if (FCSSettings.PrintDebug)
                Log.Message($"{Slug} {message}");
        }
        public static void MessageForce(string message)
        {
            Log.Message($"{Slug} {message}");
        }

        public static void Warning(string message)
        {
            Log.Warning($"{Slug} {message}");
        }

        public static void Error(string message)
        {
            Log.Error($"{Slug} {message}");
        }
    }
}
