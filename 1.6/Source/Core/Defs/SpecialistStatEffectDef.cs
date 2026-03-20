using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public class SpecialistStatEffectDef : Def
    {
        public SkillDef skill;
        public FCStatDef stat;
        public float specialistValuePerLevel = 0.05f;
        public SpecialistRole roleFilter = SpecialistRole.Specialist;

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
            if (stat == null)
            {
                yield return defName + ": stat is null";
            }
        }
    }
}
