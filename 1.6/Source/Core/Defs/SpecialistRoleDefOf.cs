using RimWorld;

namespace FactionColonies.Specialists
{
    [DefOf]
    public static class SpecialistRoleDefOf
    {
        public static SpecialistRoleDef Generalist;
        public static SpecialistRoleDef Apothecary;
        public static SpecialistRoleDef Commander;

        static SpecialistRoleDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SpecialistRoleDefOf));
        }
    }
}
