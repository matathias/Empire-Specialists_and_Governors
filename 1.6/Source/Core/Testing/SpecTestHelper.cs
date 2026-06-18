using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Shared, GAME-FREE helpers for the Specialists &amp; Governors non-destructive tests: a
    /// snapshot/restore for the mutable <see cref="FCSSettings"/> per-band taper statics (so formula
    /// tests can pin known scaling and still leave settings as they found them), synthetic def
    /// builders (never registered in the DefDatabase -- the code under test only relies on reference
    /// identity), and a controlled, never-spawned pawn for the formula tests.
    /// </summary>
    public static class SpecTestHelper
    {
        /* -*- Synthetic defs -*- */

        // A role weighted on one skill, optionally granting a per-resource additive bonus.
        public static SpecialistRoleDef Role(SkillDef weightSkill, float weight,
            ResourceTypeDef res, float resBaseValue,
            float baseUpkeep = 0f, float upkeepScaling = 0f, int maxPerSettlement = 0)
        {
            SpecialistRoleDef role = new SpecialistRoleDef
            {
                defName = "TEST_Role",
                label = "test role",
                baseUpkeepSilver = baseUpkeep,
                skillUpkeepScaling = upkeepScaling,
                maxPerSettlement = maxPerSettlement,
                skillWeights = new List<SkillWeight>
                {
                    new SkillWeight { skill = weightSkill, weight = weight }
                }
            };
            if (res is object)
                role.resourceBonuses = new List<ResourceProductionBonus>
                {
                    new ResourceProductionBonus { resource = res, baseValue = resBaseValue }
                };
            return role;
        }

        // A role that grants flat baseline production scaled by skill score.
        public static SpecialistRoleDef BaselineRole(float baselineValue, SkillDef weightSkill, float weight)
        {
            return new SpecialistRoleDef
            {
                defName = "TEST_BaselineRole",
                label = "test baseline role",
                providesBaselineProduction = true,
                baselineProductionValue = baselineValue,
                skillWeights = new List<SkillWeight>
                {
                    new SkillWeight { skill = weightSkill, weight = weight }
                }
            };
        }

        // A role carrying a single FCStatModifier scaled by skill score.
        public static SpecialistRoleDef StatRole(FCStatDef stat, double value, SkillDef weightSkill, float weight)
        {
            return new SpecialistRoleDef
            {
                defName = "TEST_StatRole",
                label = "test stat role",
                statModifiers = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = value } },
                skillWeights = new List<SkillWeight>
                {
                    new SkillWeight { skill = weightSkill, weight = weight }
                }
            };
        }

        // A role carrying the apothecary heal extension (read by SpecUtil.MedicalHealRateBonus).
        public static SpecialistRoleDef ApothecaryRole(float healPerPoint, SkillDef weightSkill, float weight)
        {
            return new SpecialistRoleDef
            {
                defName = "TEST_ApothRole",
                label = "test apothecary role",
                skillWeights = new List<SkillWeight>
                {
                    new SkillWeight { skill = weightSkill, weight = weight }
                },
                modExtensions = new List<DefModExtension>
                {
                    new RoleBehaviorExt_Apothecary { healRatePerSkillPoint = healPerPoint }
                }
            };
        }

        // A governor focus weighted on one (non-Social) skill, optionally with a resource bonus.
        public static GovernorFocusDef Focus(SkillDef weightSkill, float weight,
            ResourceTypeDef res, float resBaseValue,
            float baseUpkeep = 0f, float upkeepScaling = 0f)
        {
            GovernorFocusDef focus = new GovernorFocusDef
            {
                defName = "TEST_Focus",
                label = "test focus",
                baseUpkeepSilver = baseUpkeep,
                skillUpkeepScaling = upkeepScaling,
                skillWeights = new List<SkillWeight>
                {
                    new SkillWeight { skill = weightSkill, weight = weight }
                }
            };
            if (res is object)
                focus.resourceBonuses = new List<ResourceProductionBonus>
                {
                    new ResourceProductionBonus { resource = res, baseValue = resBaseValue }
                };
            return focus;
        }

        // A governor focus carrying a single FCStatModifier scaled by skill score.
        public static GovernorFocusDef FocusWithStat(FCStatDef stat, double value, SkillDef weightSkill, float weight)
        {
            return new GovernorFocusDef
            {
                defName = "TEST_FocusStat",
                label = "test focus stat",
                statModifiers = new List<FCStatModifier> { new FCStatModifier { stat = stat, value = value } },
                skillWeights = new List<SkillWeight>
                {
                    new SkillWeight { skill = weightSkill, weight = weight }
                }
            };
        }

        // A focus with only a Social weight, to exercise the Social-always-1.0 rule.
        public static GovernorFocusDef SocialFocus(float socialWeight)
        {
            return new GovernorFocusDef
            {
                defName = "TEST_SocialFocus",
                label = "test social focus",
                skillWeights = new List<SkillWeight>
                {
                    new SkillWeight { skill = SkillDefOf.Social, weight = socialWeight }
                }
            };
        }

        /* -*- Entry builders (object-initializer form avoids the game-dependent ctor) -*- */

        public static SettlementSpecialist Specialist(Pawn pawn, SpecialistRoleDef role)
        {
            return new SettlementSpecialist { pawn = pawn, role = role };
        }

        public static SettlementGovernor Governor(Pawn pawn, GovernorFocusDef focus)
        {
            return new SettlementGovernor { pawn = pawn, focus = focus };
        }

        /* -*- Controlled, never-spawned pawn -*- */

        // A colonist whose every skill is set to `level`, never spawned or passed to the world (so
        // it leaves no persistent residue). The generated pawn's random backstory/traits would
        // otherwise disable some skills (SkillRecord.Level returns 0 for a TotallyDisabled skill),
        // making the formula tests flaky -- so EnsureAllSkillsEnabled strips every disable source
        // first, leaving the fixture deterministic. Returns null if there is no active game.
        public static Pawn TryMakeControlledPawn(int level)
        {
            if (Current.Game is null || Find.World is null) return null;
            Faction faction = Faction.OfPlayerSilentFail;
            if (faction is null) return null;

            Pawn p;
            try
            {
                // canGeneratePawnRelations: false keeps the throwaway pawn from wiring social
                // relations onto existing colonists (which would leak references to it).
                PawnGenerationRequest req = new PawnGenerationRequest(
                    PawnKindDefOf.Colonist, faction,
                    forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false,
                    allowDowned: false);
                p = PawnGenerator.GeneratePawn(req);
            }
            catch
            {
                return null;
            }
            if (p?.skills is null) return null;

            EnsureAllSkillsEnabled(p);

            foreach (SkillRecord rec in p.skills.skills)
            {
                rec.Level = level;
                rec.passion = Passion.None;
            }
            return p;
        }

        // Cached no-work-disabling backstories (reference identity only; never registered anew).
        private static BackstoryDef noDisableChildhood;
        private static BackstoryDef noDisableAdulthood;

        // Strips every source of skill disabling from a freshly generated colonist (work-disabling
        // traits + backstories), so that no SkillRecord is TotallyDisabled and each reads its set
        // level. Generated pawns are Baseliner player colonists here, so backstories and traits are
        // the only disable sources (no disabling genes; the Empire xenotype-forcing patch only fires
        // for the Empire faction, not Faction.OfPlayer).
        private static void EnsureAllSkillsEnabled(Pawn p)
        {
            if (p?.story is null) return;

            if (p.story.traits is object)
            {
                List<Trait> disabling = new List<Trait>();
                foreach (Trait t in p.story.traits.allTraits)
                    if (t.def.disabledWorkTags != WorkTags.None) disabling.Add(t);
                foreach (Trait t in disabling) p.story.traits.RemoveTrait(t);
            }

            if (noDisableChildhood is null)
                noDisableChildhood = DefDatabase<BackstoryDef>.AllDefsListForReading.Find(b =>
                    b.slot == BackstorySlot.Childhood && b.workDisables == WorkTags.None
                    && (b.spawnCategories is null || !b.spawnCategories.Contains("Child")));
            if (noDisableAdulthood is null)
                noDisableAdulthood = DefDatabase<BackstoryDef>.AllDefsListForReading.Find(b =>
                    b.slot == BackstorySlot.Adulthood && b.workDisables == WorkTags.None);

            if (noDisableChildhood is object) p.story.Childhood = noDisableChildhood;
            if (noDisableAdulthood is object) p.story.Adulthood = noDisableAdulthood;

            // Refresh the lazily-cached disable state so the now-enabled skills report correctly.
            p.Notify_DisabledWorkTypesChanged();
            foreach (SkillRecord rec in p.skills.skills)
                rec.Notify_SkillDisablesChanged();
        }

        /* -*- Settings snapshot helper: run `body` with the default per-band taper factors
              (1, 1, 0.5) and the default upkeep scaling factor (1) so TaperedLevel and upkeep are
              deterministic regardless of the player's settings, then restore. -*- */
        public static void WithStandardTaper(Action body)
        {
            float b1 = FCSSettings.skillTaperFactorBand1;
            float b2 = FCSSettings.skillTaperFactorBand2;
            float b3 = FCSSettings.skillTaperFactorBand3;
            float sf = FCSSettings.scalingFactor;
            try
            {
                FCSSettings.skillTaperFactorBand1 = 1f;
                FCSSettings.skillTaperFactorBand2 = 1f;
                FCSSettings.skillTaperFactorBand3 = 0.5f;
                FCSSettings.scalingFactor = 1f;
                body();
            }
            finally
            {
                FCSSettings.skillTaperFactorBand1 = b1;
                FCSSettings.skillTaperFactorBand2 = b2;
                FCSSettings.skillTaperFactorBand3 = b3;
                FCSSettings.scalingFactor = sf;
            }
        }
    }
}
