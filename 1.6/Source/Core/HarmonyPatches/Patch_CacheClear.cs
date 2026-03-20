using HarmonyLib;
using Verse;

namespace FactionColonies.Specialists
{
    [HarmonyPatch(typeof(Game), "Dispose")]
    public static class Patch_GameDispose_ClearCache
    {
        public static void Postfix()
        {
            SpecialistsCache.InvalidateCache();
        }
    }
}
