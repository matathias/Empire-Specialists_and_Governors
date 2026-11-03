using RimWorld;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Pure-math and null-guard coverage for <see cref="SpecUtil"/>. No active game required.
    /// Settings-mutating tests pin a known value and restore it in a finally block so the
    /// non-destructive contract holds.
    /// </summary>
    public static class SpecUtilMathTests
    {
        /*-*-*- TaperedLevel: per-band scaling of a skill level's contribution -*-*-*/

        [EmpireTest("SG.Math")]
        public static void TaperedLevel_Zero_ReturnsZero()
        {
            SpecTestHelper.WithStandardTaper(() =>
                TestAssert.AreEqual(0.0, SpecUtil.TaperedLevel(0)));
        }

        [EmpireTest("SG.Math")]
        public static void TaperedLevel_Negative_ReturnsZero()
        {
            SpecTestHelper.WithStandardTaper(() =>
                TestAssert.AreEqual(0.0, SpecUtil.TaperedLevel(-3)));
        }

        [EmpireTest("SG.Math")]
        public static void TaperedLevel_Band1_FullSlope()
        {
            // band1 = 1.0, level 5 -> 5
            SpecTestHelper.WithStandardTaper(() =>
                TestAssert.AreEqual(5.0, SpecUtil.TaperedLevel(5)));
        }

        [EmpireTest("SG.Math")]
        public static void TaperedLevel_Band2Boundary_Is20()
        {
            // 10*1 + 10*1 = 20
            SpecTestHelper.WithStandardTaper(() =>
                TestAssert.AreEqual(20.0, SpecUtil.TaperedLevel(20)));
        }

        [EmpireTest("SG.Math")]
        public static void TaperedLevel_Band3_HalfSlopeAboveCap()
        {
            // 10 + 10 + 5*0.5 = 22.5
            SpecTestHelper.WithStandardTaper(() =>
                TestAssert.AreEqual(22.5, SpecUtil.TaperedLevel(25)));
        }

        [EmpireTest("SG.Math")]
        public static void TaperedLevel_RespectsBand1Factor()
        {
            float orig = FCSSettings.skillTaperFactorBand1;
            try
            {
                FCSSettings.skillTaperFactorBand1 = 0.5f;
                // 10 * 0.5 = 5
                TestAssert.AreEqual(5.0, SpecUtil.TaperedLevel(10));
            }
            finally { FCSSettings.skillTaperFactorBand1 = orig; }
        }

        /*-*-*- WorkerBonusFromResidents: floor(count / residentsPerWorker) -*-*-*/

        [EmpireTest("SG.Math")]
        public static void WorkerBonus_FloorDivision()
        {
            int orig = FCSSettings.residentsPerWorker;
            try
            {
                FCSSettings.residentsPerWorker = 5;
                TestAssert.AreEqual(2, SpecUtil.WorkerBonusFromResidents(12));
            }
            finally { FCSSettings.residentsPerWorker = orig; }
        }

        [EmpireTest("SG.Math")]
        public static void WorkerBonus_ExactMultiple()
        {
            int orig = FCSSettings.residentsPerWorker;
            try
            {
                FCSSettings.residentsPerWorker = 5;
                TestAssert.AreEqual(2, SpecUtil.WorkerBonusFromResidents(10));
            }
            finally { FCSSettings.residentsPerWorker = orig; }
        }

        [EmpireTest("SG.Math")]
        public static void WorkerBonus_BelowOne_ReturnsZero()
        {
            int orig = FCSSettings.residentsPerWorker;
            try
            {
                FCSSettings.residentsPerWorker = 5;
                TestAssert.AreEqual(0, SpecUtil.WorkerBonusFromResidents(3));
            }
            finally { FCSSettings.residentsPerWorker = orig; }
        }

        [EmpireTest("SG.Math")]
        public static void WorkerBonus_ZeroDivisor_ReturnsZero()
        {
            int orig = FCSSettings.residentsPerWorker;
            try
            {
                FCSSettings.residentsPerWorker = 0;
                TestAssert.AreEqual(0, SpecUtil.WorkerBonusFromResidents(100));
            }
            finally { FCSSettings.residentsPerWorker = orig; }
        }

        /*-*-*- Null / unusable-skill guards (no pawn needed; identity returns) -*-*-*/

        [EmpireTest("SG.Math")]
        public static void SpecialistAdditive_NullSpecialist_ReturnsZero()
        {
            TestAssert.AreEqual(0.0, SpecUtil.SpecialistAdditiveForResource(null, null));
        }

        [EmpireTest("SG.Math")]
        public static void SpecialistAdditive_NullRole_ReturnsZero()
        {
            SettlementSpecialist s = new SettlementSpecialist { pawn = null, role = null };
            TestAssert.AreEqual(0.0, SpecUtil.SpecialistAdditiveForResource(s, null));
        }

        [EmpireTest("SG.Math")]
        public static void SpecialistAdditive_NoUsableSkills_ReturnsZero()
        {
            // role set, but pawn null -> HasUsableSkills false -> guarded to 0
            SettlementSpecialist s = new SettlementSpecialist
            {
                pawn = null,
                role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f)
            };
            TestAssert.AreEqual(0.0, SpecUtil.SpecialistAdditiveForResource(s, null));
        }

        [EmpireTest("SG.Math")]
        public static void GovernorMultiplier_NullGovernor_ReturnsOne()
        {
            TestAssert.AreEqual(1.0, SpecUtil.GovernorMultiplierForResource(null, null));
        }

        [EmpireTest("SG.Math")]
        public static void GovernorMultiplier_NoUsableSkills_ReturnsOne()
        {
            SettlementGovernor g = new SettlementGovernor
            {
                pawn = null,
                focus = SpecTestHelper.Focus(SkillDefOf.Intellectual, 1f, null, 0f)
            };
            TestAssert.AreEqual(1.0, SpecUtil.GovernorMultiplierForResource(g, null));
        }

        [EmpireTest("SG.Math")]
        public static void SpecialistStatBonus_NullRole_ReturnsZero()
        {
            SettlementSpecialist s = new SettlementSpecialist { pawn = null, role = null };
            TestAssert.AreEqual(0.0, SpecUtil.SpecialistStatBonus(s, null));
        }

        [EmpireTest("SG.Math")]
        public static void GovernorStatMultiplier_NullFocus_ReturnsOne()
        {
            SettlementGovernor g = new SettlementGovernor { pawn = null, focus = null };
            TestAssert.AreEqual(1.0, SpecUtil.GovernorStatMultiplier(g, null));
        }

        [EmpireTest("SG.Math")]
        public static void SpecialistUpkeep_NullSpecialist_ReturnsZero()
        {
            TestAssert.AreEqual(0.0, SpecUtil.SpecialistUpkeep(null));
        }

        [EmpireTest("SG.Math")]
        public static void SpecialistUpkeep_NullRole_ReturnsZero()
        {
            SettlementSpecialist s = new SettlementSpecialist { pawn = null, role = null };
            TestAssert.AreEqual(0.0, SpecUtil.SpecialistUpkeep(s));
        }

        /*-*-*- EffectiveSpecialistDeathChance: Garrison Doctrine halves a Commander's roll -*-*-*/

        [EmpireTest("SG.Math")]
        public static void EffectiveSpecialistDeathChance_GarrisonCommander_Halved()
        {
            if (SpecialistRoleDefOf.Commander is null) TestAssert.Skip("Commander role not loaded");
            // Garrison active + Commander -> base * 0.5
            TestAssert.AreEqual(0.5, SpecUtil.EffectiveSpecialistDeathChance(SpecialistRoleDefOf.Commander, 1.0f, true));
        }

        [EmpireTest("SG.Math")]
        public static void EffectiveSpecialistDeathChance_GarrisonNonCommander_Unchanged()
        {
            if (SpecialistRoleDefOf.Generalist is null) TestAssert.Skip("Generalist role not loaded");
            // Garrison active but not a Commander -> unchanged
            TestAssert.AreEqual(1.0, SpecUtil.EffectiveSpecialistDeathChance(SpecialistRoleDefOf.Generalist, 1.0f, true));
        }

        [EmpireTest("SG.Math")]
        public static void EffectiveSpecialistDeathChance_NoGarrison_Unchanged()
        {
            if (SpecialistRoleDefOf.Commander is null) TestAssert.Skip("Commander role not loaded");
            // Garrison inactive -> even a Commander is unchanged
            TestAssert.AreEqual(1.0, SpecUtil.EffectiveSpecialistDeathChance(SpecialistRoleDefOf.Commander, 1.0f, false));
        }
    }
}
