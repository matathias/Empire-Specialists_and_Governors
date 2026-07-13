using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Production / upkeep / skill-score formulas exercised with a controlled pawn. Skip-if-no-game
    /// (pawn generation needs a loaded world). Each math test runs under
    /// <see cref="SpecTestHelper.WithStandardTaper"/> so TaperedLevel(10) == 10 and expected values
    /// are exact. The pawn is never spawned or registered, so these stay non-destructive.
    /// </summary>
    public static class SpecFormulaTests
    {
        private const int Lvl = 10; // every enabled skill set to 10 -> TaperedLevel(10) == 10

        [EmpireTest("SG.Formula")]
        public static void SkillScore_SingleWeightedSkill_Matches()
        {
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementSpecialist s = SpecTestHelper.Specialist(p,
                    SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f));
                TestAssert.AreEqual(10.0, s.SkillScore); // 10 * 1.0
            });
        }

        [EmpireTest("SG.Formula")]
        public static void SkillScore_WeightScales()
        {
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementSpecialist s = SpecTestHelper.Specialist(p,
                    SpecTestHelper.Role(SkillDefOf.Plants, 0.3f, null, 0f));
                TestAssert.AreEqual(3.0, s.SkillScore); // 10 * 0.3
            });
        }

        [EmpireTest("SG.Formula")]
        public static void SpecialistAdditive_MatchingResource()
        {
            ResourceTypeDef food = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Food");
            if (food is null) TestAssert.Skip("RTD_Food not loaded");
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementSpecialist s = SpecTestHelper.Specialist(p,
                    SpecTestHelper.Role(SkillDefOf.Plants, 1f, food, 0.05f));
                // 0.05 * (10 * 1.0) = 0.5
                TestAssert.AreEqual(0.5, SpecUtil.SpecialistAdditiveForResource(s, food));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void SpecialistAdditive_NonMatchingResource_Zero()
        {
            ResourceTypeDef food = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Food");
            ResourceTypeDef mining = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Mining");
            if (food is null || mining is null) TestAssert.Skip("RTD_Food/RTD_Mining not loaded");
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementSpecialist s = SpecTestHelper.Specialist(p,
                    SpecTestHelper.Role(SkillDefOf.Plants, 1f, food, 0.05f));
                TestAssert.AreEqual(0.0, SpecUtil.SpecialistAdditiveForResource(s, mining));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void SpecialistAdditive_Baseline()
        {
            ResourceTypeDef food = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Food");
            if (food is null) TestAssert.Skip("RTD_Food not loaded");
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementSpecialist s = SpecTestHelper.Specialist(p,
                    SpecTestHelper.BaselineRole(0.02f, SkillDefOf.Plants, 1f));
                // baselineProductionValue * score = 0.02 * 10 = 0.2
                TestAssert.AreEqual(0.2, SpecUtil.SpecialistAdditiveForResource(s, food));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void GovernorMultiplier_MatchingResource()
        {
            ResourceTypeDef food = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Food");
            if (food is null) TestAssert.Skip("RTD_Food not loaded");
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementGovernor g = SpecTestHelper.Governor(p,
                    SpecTestHelper.Focus(SkillDefOf.Intellectual, 0.4f, food, 0.01f));
                // score = 10*0.4 (Intellectual) + 10*1.0 (Social) = 14; raw mult = 1 + 0.01*14 = 1.14,
                // then the bonus portion is scaled by governor effectiveness (Meritocracy amplifies it).
                double expected = 1.0 + 0.14 * SpecUtil.GovernorEffectiveness();
                TestAssert.AreEqual(expected, SpecUtil.GovernorMultiplierForResource(g, food));
            });
        }

        // The death-letter title is role-specific (Governor/Resident/role name), not a fixed
        // "Specialist killed" -- guards the FCS_LetterMemberKilled {0} placeholder wiring.
        [EmpireTest("SG.Formula")]
        public static void DeathLetterTitle_CarriesRoleLabel()
        {
            if (Current.Game is null) TestAssert.Skip("No game / translation unavailable");
            string governorTitle = "FCS_LetterMemberKilled".Translate("Governor").Resolve();
            string residentTitle = "FCS_LetterMemberKilled".Translate("Resident").Resolve();
            TestAssert.IsTrue(governorTitle.Contains("Governor"),
                "Death-letter title should embed the role label");
            TestAssert.IsTrue(residentTitle.Contains("Resident"),
                "Death-letter title should embed the role label");
            TestAssert.IsFalse(governorTitle == residentTitle,
                "Title must vary by role, not be a fixed 'Specialist killed'");
        }

        // A focus defining BOTH a per-resource bonus and baseline production folds them additively,
        // not multiplicatively (which would scale superlinearly).
        [EmpireTest("SG.Formula")]
        public static void GovernorMultiplier_ResourceAndBaseline_FoldAdditively()
        {
            ResourceTypeDef food = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Food");
            if (food is null) TestAssert.Skip("RTD_Food not loaded");
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                GovernorFocusDef focus = SpecTestHelper.Focus(SkillDefOf.Intellectual, 0.4f, food, 0.01f);
                focus.providesBaselineProduction = true;
                focus.baselineProductionValue = 0.02f;
                SettlementGovernor g = SpecTestHelper.Governor(p, focus);
                // score = 10*0.4 + 10*1.0 (Social) = 14; additive bonus = (0.01 + 0.02) * 14 = 0.42.
                // Old multiplicative folding would give 1.14 * 1.28 = 1.4592 (bonus 0.4592) -- superlinear.
                double expected = 1.0 + (0.01 + 0.02) * 14.0 * SpecUtil.GovernorEffectiveness();
                TestAssert.AreEqual(expected, SpecUtil.GovernorMultiplierForResource(g, food));
            });
        }

        // Baseline-only focus is unchanged by the additive reconciliation (multiplier starts at 1.0).
        [EmpireTest("SG.Formula")]
        public static void GovernorMultiplier_BaselineOnly_Unchanged()
        {
            ResourceTypeDef food = DefDatabase<ResourceTypeDef>.GetNamedSilentFail("RTD_Food");
            if (food is null) TestAssert.Skip("RTD_Food not loaded");
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                GovernorFocusDef focus = SpecTestHelper.Focus(SkillDefOf.Intellectual, 0.4f, null, 0f);
                focus.providesBaselineProduction = true;
                focus.baselineProductionValue = 0.02f;
                SettlementGovernor g = SpecTestHelper.Governor(p, focus);
                // score = 14; bonus = 0.02 * 14 = 0.28.
                double expected = 1.0 + 0.02 * 14.0 * SpecUtil.GovernorEffectiveness();
                TestAssert.AreEqual(expected, SpecUtil.GovernorMultiplierForResource(g, food));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void GovernorScore_SocialWeightBumpedToOne()
        {
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                GovernorFocusDef focus = SpecTestHelper.SocialFocus(0.4f); // < 1 -> treated as 1.0
                TestAssert.AreEqual(10.0, focus.ComputeSkillScore(p)); // 10 * 1.0, not 10 * 0.4
            });
        }

        [EmpireTest("SG.Formula")]
        public static void GovernorScore_AddsSocialWhenAbsent()
        {
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                GovernorFocusDef focus = SpecTestHelper.Focus(SkillDefOf.Intellectual, 0.4f, null, 0f);
                // 10*0.4 + 10*1.0 (Social always) = 14
                TestAssert.AreEqual(14.0, focus.ComputeSkillScore(p));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void SpecialistStatBonus_ScaledByScore()
        {
            FCStatDef anyStat = DefDatabase<FCStatDef>.AllDefsListForReading.FirstOrDefault();
            if (anyStat is null) TestAssert.Skip("No FCStatDef loaded");
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementSpecialist s = SpecTestHelper.Specialist(p,
                    SpecTestHelper.StatRole(anyStat, 0.01, SkillDefOf.Shooting, 1f));
                // value * score = 0.01 * 10 = 0.1
                TestAssert.AreEqual(0.1, SpecUtil.SpecialistStatBonus(s, anyStat));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void GovernorStatMultiplier_ScaledByScore()
        {
            FCStatDef anyStat = DefDatabase<FCStatDef>.AllDefsListForReading.FirstOrDefault();
            if (anyStat is null) TestAssert.Skip("No FCStatDef loaded");
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementGovernor g = SpecTestHelper.Governor(p,
                    SpecTestHelper.FocusWithStat(anyStat, 0.01, SkillDefOf.Intellectual, 0.4f));
                // score 14; raw mult = 1 + 0.01*14 = 1.14, bonus portion scaled by governor effectiveness.
                double expected = 1.0 + 0.14 * SpecUtil.GovernorEffectiveness();
                TestAssert.AreEqual(expected, SpecUtil.GovernorStatMultiplier(g, anyStat));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void SpecialistUpkeep_BaseAndScaling()
        {
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementSpecialist s = SpecTestHelper.Specialist(p,
                    SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f, baseUpkeep: 5f, upkeepScaling: 0.3f));
                // base + score*scaling = 5 + 10*0.3 = 8.0
                TestAssert.AreEqual(8.0, SpecUtil.SpecialistUpkeep(s));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void MedicalHealRateBonus_ApothecarySpecialist()
        {
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementSpecialist s = SpecTestHelper.Specialist(p,
                    SpecTestHelper.ApothecaryRole(0.02f, SkillDefOf.Medicine, 1f));
                List<SettlementSpecialist> list = new List<SettlementSpecialist> { s };
                // score * healRatePerSkillPoint = 10 * 0.02 = 0.2
                TestAssert.AreEqual(0.2, SpecUtil.MedicalHealRateBonus(list, null));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void MedicalHealRateBonus_GovernorMedicine()
        {
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementGovernor g = SpecTestHelper.Governor(p, null); // focus not needed for this branch
                List<SettlementSpecialist> empty = new List<SettlementSpecialist>();
                // TaperedLevel(10) * healRatePerLevelGovernor (recomputed from live setting)
                double expected = 10.0 * FCSSettings.healRatePerLevelGovernor;
                TestAssert.AreEqual(expected, SpecUtil.MedicalHealRateBonus(empty, g));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void GovernorUpkeep_BranchesOnMeritocracy()
        {
            if (FindFC.FactionComp is null) TestAssert.Skip("No active faction (policy state unavailable)");
            Pawn p = SpecTestHelper.TryMakeControlledPawn(Lvl);
            if (p is null) TestAssert.Skip("No game / pawn generation unavailable");
            SpecTestHelper.WithStandardTaper(() =>
            {
                SettlementGovernor g = SpecTestHelper.Governor(p,
                    SpecTestHelper.Focus(SkillDefOf.Plants, 1f, null, 0f, baseUpkeep: 4f, upkeepScaling: 0.3f));
                double mult = SpecUtil.HasTrait(SpecPolicyDefOf.FCSmeritocratic) ? 3.0 : 2.0;
                // unified: (base + score * scaling * scalingFactor) * governor mult
                double expected = (4.0 + g.SkillScore * 0.3 * 1.0) * mult;
                TestAssert.AreEqual(expected, SpecUtil.GovernorUpkeep(g));
            });
        }

        [EmpireTest("SG.Formula")]
        public static void XPPerDay_AppliesPolicyMultipliers()
        {
            if (FindFC.FactionComp is null) TestAssert.Skip("No active faction (policy state unavailable)");
            float expected = FCSSettings.xpPerDay
                * (SpecUtil.HasTrait(SpecPolicyDefOf.FCSspecialistCorps) ? 2f : 1f)
                * (SpecUtil.HasTrait(SpecPolicyDefOf.FCSmeritocratic) ? 1.5f : 1f);
            TestAssert.AreEqual(expected, SpecUtil.XPPerDay());
        }
    }
}
