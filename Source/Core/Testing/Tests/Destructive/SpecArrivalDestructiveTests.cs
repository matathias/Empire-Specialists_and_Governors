namespace FactionColonies.Specialists;

/* DESTRUCTIVE: transport-pod arrival overflow. The arrival comps place pawns into the roster and
   must never drop one: a governor pod past the single slot overflows to residents, and a
   specialist pod past MaxSpecialists overflows to residents. Drives the real arrival comps on a
   transient settlement, then tears the roster and settlement down. */
public static class SpecArrivalDestructiveTests
{
    [EmpireDestructiveTest("SG.Destructive.Arrival")]
    public static void Arrival_Governor_OverflowsExtraPawnsToResidents()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        WorldObjectComp_PawnArrival_Governor arrival = s.GetComponent<WorldObjectComp_PawnArrival_Governor>();
        TestAssert.NotNull(arrival, "No governor-arrival comp (injected by InjectComp.xml)");
        SpecDestructiveTestUtil.AnyFocus(); // the governor arrival assigns a shipped focus
        Pawn p1 = SpecDestructiveTestUtil.MakeAssignable(8);
        Pawn p2 = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            arrival.ReceivePawns([p1, p2]);

            TestAssert.IsTrue(comp.HasGovernor && comp.Governor.pawn == p1, "first pawn should become governor");
            TestAssert.AreEqual(1, comp.ResidentCount, "the extra pawn should overflow to a resident");
            TestAssert.IsTrue(comp.Residents.Any(r => r.pawn == p2), "second pawn should be a resident");
            DestructiveTestUtil.AssertEmpireInvariants(f, "Arrival_Governor_OverflowsExtraPawnsToResidents");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("SG.Destructive.Arrival")]
    public static void Arrival_Specialist_OverflowsToResidents_WhenFull()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        WorldObjectComp_PawnArrival_Specialist arrival = s.GetComponent<WorldObjectComp_PawnArrival_Specialist>();
        TestAssert.NotNull(arrival, "No specialist-arrival comp (injected by InjectComp.xml)");
        const int max = 3;
        using SpecDestructiveTestUtil.SpecialistCapScope cap = SpecDestructiveTestUtil.PinMaxSpecialists(comp, max);
        // Uncapped synthetic role so filling hits the MaxSpecialists ceiling, not a per-role cap.
        SpecialistRoleDef fillRole = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f);
        Pawn extra = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            for (int i = 0; i < max; i++)
            {
                Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
                comp.AssignSpecialist(p, fillRole);
            }
            if (comp.SpecialistCount != max) TestAssert.Fail("Could not fill specialists to cap");

            arrival.ReceivePawns([extra]);

            TestAssert.AreEqual(max, comp.SpecialistCount, "specialist count should stay at the cap");
            TestAssert.AreEqual(1, comp.ResidentCount, "the over-cap pawn should overflow to a resident");
            TestAssert.IsTrue(comp.Residents.Any(r => r.pawn == extra), "over-cap pawn should be a resident");
            DestructiveTestUtil.AssertEmpireInvariants(f, "Arrival_Specialist_OverflowsToResidents_WhenFull");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }
}
