using RimWorld;

namespace FactionColonies.Specialists
{
    [DefOf]
    public static class GovernorFocusDefOf
    {
        public static GovernorFocusDef Balanced;

        static GovernorFocusDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(GovernorFocusDefOf));
        }
    }
}
