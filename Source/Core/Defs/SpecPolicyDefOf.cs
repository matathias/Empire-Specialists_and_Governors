using RimWorld;

namespace FactionColonies.Specialists
{
    [DefOf]
    public static class SpecPolicyDefOf
    {
        public static FCPolicyDef FCSspecialistCorps;
        public static FCPolicyDef FCSmeritocratic;
        public static FCPolicyDef FCSprofessionalArmy;
        public static FCPolicyDef FCSpatrician;
        public static FCPolicyDef FCSgarrisonDoctrine;
        
        static SpecPolicyDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SpecPolicyDefOf));
        }
    }
}