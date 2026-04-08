using RimWorld;
using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies.Specialists
{
    public static class SpecUtil
    {
        /// <summary>
        /// Returns 0 if the skill level is below the configured skill floor for the given role, otherwise returns the level unchanged.
        /// </summary>
        public static int EffectiveLevel(int level, SpecialistRole role)
        {
            int floor;
            switch (role)
            {
                case SpecialistRole.Governor: floor = FCSSettings.skillFloorGovernor; break;
                case SpecialistRole.Defense:  floor = FCSSettings.skillFloorDefense; break;
                default:                      floor = FCSSettings.skillFloorSpecialist; break;
            }
            return level >= floor ? level : 0;
        }
        public static float XPPerDay()
        {
            float xp = FCSSettings.xpPerDay;
            if (HasTrait(SpecPolicyDefOf.FCSspecialistCorps)) xp *= 2f;
            if (HasTrait(SpecPolicyDefOf.FCSmeritocratic)) xp *= 1.5f;

            return xp;
        }

        public static double GetMilBonus(int meleeLevel, int shootingLevel)
        {
            return Math.Max(EffectiveLevel(meleeLevel, SpecialistRole.Defense), EffectiveLevel(shootingLevel, SpecialistRole.Defense)) * 0.05;
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
                    bonus += EffectiveLevel(skill.Level, SpecialistRole.Specialist) * weight.specialistAdditivePerLevel;
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
                    raw += EffectiveLevel(skill.Level, SpecialistRole.Governor) * weight.governorMultiplierPerLevel;
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

        /// <summary>
        /// Computes the total heal rate bonus from specialists' and governor's Medicine skill.
        /// Returns the value to add to 1.0 for the mercHealRateMultiplier stat.
        /// </summary>
        public static double MedicalHealRateBonus(List<SpecialistFC> allPawns, SpecialistFC governor)
        {
            double bonus = 0;

            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Specialist) continue;
                if (!s.HasUsableSkills) continue;
                SkillRecord med = s.pawn.skills.GetSkill(SkillDefOf.Medicine);
                if (med != null)
                {
                    int level = EffectiveLevel(med.Level, SpecialistRole.Specialist);
                    bonus += level * FCSSettings.healRatePerLevelSpecialist;
                }
            }

            if (governor != null && governor.HasUsableSkills)
            {
                SkillRecord govMed = governor.pawn.skills.GetSkill(SkillDefOf.Medicine);
                if (govMed != null)
                {
                    int level = EffectiveLevel(govMed.Level, SpecialistRole.Governor);
                    SkillRecord social = governor.pawn.skills.GetSkill(SkillDefOf.Social);
                    double socialFactor = GovSocialFactor(social?.Level ?? 0);
                    bonus += level * FCSSettings.healRatePerLevelGovernor * socialFactor;
                }
            }

            return bonus;
        }

        public static void SendDeathLetter(SpecialistRole role, string bodyText)
        {
            string label = role == SpecialistRole.Governor
                ? "FCS_LetterGovernorKilled".Translate() : "FCS_LetterSpecialistKilled".Translate();
            Find.LetterStack.ReceiveLetter(label, bodyText, LetterDefOf.Death);
        }
    }
}