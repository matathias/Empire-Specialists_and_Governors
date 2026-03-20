using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public static class SpecialistsCache
    {
        private static Dictionary<SkillDef, SpecialistSkillWeightDef> _skillWeights;
        private static Dictionary<FCStatDef, List<SpecialistStatEffectDef>> _statEffectsByStat;

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
            SpecialistSkillWeightDef result;
            _skillWeights.TryGetValue(skill, out result);
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
                    List<SpecialistStatEffectDef> list;
                    if (!_statEffectsByStat.TryGetValue(def.stat, out list))
                    {
                        list = new List<SpecialistStatEffectDef>();
                        _statEffectsByStat[def.stat] = list;
                    }
                    list.Add(def);
                }
            }
            List<SpecialistStatEffectDef> result;
            _statEffectsByStat.TryGetValue(stat, out result);
            return result;
        }

        public static void InvalidateCache()
        {
            _skillWeights = null;
            _statEffectsByStat = null;
        }
    }
}
