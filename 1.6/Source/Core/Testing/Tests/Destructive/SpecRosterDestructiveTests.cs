using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace FactionColonies.Specialists
{
    /* DESTRUCTIVE: roster mutation on a real comp -- assign / recall / promote / change. Each test
       creates a transient settlement, mutates its specialists comp, asserts invariants, then
       best-effort tears the roster and settlement down. */
    public static class SpecRosterDestructiveTests
    {
        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void AssignSpecialist_AddsToRoster()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
            if (role is null) TestAssert.Skip("No SpecialistRoleDef loaded");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(p, role);
                TestAssert.AreEqual(1, comp.SpecialistCount);
                TestAssert.IsTrue(comp.Specialists[0].pawn == p, "entry should hold the assigned pawn");
                TestAssert.IsTrue(comp.Specialists[0].role == role, "entry should hold the assigned role");
                TestAssert.IsTrue(p.Faction == FindFC.EmpireFaction, "assigned pawn should join the empire");
                TestAssert.IsTrue(SpecialistRoster.GetRole(p) == role, "global roster should map pawn -> role");
                TestAssert.IsTrue(SpecialistRoster.IsAssigned(p), "global roster should mark pawn assigned");
                DestructiveTestUtil.AssertEmpireInvariants(f, "AssignSpecialist_AddsToRoster");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void AssignResident_AddsToResidents()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(p, null); // null role -> resident
                TestAssert.AreEqual(1, comp.ResidentCount);
                TestAssert.AreEqual(0, comp.SpecialistCount);
                TestAssert.IsTrue(comp.Residents[0].IsResident, "entry should be a resident");
                DestructiveTestUtil.AssertEmpireInvariants(f, "AssignResident_AddsToResidents");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void RecallSpecialist_RemovesAndClearsRoster()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
            if (role is null) TestAssert.Skip("No SpecialistRoleDef loaded");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(p, role);
                SettlementSpecialist entry = comp.Specialists[0];
                TestAssert.DoesNotThrow(() => comp.RecallSpecialist(entry), "RecallSpecialist threw");
                TestAssert.AreEqual(0, comp.SpecialistCount);
                TestAssert.IsFalse(SpecialistRoster.IsAssigned(p), "recalled pawn should leave the roster");
                DestructiveTestUtil.AssertEmpireInvariants(f, "RecallSpecialist_RemovesAndClearsRoster");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void AssignGovernor_SetsGovernor()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (focus is null) TestAssert.Skip("No GovernorFocusDef loaded");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignGovernor(p, focus);
                TestAssert.IsTrue(comp.HasGovernor, "settlement should have a governor");
                TestAssert.IsTrue(comp.Governor.pawn == p, "governor pawn should match");
                TestAssert.IsTrue(comp.Governor.focus == focus, "governor focus should match");
                TestAssert.IsTrue(SpecialistRoster.IsGovernor(p), "global roster should mark pawn as governor");
                DestructiveTestUtil.AssertEmpireInvariants(f, "AssignGovernor_SetsGovernor");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void AssignGovernor_Twice_SecondIgnored()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (focus is null) TestAssert.Skip("No GovernorFocusDef loaded");
            Pawn p1 = SpecDestructiveTestUtil.MakeAssignable(8);
            Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p1 is null || p2 is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignGovernor(p1, focus);
                comp.AssignGovernor(p2, focus); // should be rejected
                TestAssert.IsTrue(comp.Governor.pawn == p1, "the first governor should remain");
                TestAssert.IsFalse(SpecialistRoster.IsGovernor(p2), "second pawn should not be a governor");
                DestructiveTestUtil.AssertEmpireInvariants(f, "AssignGovernor_Twice_SecondIgnored");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void RecallGovernor_ClearsSlot()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (focus is null) TestAssert.Skip("No GovernorFocusDef loaded");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignGovernor(p, focus);
                TestAssert.DoesNotThrow(() => comp.RecallGovernor(), "RecallGovernor threw");
                TestAssert.IsFalse(comp.HasGovernor, "governor slot should be empty after recall");
                TestAssert.IsNull(comp.Governor, "governor reference should be null after recall");
                DestructiveTestUtil.AssertEmpireInvariants(f, "RecallGovernor_ClearsSlot");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void PromoteToGovernor_FromSpecialist()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (role is null || focus is null) TestAssert.Skip("Missing role/focus defs");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(p, role);
                comp.PromoteToGovernor(comp.Specialists[0], focus);
                TestAssert.IsTrue(comp.HasGovernor && comp.Governor.pawn == p, "pawn should become governor");
                TestAssert.AreEqual(0, comp.SpecialistCount);
                DestructiveTestUtil.AssertEmpireInvariants(f, "PromoteToGovernor_FromSpecialist");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void PromoteToGovernor_DemotesExistingGovToResident()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (role is null || focus is null) TestAssert.Skip("Missing role/focus defs");
            Pawn p1 = SpecDestructiveTestUtil.MakeAssignable(8);
            Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p1 is null || p2 is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignGovernor(p1, focus);
                comp.AssignSpecialist(p2, role);
                comp.PromoteToGovernor(comp.Specialists[0], focus); // promote p2
                TestAssert.IsTrue(comp.Governor.pawn == p2, "p2 should be the new governor");
                TestAssert.IsTrue(comp.Residents.Any(r => r.pawn == p1), "old governor p1 should be demoted to resident");
                DestructiveTestUtil.AssertEmpireInvariants(f, "PromoteToGovernor_DemotesExistingGovToResident");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void ChangeRole_SpecialistToResident_MovesList()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
            if (role is null) TestAssert.Skip("No SpecialistRoleDef loaded");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(p, role);
                comp.ChangeRole(comp.Specialists[0], null); // demote to resident
                TestAssert.AreEqual(0, comp.SpecialistCount);
                TestAssert.AreEqual(1, comp.ResidentCount);
                TestAssert.IsTrue(comp.Residents[0].role is null, "moved entry should have a null role");
                DestructiveTestUtil.AssertEmpireInvariants(f, "ChangeRole_SpecialistToResident_MovesList");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void ChangeGovernorFocus_RunsLifecycle()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            List<GovernorFocusDef> focuses = SpecDestructiveTestUtil.AllFocuses();
            if (focuses is null || focuses.Count < 2) TestAssert.Skip("Need >= 2 GovernorFocusDefs");
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                GovernorFocusDef focusA = focuses[0];
                GovernorFocusDef focusB = focuses[1];
                comp.AssignGovernor(p, focusA);
                TestAssert.DoesNotThrow(() => comp.ChangeGovernorFocus(focusB), "ChangeGovernorFocus threw");
                TestAssert.IsTrue(comp.Governor.focus == focusB, "governor focus should have changed");
                DestructiveTestUtil.AssertEmpireInvariants(f, "ChangeGovernorFocus_RunsLifecycle");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        /* DESTRUCTIVE regression guard for M7: supply-chain satisfaction must reset to 1 once the
           roster empties (the SC bridge stops writing it, so a stale <1 value would otherwise scale
           every future member's bonuses forever). It must NOT reset while any member remains. */
        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void RosterEmptied_ResetsSatisfaction()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
            if (role is null) TestAssert.Skip("No SpecialistRoleDef loaded");
            Pawn p1 = SpecDestructiveTestUtil.MakeAssignable(8);
            Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p1 is null || p2 is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(p1, role);
                comp.AssignSpecialist(p2, null); // resident
                comp.FoodSatisfaction = 0.3f;
                comp.MedicineSatisfaction = 0.4f;

                // Roster still non-empty after removing one -> satisfaction preserved.
                comp.RecallSpecialist(comp.Specialists[0]);
                TestAssert.AreEqual(0.3, comp.FoodSatisfaction, message: "satisfaction preserved while a member remains");
                TestAssert.AreEqual(0.4, comp.MedicineSatisfaction);

                // Removing the last member empties the roster -> both reset to 1.
                comp.RecallSpecialist(comp.Residents[0]);
                TestAssert.AreEqual(0, comp.TotalCount);
                TestAssert.AreEqual(1.0, comp.FoodSatisfaction, message: "food satisfaction resets on empty roster");
                TestAssert.AreEqual(1.0, comp.MedicineSatisfaction, message: "medicine satisfaction resets on empty roster");
                DestructiveTestUtil.AssertEmpireInvariants(f, "RosterEmptied_ResetsSatisfaction");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        /* DESTRUCTIVE regression guard for M7: the governor-recall path also resets satisfaction when
           it empties the roster. */
        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void RecallGovernor_LastMember_ResetsSatisfaction()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (focus is null) TestAssert.Skip("No GovernorFocusDef loaded");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignGovernor(p, focus);
                comp.FoodSatisfaction = 0.2f;
                comp.MedicineSatisfaction = 0.6f;

                comp.RecallGovernor();
                TestAssert.AreEqual(0, comp.TotalCount);
                TestAssert.AreEqual(1.0, comp.FoodSatisfaction, message: "food satisfaction resets on empty roster");
                TestAssert.AreEqual(1.0, comp.MedicineSatisfaction, message: "medicine satisfaction resets on empty roster");
                DestructiveTestUtil.AssertEmpireInvariants(f, "RecallGovernor_LastMember_ResetsSatisfaction");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        /* DESTRUCTIVE regression guard for H5: recall now routes through the layer-aware
           DeliverPawnsToPlayer. On a surface settlement (caravan-capable layer) the refactor must
           still form a player caravan at the settlement tile -- the orbital drop-pod branch can't be
           exercised here (transient settlements are surface-only) and is verified manually. */
        [EmpireDestructiveTest("SG.Destructive.Roster")]
        public static void RecallSpecialist_SurfaceFormsCaravan()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            if (!s.Tile.LayerDef.canFormCaravans) TestAssert.Skip("Transient settlement is not on a caravan-capable layer");
            SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
            if (role is null) TestAssert.Skip("No SpecialistRoleDef loaded");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(p, role);
                SettlementSpecialist entry = comp.Specialists[0];
                comp.RecallSpecialist(entry);

                TestAssert.AreEqual(0, comp.SpecialistCount);
                TestAssert.IsFalse(SpecialistRoster.IsAssigned(p), "recalled pawn should leave the roster");

                // The surface delivery path must still form a player caravan carrying the pawn.
                Caravan caravan = Find.WorldObjects.Caravans.FirstOrDefault(
                    c => c is object && !c.Destroyed && c.Tile == s.Tile && c.PawnsListForReading.Contains(p));
                TestAssert.IsTrue(caravan is object, "recall on a surface settlement should form a caravan carrying the pawn");

                DestructiveTestUtil.AssertEmpireInvariants(f, "RecallSpecialist_SurfaceFormsCaravan");
            }
            finally
            {
                List<Caravan> caravans = new List<Caravan>(Find.WorldObjects.Caravans);
                foreach (Caravan c in caravans)
                    if (c is object && !c.Destroyed && c.Tile == s.Tile)
                        c.Destroy();
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }
    }
}
