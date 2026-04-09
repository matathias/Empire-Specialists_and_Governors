using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public class GovernorFocusDef : Def
    {
        public List<SkillWeight> skillWeights;
        public List<FCStatModifier> statModifiers;
        public List<ResourceProductionBonus> resourceBonuses;
        public bool providesBaselineProduction = false;
        public float baselineProductionValue = 0.005f;

        public float ComputeSkillScore(Pawn pawn)
        {
            if (pawn?.skills is null) return 0f;

            float score = 0f;
            bool socialHandled = false;

            if (skillWeights is object)
            {
                foreach (SkillWeight sw in skillWeights)
                {
                    if (sw.skill is null) continue;
                    SkillRecord rec = pawn.skills.GetSkill(sw.skill);
                    if (rec is null) continue;

                    if (sw.skill == SkillDefOf.Social)
                    {
                        float effectiveWeight = sw.weight < 1f ? 1f : sw.weight;
                        score += rec.Level * effectiveWeight;
                        socialHandled = true;
                    }
                    else
                    {
                        score += rec.Level * sw.weight;
                    }
                }
            }

            if (!socialHandled)
            {
                SkillRecord social = pawn.skills.GetSkill(SkillDefOf.Social);
                if (social is object)
                    score += social.Level * 1f;
            }

            return score;
        }

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
