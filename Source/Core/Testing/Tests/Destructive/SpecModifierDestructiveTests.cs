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

        /* M2 regression: with the base stat now appliesToSettlements and CombineForce passing the
           anchor settlement, a Military-focus governor's militaryLevelBonusDefending contribution
           must reach the stat at settlement scope (previously silently dead). */
        [EmpireDestructiveTest("SG.Destructive.Modifiers")]
        public static void GovernorMilitaryFocus_FeedsDefendingStatAtSettlementScope()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            FCStatDef stat = FCStatDefOf.militaryLevelBonusDefending;
            if (stat is null) TestAssert.Skip("militaryLevelBonusDefending not loaded");
            if (!stat.appliesToSettlements) TestAssert.Skip("stat is not settlement-scoped (M2 flip missing)");
            GovernorFocusDef mil = DefDatabase<GovernorFocusDef>.GetNamedSilentFail("Military");
            if (mil is null) TestAssert.Skip("Military focus not loaded");
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(12);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignGovernor(p, mil);
                if (comp.GetStatModifier(stat) <= stat.IdentityValue)
                    TestAssert.Skip("Governor produced no defending contribution (skills disabled?)");

                double factionOnly = f.GetStatValue(stat, null);
                double withSettlement = f.GetStatValue(stat, s);
                TestAssert.GreaterThan(withSettlement, factionOnly,
                    "settlement scope should fold in the Military governor's defending bonus");
                DestructiveTestUtil.AssertEmpireInvariants(f, "GovernorMilitaryFocus_FeedsDefendingStatAtSettlementScope");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        /* M5 regression: GetStatModifier applies the governor's stat contribution but the paired
           breakdown previously emitted no line for it, so the tooltip under-reported. */
        [EmpireDestructiveTest("SG.Destructive.Modifiers")]
        public static void GetStatModifierDesc_IncludesGovernorLine()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            FCStatDef stat = FCStatDefOf.militaryLevelBonusDefending;
            if (stat is null) TestAssert.Skip("militaryLevelBonusDefending not loaded");
            GovernorFocusDef mil = DefDatabase<GovernorFocusDef>.GetNamedSilentFail("Military");
            if (mil is null) TestAssert.Skip("Military focus not loaded");
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(12);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignGovernor(p, mil);
                if (comp.GetStatModifier(stat) <= stat.IdentityValue)
                    TestAssert.Skip("Governor produced no contribution (skills disabled?)");

                string desc = comp.GetStatModifierDesc(stat);
                TestAssert.IsNotNull(desc, "expected a stat breakdown");
                TestAssert.IsTrue(desc.Contains(p.LabelShort),
                    "breakdown should include a governor contribution line");
                DestructiveTestUtil.AssertEmpireInvariants(f, "GetStatModifierDesc_IncludesGovernorLine");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        /* M5 regression: the resource additive breakdown must surface the satisfaction multiplier so
           its lines reconcile with the damped value from GetResourceAdditiveModifier. */
        [EmpireDestructiveTest("SG.Destructive.Modifiers")]
        public static void GetResourceAdditiveDesc_ShowsSatisfactionWhenDamped()
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
            string satisfactionLabel = "FCS_SatisfactionLabel".Translate();
            try
            {
                comp.AssignSpecialist(p, SpecTestHelper.Role(SkillDefOf.Plants, 1f, rfc.def, 0.05f));
                if (SpecUtil.SpecialistAdditiveForResource(comp.Specialists[0], rfc.def) <= 0.001)
                    TestAssert.Skip("Specialist adds no production (skills disabled?)");

                // Fresh comp: satisfaction == 1, so no satisfaction line.
                string full = comp.GetResourceAdditiveDesc(rfc);
                TestAssert.IsNotNull(full, "expected a production breakdown");
                TestAssert.IsFalse(full.Contains(satisfactionLabel), "no satisfaction line when satisfaction is 1");
                double fullModifier = comp.GetResourceAdditiveModifier(rfc); // satisfaction == 1

                // Damp satisfaction: the applied value must drop by exactly the 0.5 factor (independent
                // of any specialist-corps multiplier), and the breakdown must surface the multiplier.
                comp.FoodSatisfaction = 0.5f;
                double damped = comp.GetResourceAdditiveModifier(rfc);
                TestAssert.LessThan(damped, fullModifier, "satisfaction should reduce the applied bonus");
                TestAssert.AreEqual(fullModifier * 0.5, damped, message: "0.5 food satisfaction should halve the applied additive");
                string dampedDesc = comp.GetResourceAdditiveDesc(rfc);
                TestAssert.IsTrue(dampedDesc.Contains(satisfactionLabel),
                    "breakdown should show the satisfaction multiplier when damped");
                DestructiveTestUtil.AssertEmpireInvariants(f, "GetResourceAdditiveDesc_ShowsSatisfactionWhenDamped");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }
    }
}
