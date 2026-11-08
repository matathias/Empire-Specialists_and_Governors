namespace FactionColonies.Specialists;

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

    /* Weighted, tapered skill score for a pawn in this role: how good a fit the pawn is. Also
       backs SettlementSpecialist's cached skill score, so auto-assignment and the live score agree. */
    public float ComputeSkillScore(Pawn pawn)
    {
        if (pawn?.skills is null || skillWeights is null) return 0f;
        float score = 0f;
        foreach (SkillWeight sw in skillWeights)
        {
            if (sw.skill is null) continue;
            SkillRecord rec = pawn.skills.GetSkill(sw.skill);
            if (rec is not null)
                score += SpecUtil.TaperedLevel(rec.Level) * sw.weight;
        }
        return score;
    }

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string err in base.ConfigErrors())
            yield return err;

        if (skillWeights is not null)
        {
            for (int i = 0; i < skillWeights.Count; i++)
            {
                if (skillWeights[i].skill is null)
                    yield return defName + ": skillWeights[" + i + "] has null skill";
            }
        }

        if (resourceBonuses is not null)
        {
            for (int i = 0; i < resourceBonuses.Count; i++)
            {
                if (resourceBonuses[i].resource is null)
                    yield return defName + ": resourceBonuses[" + i + "] has null resource";
            }
        }

        if (statModifiers is not null)
        {
            foreach (string err in FCStatModifier.ConfigErrors(statModifiers, defName))
                yield return err;

            // Role stat modifiers only ever feed settlement-scoped aggregation (via the
            // settlement comp's IStatModifierProvider). A stat that doesn't apply to
            // settlements is silently dead here, so flag it at load.
            for (int i = 0; i < statModifiers.Count; i++)
            {
                if (statModifiers[i].stat is not null && !statModifiers[i].stat.appliesToSettlements)
                    yield return defName + ": statModifiers[" + i + "] stat '" + statModifiers[i].stat.defName
                        + "' is not appliesToSettlements, so it can never apply to a specialist role";
            }
        }
    }
}
