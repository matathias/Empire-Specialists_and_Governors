using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    /* DESTRUCTIVE: the comp's interface modifiers reflect the live roster. Production / stat / upkeep
       contributions are recomputed from SpecUtil against the same live state, so these assert the
       comp's wiring (iteration, satisfaction, specialist-corps multiplier) rather than re-deriving
       the raw formula. */
    public static class SpecModifierDestructiveTests
    {
        [EmpireDestructiveTest("SG.Destructive.Modifiers")]
        public static void GetResourceAdditiveModifier_ReflectsSpecialist()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            if (s.Resources is null || s.Resources.Count == 0) TestAssert.Skip("Settlement has no resources");
            ResourceFC rfc = s.Resources[0];
            if (rfc?.def is null) TestAssert.Skip("Resource has no def");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(10);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(p, SpecTestHelper.Role(SkillDefOf.Plants, 1f, rfc.def, 0.05f));

                double raw = SpecUtil.SpecialistAdditiveForResource(comp.Specialists[0], rfc.def);
                double expected = raw;
                if (raw > 0 && SpecUtil.HasTrait(SpecPolicyDefOf.FCSspecialistCorps))
                    expected *= 1.2;
                // A fresh comp has food/medicine satisfaction == 1, so no extra damping.

                double actual = comp.GetResourceAdditiveModifier(rfc);
                TestAssert.AreEqual(expected, actual);
                if (comp.Specialists[0].SkillScore > 0f)
                    TestAssert.GreaterThan(actual, 0.0, "a skilled specialist should add production");
                DestructiveTestUtil.AssertEmpireInvariants(f, "GetResourceAdditiveModifier_ReflectsSpecialist");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Modifiers")]
        public static void GetResourceMultiplierModifier_NoGovernor_ReturnsOne()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            if (s.Resources is null || s.Resources.Count == 0) TestAssert.Skip("Settlement has no resources");
            ResourceFC rfc = s.Resources[0];
            if (rfc?.def is null) TestAssert.Skip("Resource has no def");
            try
            {
                TestAssert.AreEqual(1.0, comp.GetResourceMultiplierModifier(rfc));
                DestructiveTestUtil.AssertEmpireInvariants(f, "GetResourceMultiplierModifier_NoGovernor_ReturnsOne");
            }
            finally
            {
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Modifiers")]
        public static void GetStatModifier_WorkerBonusFromResidents()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            Pawn p1 = SpecDestructiveTestUtil.MakeAssignable(8);
            Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p1 is null || p2 is null) TestAssert.Skip("No pawn");
            int origRpw = FCSSettings.residentsPerWorker;
            try
            {
                FCSSettings.residentsPerWorker = 2; // 2 residents -> 1 worker
                comp.AssignSpecialist(p1, null);
                comp.AssignSpecialist(p2, null);

                double expected = FCStatDefOf.workerBaseMax.IdentityValue
                    + SpecUtil.WorkerBonusFromResidents(comp.LiveResidentCount);
                TestAssert.AreEqual(expected, comp.GetStatModifier(FCStatDefOf.workerBaseMax));
                TestAssert.AreEqual(1, SpecUtil.WorkerBonusFromResidents(comp.LiveResidentCount));
                DestructiveTestUtil.AssertEmpireInvariants(f, "GetStatModifier_WorkerBonusFromResidents");
            }
            finally
            {
                FCSSettings.residentsPerWorker = origRpw;
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Modifiers")]
        public static void GetDailyUpkeepContribution_SumsSpecialistsAndGovernor()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (focus is null) TestAssert.Skip("No GovernorFocusDef loaded");
            Pawn p1 = SpecDestructiveTestUtil.MakeAssignable(10);
            Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(10);
            if (p1 is null || p2 is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(p1, SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f, baseUpkeep: 5f, upkeepScaling: 0.3f));
                comp.AssignGovernor(p2, focus);

                double expected = SpecUtil.SpecialistUpkeep(comp.Specialists[0]) + SpecUtil.GovernorUpkeep(comp.Governor);
                TestAssert.AreEqual(expected, comp.GetDailyUpkeepContribution());
                TestAssert.GreaterThan(comp.GetDailyUpkeepContribution(), 0.0, "upkeep should be positive");
                DestructiveTestUtil.AssertEmpireInvariants(f, "GetDailyUpkeepContribution_SumsSpecialistsAndGovernor");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Modifiers")]
        public static void GetDailyUpkeepContribution_Empty_Zero()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            try
            {
                TestAssert.AreEqual(0.0, comp.GetDailyUpkeepContribution());
                DestructiveTestUtil.AssertEmpireInvariants(f, "GetDailyUpkeepContribution_Empty_Zero");
            }
            finally
            {
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }
    }
}
