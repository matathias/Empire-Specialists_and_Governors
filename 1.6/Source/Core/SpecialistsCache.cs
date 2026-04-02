using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public static class SpecialistsCache
    {
        private static Dictionary<SkillDef, SpecialistSkillWeightDef> _skillWeights;
        private static Dictionary<FCStatDef, List<SpecialistStatEffectDef>> _statEffectsByStat;
        private static Dictionary<string, FCPolicyDef> _traitDefs;

        public static SpecialistSkillWeightDef SkillWeight(SkillDef skill)
        {
            if (_skillWeights == null)
            {
                _skillWeights = new Dictionary<SkillDef, SpecialistSkillWeightDef>();
                foreach (SpecialistSkillWeightDef def in DefDatabase<SpecialistSkillWeightDef>.AllDefs)
                {
                    if (def.skill != null)
                    {
                        _skillWeights[def.skill] = def;
                    }
                }
            }
            _skillWeights.TryGetValue(skill, out SpecialistSkillWeightDef result);
            return result;
        }

        public static List<SpecialistStatEffectDef> StatEffectsForStat(FCStatDef stat)
        {
            if (_statEffectsByStat == null)
            {
                _statEffectsByStat = new Dictionary<FCStatDef, List<SpecialistStatEffectDef>>();
                foreach (SpecialistStatEffectDef def in DefDatabase<SpecialistStatEffectDef>.AllDefs)
                {
                    if (def.stat == null) continue;
                    if (!_statEffectsByStat.TryGetValue(def.stat, out List<SpecialistStatEffectDef> list))
                    {
                        list = new List<SpecialistStatEffectDef>();
                        _statEffectsByStat[def.stat] = list;
                    }
                    list.Add(def);
                }
            }
            _statEffectsByStat.TryGetValue(stat, out List<SpecialistStatEffectDef> result);
            return result;
        }

        public static FCPolicyDef TraitDef(string defName)
        {
            if (_traitDefs == null)
            {
                _traitDefs = new Dictionary<string, FCPolicyDef>();
            }
            if (!_traitDefs.TryGetValue(defName, out FCPolicyDef result))
            {
                result = DefDatabase<FCPolicyDef>.GetNamedSilentFail(defName);
                _traitDefs[defName] = result;
            }
            return result;
        }

        public static void InvalidateCache()
        {
            _skillWeights = null;
            _statEffectsByStat = null;
            _traitDefs = null;
        }
    }
}
