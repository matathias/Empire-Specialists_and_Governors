using RimWorld;
using System;
using Verse;

namespace FactionColonies.Specialists
{
    public static class SpecUtil
    {
        public static float XPPerDay()
        {
            float xp = FCSSettings.xpPerDay;
            if (HasTrait(SpecPolicyDefOf.FCSspecialistCorps)) xp *= 2f;
            if (HasTrait(SpecPolicyDefOf.FCSmeritocratic)) xp *= 1.5f;

            return xp;
        }

        public static double GetMilBonus(int meleeLevel, int shootingLevel)
        {
            return Math.Max(meleeLevel, shootingLevel) * 0.05;
        }

        public static double GetMilBonus(Pawn pawn)
        {
            SkillRecord melee = pawn?.skills?.GetSkill(SkillDefOf.Melee);
            SkillRecord shooting = pawn?.skills?.GetSkill(SkillDefOf.Shooting);
            return GetMilBonus(melee?.Level ?? 0, shooting?.Level ?? 0);
        }

        public static double GetMilBonus(SpecialistFC s)
        {
            return GetMilBonus(s?.pawn);
        }

        public static bool HasTrait(FCPolicyDef def)
        {
            return def != null && FactionCache.FactionComp.HasTrait(def);
        }

        public static double GovSocialFactor(int socialLevel)
        {
            bool meritocratic = HasTrait(SpecPolicyDefOf.FCSmeritocratic);
            double socialFactor = meritocratic
                ? 0.75 + (socialLevel / 16.0)
                : 0.5 + (socialLevel / 20.0);
            return socialFactor;
        }

        public static double FocusBonusMultiplier()
        {
            return HasTrait(SpecPolicyDefOf.FCSmeritocratic) ? 2.0 : 1.5;
        }

        public static string GovSocialFactorDesc(int socialLevel)
        {
            bool meritocratic = HasTrait(SpecPolicyDefOf.FCSmeritocratic);
            return meritocratic ? $"0.75 + ({socialLevel} / 16)"
                                : $"0.5 + ({socialLevel} / 20)";
        }

        public static int WorkerBonusFromResidents(int liveResidentCount)
        {
            if (FCSSettings.residentsPerWorker <= 0) return 0;
            return (int)Math.Floor(liveResidentCount / (double)FCSSettings.residentsPerWorker);
        }

        public static double SpecialistAdditiveForResource(Pawn pawn, ResourceFC resource)
        {
            if (pawn?.skills is null || resource.def.associatedSkills is null) return 0;
            double bonus = 0;
            foreach (SkillDef skillDef in resource.def.associatedSkills)
            {
                SpecialistSkillWeightDef weight = SpecialistsCache.SkillWeight(skillDef);
                if (weight is null) continue;
                SkillRecord skill = pawn.skills.GetSkill(skillDef);
                if (skill != null)
                    bonus += skill.Level * weight.specialistAdditivePerLevel;
            }
            return bonus;
        }

        public static double GovernorRawMultiplierForResource(Pawn pawn, ResourceFC resource)
        {
            if (pawn?.skills is null || resource.def.associatedSkills is null) return 0;
            double raw = 0;
            foreach (SkillDef skillDef in resource.def.associatedSkills)
            {
                SpecialistSkillWeightDef weight = SpecialistsCache.SkillWeight(skillDef);
                if (weight is null) continue;
                SkillRecord skill = pawn.skills.GetSkill(skillDef);
                if (skill != null)
                    raw += skill.Level * weight.governorMultiplierPerLevel;
            }
            return raw;
        }

        public static double PawnSkillSum(Pawn pawn)
        {
            if (pawn?.skills?.skills is null) return 0;
            double sum = 0;
            foreach (SkillRecord sk in pawn.skills.skills)
                sum += sk.Level;
            return sum;
        }

        public static double BaseUpkeep(double skillSum)
        {
            return FCSSettings.specialistBaseCost + (skillSum / FCSSettings.skillDivisor) * FCSSettings.scalingFactor;
        }

        public static double GovernorUpkeepMultiplier()
        {
            return HasTrait(SpecPolicyDefOf.FCSmeritocratic) ? 3.0 : 2.0;
        }

        public static void SendDeathLetter(SpecialistRole role, string bodyText)
        {
            string label = role == SpecialistRole.Governor
                ? "FCS_LetterGovernorKilled".Translate() : "FCS_LetterSpecialistKilled".Translate();
            Find.LetterStack.ReceiveLetter(label, bodyText, LetterDefOf.Death);
        }
    }
}