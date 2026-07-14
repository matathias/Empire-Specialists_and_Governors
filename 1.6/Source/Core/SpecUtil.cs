using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public static class SpecUtil
    {
        /* Piecewise per-band scaling of a skill level's contribution to bonuses, so players can
           tune how much low (1-10), mid (11-20), and above-cap (21+) skill levels matter. Each
           band factor scales that band's slope: 1.0 = full, 0 = no contribution. Defaults
           (1, 1, 0.5) leave vanilla 1-20 scaling untouched and halve above 20 — where only
           skill-cap-removing mods reach, since SkillRecord.Level is engine-clamped to 20. */
        public static float TaperedLevel(int level)
        {
            if (level <= 0) return 0f;
            float eff = Math.Min(level, 10) * FCSSettings.skillTaperFactorBand1;
            if (level > 10)
                eff += Math.Min(level - 10, 10) * FCSSettings.skillTaperFactorBand2;
            if (level > 20)
                eff += (level - 20) * FCSSettings.skillTaperFactorBand3;
            return eff;
        }

        /* -*-*-*- Pawn arrival via transport pod: eligibility + auto-role/focus selection -*-*-*- */

        /* A pawn that may be assigned to a settlement (matches Dialog_AssignSpecialists.IsEligible,
           plus IsColonist since pod cargo isn't pre-filtered to caravan colonists). */
        public static bool IsAssignablePawn(Pawn p)
        {
            return p is object
                && p.RaceProps.Humanlike
                && p.IsColonist
                && !p.Downed
                && !p.Dead
                && p.DevelopmentalStage == DevelopmentalStage.Adult
                && !p.IsPrisoner
                && !p.IsSlave;
        }

        /* The specialist role the pawn fits best (highest weighted skill score), skipping the
           Generalist (it's the fallback) and any role already at its per-settlement cap. Falls back
           to Generalist when the pawn has no relevant skills or every specific role is capped. */
        public static SpecialistRoleDef BestSpecialistRole(Pawn pawn, WorldObjectComp_SettlementSpecialists roster)
        {
            SpecialistRoleDef best = null;
            float bestScore = 0f;
            foreach (SpecialistRoleDef role in DefDatabase<SpecialistRoleDef>.AllDefs)
            {
                if (role.isGeneralist) continue;
                if (roster is object && roster.RoleAtCap(role)) continue;
                float score = role.ComputeSkillScore(pawn);
                if (score > bestScore
                    || (score == bestScore && best is object && string.CompareOrdinal(role.defName, best.defName) < 0))
                {
                    bestScore = score;
                    best = role;
                }
            }
            return best ?? SpecialistRoleDefOf.Generalist;
        }

        /* The governor focus the pawn fits best; falls back to Balanced. */
        public static GovernorFocusDef BestGovernorFocus(Pawn pawn)
        {
            GovernorFocusDef best = null;
            float bestScore = float.NegativeInfinity;
            foreach (GovernorFocusDef focus in DefDatabase<GovernorFocusDef>.AllDefs)
            {
                float score = focus.ComputeSkillScore(pawn);
                if (score > bestScore
                    || (score == bestScore && best is object && string.CompareOrdinal(focus.defName, best.defName) < 0))
                {
                    bestScore = score;
                    best = focus;
                }
            }
            return best ?? GovernorFocusDefOf.Balanced;
        }

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

        /* Meritocratic amplifies a governor's focus effectiveness: the bonus portion (the part above
           1.0) of every governor multiplier is scaled up. */
        public static double GovernorEffectiveness()
        {
            return HasTrait(SpecPolicyDefOf.FCSmeritocratic) ? 1.25 : 1.0;
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

            // Baseline folds additively too, so per-resource and baseline contributions compose
            // consistently (a focus defining both won't scale superlinearly).
            if (g.focus.providesBaselineProduction)
                multiplier += g.focus.baselineProductionValue * score;

            return 1.0 + (multiplier - 1.0) * GovernorEffectiveness();
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

            // Professional Army: Commander specialists contribute double military level.
            if (bonus != 0 && stat == FCStatDefOf.militaryBaseLevel
                && s.role == SpecialistRoleDefOf.Commander
                && HasTrait(SpecPolicyDefOf.FCSprofessionalArmy))
                bonus *= 2.0;

            return bonus;
        }

        /* Garrison Doctrine: settlement happiness gained per Commander specialist, scaled by skill.
           Tunable; kept worker-count-independent (per-specialist skill scaling only). */
        public const double GarrisonHappinessPerSkill = 0.05;

        public static double GarrisonHappinessBonus(IEnumerable<SettlementSpecialist> specialists)
        {
            if (specialists is null || !HasTrait(SpecPolicyDefOf.FCSgarrisonDoctrine)) return 0;
            double bonus = 0;
            foreach (SettlementSpecialist s in specialists)
            {
                if (s.role != SpecialistRoleDefOf.Commander || !s.HasUsableSkills) continue;
                bonus += s.SkillScore * GarrisonHappinessPerSkill;
            }
            return bonus;
        }

        /* Garrison Doctrine: a Commander specialist holds the line and dies half as often in the
           abstract battle death roll. Kept pure (garrison state passed in) so the per-member chance
           the roll consumes stays deterministically testable. */
        public static float EffectiveSpecialistDeathChance(SpecialistRoleDef role, float baseChance, bool garrisonActive)
        {
            if (garrisonActive && role == SpecialistRoleDefOf.Commander)
                return baseChance * 0.5f;
            return baseChance;
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
            return 1.0 + (mult - 1.0) * GovernorEffectiveness();
        }

        /* Unified upkeep: base silver plus a skill-scaled portion, where the global Scaling factor
           tunes how strongly skill raises wages. Specialists use their role's values; governors use
           their focus's values and then apply the governor multiplier on top (see GovernorUpkeep). */
        private static double ComputeUpkeep(float baseSilver, float skillScore, float skillScaling)
        {
            return baseSilver + (skillScore * skillScaling * FCSSettings.scalingFactor);
        }

        public static double SpecialistUpkeep(SettlementSpecialist s)
        {
            if (s is null || s.role is null) return 0;
            return ComputeUpkeep(s.role.baseUpkeepSilver, s.SkillScore, s.role.skillUpkeepScaling);
        }

        public static int WorkerBonusFromResidents(int liveResidentCount)
        {
            if (FCSSettings.residentsPerWorker <= 0) return 0;
            return (int)Math.Floor(liveResidentCount / (double)FCSSettings.residentsPerWorker);
        }

        public static bool HasTrait(FCPolicyDef def)
        {
            return def is object && (FindFC.PolicyManager?.HasTrait(def) ?? false);
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
                    bonus += TaperedLevel(govMed.Level) * FCSSettings.healRatePerLevelGovernor;
            }

            return bonus;
        }

        /* Governor upkeep (wage) is Meritocratic ×3, otherwise ×2. Shared so the tooltip breakdown
           and the applied value can't drift. */
        public static double GovernorUpkeepMultiplier => HasTrait(SpecPolicyDefOf.FCSmeritocratic) ? 3.0 : 2.0;

        public static double GovernorUpkeep(SettlementGovernor g)
        {
            if (g is null || g.focus is null) return 0; // no focus -> no bonuses and no wage
            return ComputeUpkeep(g.focus.baseUpkeepSilver, g.SkillScore, g.focus.skillUpkeepScaling) * GovernorUpkeepMultiplier;
        }

        public static void SendDeathLetter(string roleLabel, string bodyText)
        {
            string label = "FCS_LetterMemberKilled".Translate(roleLabel);
            Find.LetterStack.ReceiveLetter(label, bodyText, LetterDefOf.Death);
        }

        /* The settlement comp whose roster (specialist/resident/governor) contains this pawn, or null.
           Only called after SpecialistRoster.IsAssigned has confirmed the pawn is a member, so the
           per-settlement scan runs rarely. */
        public static WorldObjectComp_SettlementSpecialists FindOwningComp(Pawn pawn)
        {
            if (pawn is null) return null;
            List<WorldSettlementFC> settlements = FindFC.Settlements;
            if (settlements is null) return null;
            foreach (WorldSettlementFC settlement in settlements)
            {
                WorldObjectComp_SettlementSpecialists comp =
                    settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) continue;
                foreach (SettlementSpecialist s in comp.Specialists)
                    if (s.pawn == pawn) return comp;
                foreach (SettlementSpecialist r in comp.Residents)
                    if (r.pawn == pawn) return comp;
                // Match on identity, not HasGovernor: a just-killed governor is already !IsAlive.
                if (comp.Governor is object && comp.Governor.pawn == pawn) return comp;
            }
            return null;
        }

        /* Top-N skills label, optionally filtered to a role's skillWeights */
        public static string TopRoleSkillsLabel(Pawn pawn, SpecialistRoleDef role, int count = 2)
        {
            HashSet<SkillDef> relevant = null;
            if (role is object && role.skillWeights is object && role.skillWeights.Count > 0)
            {
                relevant = new HashSet<SkillDef>();
                foreach (SkillWeight sw in role.skillWeights)
                {
                    if (sw.skill is object) relevant.Add(sw.skill);
                }
                if (relevant.Count == 0) relevant = null;
            }
            return TopSkillsLabelCore(pawn, relevant, count);
        }

        /* Top-N skills label filtered to a governor focus's skillWeights; Social is always relevant */
        public static string TopFocusSkillsLabel(Pawn pawn, GovernorFocusDef focus, int count = 2)
        {
            HashSet<SkillDef> relevant = null;
            if (focus is object)
            {
                relevant = new HashSet<SkillDef>();
                if (focus.skillWeights is object)
                {
                    foreach (SkillWeight sw in focus.skillWeights)
                    {
                        if (sw.skill is object) relevant.Add(sw.skill);
                    }
                }
                relevant.Add(SkillDefOf.Social);
                if (relevant.Count == 0) relevant = null;
            }
            return TopSkillsLabelCore(pawn, relevant, count);
        }

        private static string TopSkillsLabelCore(Pawn pawn, HashSet<SkillDef> relevant, int count)
        {
            if (pawn?.skills is null || count <= 0) return "";

            SkillRecord best = null;
            SkillRecord second = null;
            foreach (SkillRecord sk in pawn.skills.skills)
            {
                if (sk.TotallyDisabled) continue;
                if (relevant is object && !relevant.Contains(sk.def)) continue;
                if (best is null || sk.Level > best.Level)
                {
                    second = best;
                    best = sk;
                }
                else if (second is null || sk.Level > second.Level)
                {
                    second = sk;
                }
            }

            if (best is null) return "";
            string result = best.def.skillLabel.CapitalizeFirst() + " " + best.Level;
            if (count >= 2 && second is object)
            {
                result += ", " + second.def.skillLabel.CapitalizeFirst() + " " + second.Level;
            }
            return result;
        }
    }
}
