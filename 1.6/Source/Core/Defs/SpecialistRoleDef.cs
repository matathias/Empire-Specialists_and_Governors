using System.Collections.Generic;
using Verse;

namespace FactionColonies.Specialists
{
    public class SpecialistRoleDef : Def
    {
        public List<SkillWeight> skillWeights;
        public List<ResourceProductionBonus> resourceBonuses;
        public List<FCStatModifier> statModifiers;
        public int maxPerSettlement = 0;
        public bool isGeneralist = false;
        public bool providesBaselineProduction = false;
        public float baselineProductionValue = 0.02f;
        public float baseUpkeepSilver = 0f;
        public float skillUpkeepScaling = 0f;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;

            if (skillWeights is object)
            {
                for (int i = 0; i < skillWeights.Count; i++)
                {
                    if (skillWeights[i].skill is null)
                        yield return defName + ": skillWeights[" + i + "] has null skill";
                }
            }

            if (resourceBonuses is object)
            {
                for (int i = 0; i < resourceBonuses.Count; i++)
                {
                    if (resourceBonuses[i].resource is null)
                        yield return defName + ": resourceBonuses[" + i + "] has null resource";
                }
            }

            if (statModifiers is object)
            {
                foreach (string err in FCStatModifier.ConfigErrors(statModifiers, defName))
                    yield return err;
            }
        }
    }
}
