using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    /* DESTRUCTIVE: roster capacity rules -- max specialists, per-role cap, single governor slot. */
    public static class SpecCapacityDestructiveTests
    {
        [EmpireDestructiveTest("SG.Destructive.Capacity")]
        public static void AssignSpecialist_RespectsMaxSpecialists()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            int max = comp.MaxSpecialists;
            if (max <= 0 || max > 12) TestAssert.Skip("MaxSpecialists out of testable range: " + max);

            List<Pawn> pawns = new List<Pawn>();
            for (int i = 0; i < max + 1; i++)
            {
                Pawn pw = SpecDestructiveTestUtil.MakeAssignable(5);
                if (pw is null) TestAssert.Skip("No pawn");
                pawns.Add(pw);
            }
            try
            {
                SpecialistRoleDef role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f);
                for (int i = 0; i < max; i++)
                {
                    TestAssert.IsTrue(comp.CanAssignSpecialist(role), "CanAssignSpecialist should be true below cap");
                    TestAssert.IsTrue(comp.AssignSpecialist(pawns[i], role), "AssignSpecialist should return true below cap");
                }
                TestAssert.AreEqual(max, comp.SpecialistCount);

                TestAssert.IsFalse(comp.CanAssignSpecialist(role), "CanAssignSpecialist should be false at cap");
                TestAssert.IsTrue(comp.CanAssignSpecialist(null), "residents (null role) should always fit");
                TestAssert.IsFalse(comp.AssignSpecialist(pawns[max], role), "AssignSpecialist over cap should return false");
                TestAssert.AreEqual(max, comp.SpecialistCount);
                DestructiveTestUtil.AssertEmpireInvariants(f, "AssignSpecialist_RespectsMaxSpecialists");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Capacity")]
        public static void AssignSpecialist_RespectsPerRoleCap()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            if (comp.MaxSpecialists < 3) TestAssert.Skip("MaxSpecialists too low for per-role-cap test");
            Pawn p1 = SpecDestructiveTestUtil.MakeAssignable(5);
            Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(5);
            Pawn p3 = SpecDestructiveTestUtil.MakeAssignable(5);
            if (p1 is null || p2 is null || p3 is null) TestAssert.Skip("No pawn");
            try
            {
                SpecialistRoleDef role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f, maxPerSettlement: 2);
                TestAssert.IsTrue(comp.AssignSpecialist(p1, role), "first of role should return true");
                TestAssert.IsTrue(comp.AssignSpecialist(p2, role), "second of role should return true");
                TestAssert.IsFalse(comp.CanAssignSpecialist(role), "CanAssignSpecialist should be false at per-role cap");
                TestAssert.IsFalse(comp.AssignSpecialist(p3, role), "third of role should be rejected by per-role cap");
                TestAssert.AreEqual(2, comp.Specialists.Count(x => x.role == role));
                DestructiveTestUtil.AssertEmpireInvariants(f, "AssignSpecialist_RespectsPerRoleCap");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Capacity")]
        public static void AssignGovernor_SingleSlot()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (focus is null) TestAssert.Skip("No GovernorFocusDef loaded");
            Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
            Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(8);
            if (p is null || p2 is null) TestAssert.Skip("No pawn");
            try
            {
                TestAssert.IsTrue(comp.CanAssignGovernor, "CanAssignGovernor should be true with no governor");
                TestAssert.IsTrue(comp.AssignGovernor(p, focus), "first governor assignment should return true");
                TestAssert.IsTrue(comp.HasGovernor, "should have a governor");

                TestAssert.IsFalse(comp.CanAssignGovernor, "CanAssignGovernor should be false once a governor exists");
                TestAssert.IsFalse(comp.AssignGovernor(p2, focus), "second governor assignment should be rejected");
                TestAssert.AreEqual(1, comp.TotalCount); // exactly one slot used by the governor
                DestructiveTestUtil.AssertEmpireInvariants(f, "AssignGovernor_SingleSlot");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }

        [EmpireDestructiveTest("SG.Destructive.Capacity")]
        public static void Counts_Consistency()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            if (comp.MaxSpecialists < 2) TestAssert.Skip("MaxSpecialists too low");
            SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (role is null || focus is null) TestAssert.Skip("Missing role/focus defs");
            Pawn spec1 = SpecDestructiveTestUtil.MakeAssignable(6);
            Pawn spec2 = SpecDestructiveTestUtil.MakeAssignable(6);
            Pawn resident = SpecDestructiveTestUtil.MakeAssignable(6);
            Pawn gov = SpecDestructiveTestUtil.MakeAssignable(6);
            if (spec1 is null || spec2 is null || resident is null || gov is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(spec1, role);
                comp.AssignSpecialist(spec2, role);
                comp.AssignSpecialist(resident, null);
                comp.AssignGovernor(gov, focus);

                TestAssert.AreEqual(2, comp.SpecialistCount);
                TestAssert.AreEqual(1, comp.ResidentCount);
                TestAssert.IsTrue(comp.HasGovernor, "should have a governor");
                TestAssert.AreEqual(4, comp.TotalCount);
                DestructiveTestUtil.AssertEmpireInvariants(f, "Counts_Consistency");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
            }
        }
    }
}
