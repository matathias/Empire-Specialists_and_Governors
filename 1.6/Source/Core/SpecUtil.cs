using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public static class SpecUtil
    {
        public static double SpecialistAdditiveForResource(SettlementSpecialist s, ResourceTypeDef resourceDef)
        {
            if (s is null || s.role is null || !s.HasUsableSkills) return 0;
            float score = s.SkillScore;
            double bonus = 0;

            if (s.role.resourceBonuses is object)
            {
                foreach (ResourceProductionBonus rpb in s.role.resourceBonuses)
                {
                    if (rpb.resource == resourceDef)
                        bonus += rpb.baseValue * score;
                }
            }

            if (s.role.providesBaselineProduction)
                bonus += s.role.baselineProductionValue * score;

            return bonus;
        }

        public static double GovernorMultiplierForResource(SettlementGovernor g, ResourceTypeDef resourceDef)
        {
            if (g is null || g.focus is null || !g.HasUsableSkills) return 1.0;
            float score = g.SkillScore;
            double multiplier = 1.0;

            if (g.focus.resourceBonuses is object)
            {
                foreach (ResourceProductionBonus rpb in g.focus.resourceBonuses)
                {
                    if (rpb.resource == resourceDef)
                        multiplier += rpb.baseValue * score;
                }
            }

            if (g.focus.providesBaselineProduction)
                multiplier *= (1.0 + g.focus.baselineProductionValue * score);

            return multiplier;
        }

        public static double SpecialistStatBonus(SettlementSpecialist s, FCStatDef stat)
        {
            if (s is null || s.role is null || s.role.statModifiers is null || !s.HasUsableSkills) return 0;
            float score = s.SkillScore;
            double bonus = 0;
            foreach (FCStatModifier mod in s.role.statModifiers)
            {
                if (mod.stat == stat)
                    bonus += mod.value * score;
            }
            return bonus;
        }

        public static double GovernorStatMultiplier(SettlementGovernor g, FCStatDef stat)
        {
            if (g is null || g.focus is null || g.focus.statModifiers is null || !g.HasUsableSkills) return 1.0;
            float score = g.SkillScore;
            double mult = 1.0;
            foreach (FCStatModifier mod in g.focus.statModifiers)
            {
                if (mod.stat == stat)
                    mult += mod.value * score;
            }
            return mult;
        }

        public static double SpecialistUpkeep(SettlementSpecialist s)
        {
            if (s is null || s.role is null) return 0;
            return s.role.baseUpkeepSilver + (s.SkillScore * s.role.skillUpkeepScaling);
        }

        public static int WorkerBonusFromResidents(int liveResidentCount)
        {
            if (FCSSettings.residentsPerWorker <= 0) return 0;
            return (int)Math.Floor(liveResidentCount / (double)FCSSettings.residentsPerWorker);
        }

        public static bool HasTrait(FCPolicyDef def)
        {
            return def is object && FactionCache.FactionComp.HasTrait(def);
        }

        public static float XPPerDay()
        {
            float xp = FCSSettings.xpPerDay;
            if (HasTrait(SpecPolicyDefOf.FCSspecialistCorps)) xp *= 2f;
            if (HasTrait(SpecPolicyDefOf.FCSmeritocratic)) xp *= 1.5f;
            return xp;
        }

        public static double MedicalHealRateBonus(List<SettlementSpecialist> specialists, SettlementGovernor governor)
        {
            double bonus = 0;

            foreach (SettlementSpecialist s in specialists)
            {
                if (s.role is null || !s.HasUsableSkills) continue;
                RoleBehaviorExt_Apothecary ext = s.role.GetModExtension<RoleBehaviorExt_Apothecary>();
                if (ext is null) continue;
                bonus += s.SkillScore * ext.healRatePerSkillPoint;
            }

            if (governor is object && governor.HasUsableSkills)
            {
                SkillRecord govMed = governor.pawn.skills.GetSkill(SkillDefOf.Medicine);
                if (govMed is object && govMed.Level > 0)
                    bonus += govMed.Level * FCSSettings.healRatePerLevelGovernor;
            }

            return bonus;
        }

        public static void SendDeathLetter(string roleLabel, string bodyText)
        {
            string label = "FCS_LetterSpecialistKilled".Translate();
            Find.LetterStack.ReceiveLetter(label, bodyText, LetterDefOf.Death);
        }
    }
}
