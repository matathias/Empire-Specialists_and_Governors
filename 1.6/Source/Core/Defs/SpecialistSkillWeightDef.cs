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
