namespace FactionColonies.Specialists;

/* DESTRUCTIVE: roster capacity rules -- max specialists, per-role cap, single governor slot. */
public static class SpecCapacityDestructiveTests
{
    [EmpireDestructiveTest("SG.Destructive.Capacity")]
    public static void AssignSpecialist_RespectsMaxSpecialists()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        const int max = 3;
        using SpecDestructiveTestUtil.SpecialistCapScope cap = SpecDestructiveTestUtil.PinMaxSpecialists(comp, max);

        List<Pawn> pawns = [];
        for (int i = 0; i < max + 1; i++)
        {
            Pawn pw = SpecDestructiveTestUtil.MakeAssignable(5);
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
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        using SpecDestructiveTestUtil.SpecialistCapScope cap = SpecDestructiveTestUtil.EnsureMaxSpecialists(comp, 3);
        Pawn p1 = SpecDestructiveTestUtil.MakeAssignable(5);
        Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(5);
        Pawn p3 = SpecDestructiveTestUtil.MakeAssignable(5);
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
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(8);
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
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        using SpecDestructiveTestUtil.SpecialistCapScope cap = SpecDestructiveTestUtil.EnsureMaxSpecialists(comp, 2);
        SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
        GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
        Pawn spec1 = SpecDestructiveTestUtil.MakeAssignable(6);
        Pawn spec2 = SpecDestructiveTestUtil.MakeAssignable(6);
        Pawn resident = SpecDestructiveTestUtil.MakeAssignable(6);
        Pawn gov = SpecDestructiveTestUtil.MakeAssignable(6);
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
