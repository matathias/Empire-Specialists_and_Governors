using RimWorld;
using System;

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

        public static double GetMilBonus(SpecialistFC s)
        {
            SkillRecord melee = s?.pawn?.skills?.GetSkill(SkillDefOf.Melee);
            SkillRecord shooting = s?.pawn?.skills?.GetSkill(SkillDefOf.Shooting);
            return GetMilBonus(melee?.Level ?? 0, shooting?.Level ?? 0);
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

        public static string GovSocialFactorDesc(int socialLevel)
        {
            bool meritocratic = HasTrait(SpecPolicyDefOf.FCSmeritocratic);
            return meritocratic ? $"0.75 + ({socialLevel} / 16)"
                                : $"0.5 + ({socialLevel} / 20)";
        }
    }
}