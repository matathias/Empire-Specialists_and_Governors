using System.Collections.Generic;
using Verse;

namespace FactionColonies.Specialists
{
    public static class SpecialistsCache
    {
        private static Dictionary<string, FCPolicyDef> _traitDefs;

        public static FCPolicyDef TraitDef(string defName)
        {
            if (_traitDefs is null)
                _traitDefs = new Dictionary<string, FCPolicyDef>();
            if (!_traitDefs.TryGetValue(defName, out FCPolicyDef result))
            {
                result = DefDatabase<FCPolicyDef>.GetNamedSilentFail(defName);
                _traitDefs[defName] = result;
            }
            return result;
        }

        public static void InvalidateCache()
        {
            _traitDefs = null;
        }
    }
}
