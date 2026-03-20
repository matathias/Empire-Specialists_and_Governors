using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public class SpecialistSkillWeightDef : Def
    {
        public SkillDef skill;
        public float specialistAdditivePerLevel = 0.05f;
        public float governorMultiplierPerLevel = 0.008f;

        private static Dictionary<SkillDef, SpecialistSkillWeightDef> cache;

        public static SpecialistSkillWeightDef ForSkill(SkillDef skill)
        {
            if (cache == null)
            {
                cache = new Dictionary<SkillDef, SpecialistSkillWeightDef>();
                foreach (SpecialistSkillWeightDef def in DefDatabase<SpecialistSkillWeightDef>.AllDefs)
                {
                    if (def.skill != null)
                    {
                        cache[def.skill] = def;
                    }
                }
            }

            SpecialistSkillWeightDef result;
            cache.TryGetValue(skill, out result);
            return result;
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
            {
                yield return err;
            }
            if (skill == null)
            {
                yield return defName + ": skill is null";
            }
        }
    }
}
