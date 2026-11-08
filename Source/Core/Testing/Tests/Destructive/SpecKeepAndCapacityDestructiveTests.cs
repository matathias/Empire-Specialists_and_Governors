namespace FactionColonies.Specialists;

/* DESTRUCTIVE: roster behavior that needs a live settlement. Covers the forcefully-kept world
   pawn hold (taken on assign, dropped on recall and death, re-applied on load), the pod-arrival
   fallback to resident when a role is capped, slot and per-role caps with pending picks, member
   order, the best-resource scans, and quest-tied pawns being refused. Each test builds a
   transient settlement and tears it down; recalled pawns' caravans are disbanded and the pawns
   discarded best-effort. */
public static class SpecKeepAndCapacityDestructiveTests
{
    private static bool IsKept(Pawn p) => Find.WorldPawns.ForcefullyKeptPawns.Contains(p);

    // A recall hands the pawn back in a new player caravan; disband it and discard the pawn so the
    // test leaves no caravan behind.
    private static void DiscardRecalled(Pawn p)
    {
        if (p is null || p.Dead) return;
        Caravan caravan = p.GetCaravan();
        if (caravan is not null)
        {
            caravan.RemovePawn(p);
            if (!caravan.Destroyed && caravan.PawnsListForReading.Count == 0)
                caravan.Destroy();
        }
        if (p.Spawned) return;
        if (Find.WorldPawns.Contains(p))
            Find.WorldPawns.RemoveAndDiscardPawnViaGC(p);
    }

    /*-*-*- Forcefully kept hold -*-*-*/

    [EmpireDestructiveTest("SG.Destructive.Keep")]
    public static void AssignSpecialist_KeepsPawnForcefully()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            TestAssert.IsTrue(comp.AssignSpecialist(p, null), "resident assignment should succeed");
            TestAssert.IsTrue(p.IsWorldPawn(), "an assigned pawn should be a world pawn");
            TestAssert.IsTrue(IsKept(p), "an assigned pawn should be forcefully kept");
            DestructiveTestUtil.AssertEmpireInvariants(f, "AssignSpecialist_KeepsPawnForcefully");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    // Caravan and pod pawns are already world pawns when they are assigned; they must still be
    // added to the kept set.
    [EmpireDestructiveTest("SG.Destructive.Keep")]
    public static void AssignSpecialist_AlreadyWorldPawn_IsKept()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Decide);
            if (!p.IsWorldPawn() || IsKept(p)) TestAssert.Fail("Could not make an unkept world pawn");

            comp.AssignSpecialist(p, SpecDestructiveTestUtil.AnyRole());
            TestAssert.IsTrue(IsKept(p), "an already-world pawn should be forcefully kept once assigned");
            DestructiveTestUtil.AssertEmpireInvariants(f, "AssignSpecialist_AlreadyWorldPawn_IsKept");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("SG.Destructive.Keep")]
    public static void AssignGovernor_AlreadyWorldPawn_IsKept()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Decide);
            if (!p.IsWorldPawn() || IsKept(p)) TestAssert.Fail("Could not make an unkept world pawn");

            TestAssert.IsTrue(comp.AssignGovernor(p, SpecDestructiveTestUtil.AnyFocus()), "governor assignment should succeed");
            TestAssert.IsTrue(IsKept(p), "an already-world governor should be forcefully kept once assigned");
            DestructiveTestUtil.AssertEmpireInvariants(f, "AssignGovernor_AlreadyWorldPawn_IsKept");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("SG.Destructive.Keep")]
    public static void RecallSpecialist_DropsKeptHold()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            comp.AssignSpecialist(p, SpecDestructiveTestUtil.AnyRole());
            if (!IsKept(p)) TestAssert.Fail("Assigned pawn was not kept");

            comp.RecallSpecialist(comp.Specialists[0]);
            TestAssert.IsTrue(p.Faction == Faction.OfPlayer, "a recalled pawn should be the player's again");
            TestAssert.IsFalse(IsKept(p), "a recalled pawn should no longer be forcefully kept");
            DestructiveTestUtil.AssertEmpireInvariants(f, "RecallSpecialist_DropsKeptHold");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DiscardRecalled(p);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("SG.Destructive.Keep")]
    public static void RecallGovernor_DropsKeptHold()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            comp.AssignGovernor(p, SpecDestructiveTestUtil.AnyFocus());
            if (!IsKept(p)) TestAssert.Fail("Assigned governor was not kept");

            comp.RecallGovernor();
            TestAssert.IsTrue(p.Faction == Faction.OfPlayer, "a recalled governor should be the player's again");
            TestAssert.IsFalse(IsKept(p), "a recalled governor should no longer be forcefully kept");
            DestructiveTestUtil.AssertEmpireInvariants(f, "RecallGovernor_DropsKeptHold");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DiscardRecalled(p);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("SG.Destructive.Keep")]
    public static void MemberDeath_DropsKeptHold()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            comp.AssignSpecialist(p, null);
            if (!IsKept(p)) TestAssert.Fail("Assigned pawn was not kept");

            // The death funnel is driven directly, without killing the pawn.
            comp.NotifyMemberDied(p, SpecDeathCause.Other);
            TestAssert.AreEqual(0, comp.ResidentCount, "the member should leave the roster");
            TestAssert.IsFalse(IsKept(p), "a dead member should no longer be forcefully kept");
            DestructiveTestUtil.AssertEmpireInvariants(f, "MemberDeath_DropsKeptHold");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DiscardRecalled(p);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    // Saves from before the hold existed have unkept roster pawns; the load pass keeps them.
    [EmpireDestructiveTest("SG.Destructive.Keep")]
    public static void LoadPass_KeepsUnkeptRosterPawns()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            comp.AssignSpecialist(p, null);
            Find.WorldPawns.ForcefullyKeptPawns.Remove(p);
            if (IsKept(p)) TestAssert.Fail("Could not drop the kept hold");

            GameComponent_SpecialistRoster roster = TestAssert.NotNull(
                Current.Game.GetComponent<GameComponent_SpecialistRoster>(), "No GameComponent_SpecialistRoster");
            roster.FinalizeInit();
            TestAssert.IsTrue(IsKept(p), "the load pass should keep every living roster world pawn");
            TestAssert.IsTrue(SpecialistRoster.IsAssigned(p), "the load pass should keep the roster assignment");
            DestructiveTestUtil.AssertEmpireInvariants(f, "LoadPass_KeepsUnkeptRosterPawns");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    /*-*-*- Pod arrival never drops a pawn -*-*-*/

    // A pawn with no skills picks the Generalist; with the Generalist at its cap the pod pawn
    // must land as a resident instead of being rejected.
    [EmpireDestructiveTest("SG.Destructive.Arrival")]
    public static void Arrival_Specialist_CappedRole_FallsBackToResident()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        WorldObjectComp_PawnArrival_Specialist arrival = TestAssert.NotNull(
            s.GetComponent<WorldObjectComp_PawnArrival_Specialist>(), "No specialist-arrival comp (injected by InjectComp.xml)");
        SpecialistRoleDef generalist = TestAssert.NotNull(SpecialistRoleDefOf.Generalist, "Generalist role not loaded");
        int savedCap = generalist.maxPerSettlement;
        using SpecDestructiveTestUtil.SpecialistCapScope cap = SpecDestructiveTestUtil.EnsureMaxSpecialists(comp, 3);
        Pawn filler = SpecDestructiveTestUtil.MakeAssignable(8);
        Pawn arriving = SpecDestructiveTestUtil.MakeAssignable(0);
        try
        {
            generalist.maxPerSettlement = 1;
            TestAssert.IsTrue(SpecUtil.BestSpecialistRole(arriving, comp) == generalist,
                "a pawn with no skills should pick the Generalist");
            TestAssert.IsTrue(comp.AssignSpecialist(filler, generalist), "the filler should take the only Generalist slot");
            TestAssert.IsTrue(comp.RoleAtCap(generalist), "the Generalist should now be at its cap");

            arrival.ReceivePawns([arriving]);

            TestAssert.IsTrue(comp.Residents.Any(r => r.pawn == arriving), "the arriving pawn should become a resident");
            TestAssert.AreEqual(1, comp.SpecialistCount, "the capped role should keep a single holder");
            TestAssert.IsTrue(SpecialistRoster.IsAssigned(arriving), "the arriving pawn should be on the roster");
            DestructiveTestUtil.AssertEmpireInvariants(f, "Arrival_Specialist_CappedRole_FallsBackToResident");
        }
        finally
        {
            generalist.maxPerSettlement = savedCap;
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    /*-*-*- Caps with pending picks -*-*-*/

    [EmpireDestructiveTest("SG.Destructive.Capacity")]
    public static void RoleAtCap_CountsHoldersAndPending()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        using SpecDestructiveTestUtil.SpecialistCapScope cap = SpecDestructiveTestUtil.EnsureMaxSpecialists(comp, 3);
        SpecialistRoleDef capped = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f, maxPerSettlement: 2);
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            TestAssert.IsTrue(comp.AssignSpecialist(p, capped), "the first holder should be accepted");
            TestAssert.IsFalse(comp.RoleAtCap(capped), "one holder of two: not at cap");
            TestAssert.IsFalse(comp.RoleAtCap(capped, 0), "one holder of two, nothing pending: not at cap");
            TestAssert.IsTrue(comp.RoleAtCap(capped, 1), "one holder plus one pending reaches the cap of two");
            DestructiveTestUtil.AssertEmpireInvariants(f, "RoleAtCap_CountsHoldersAndPending");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("SG.Destructive.Capacity")]
    public static void SpecialistSlotsFull_CountsSpecialistsAndPending()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        using SpecDestructiveTestUtil.SpecialistCapScope cap = SpecDestructiveTestUtil.PinMaxSpecialists(comp, 3);
        SpecialistRoleDef role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f);
        Pawn spec = SpecDestructiveTestUtil.MakeAssignable(8);
        Pawn resident = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            comp.AssignSpecialist(spec, role);
            comp.AssignSpecialist(resident, null); // residents take no specialist slot
            TestAssert.IsFalse(comp.SpecialistSlotsFull(), "one of three slots used");
            TestAssert.IsFalse(comp.SpecialistSlotsFull(1), "one used plus one pending of three");
            TestAssert.IsTrue(comp.SpecialistSlotsFull(2), "one used plus two pending fills three slots");
            TestAssert.IsTrue(comp.CanAssignSpecialist(role), "a free slot should accept a specialist");
            DestructiveTestUtil.AssertEmpireInvariants(f, "SpecialistSlotsFull_CountsSpecialistsAndPending");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    /*-*-*- Member walk -*-*-*/

    [EmpireDestructiveTest("SG.Destructive.Roster")]
    public static void Members_SpecialistsThenResidentsThenGovernor()
    {
        FactionFC f = DestructiveTestUtil.RequireFaction();
        WorldSettlementFC s = SpecDestructiveTestUtil.CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp);
        using SpecDestructiveTestUtil.SpecialistCapScope cap = SpecDestructiveTestUtil.EnsureMaxSpecialists(comp, 3);
        Pawn gov = SpecDestructiveTestUtil.MakeAssignable(8);
        Pawn res = SpecDestructiveTestUtil.MakeAssignable(8);
        Pawn spec = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            // Assigned out of order, so the walk order comes from the lists, not assignment time.
            comp.AssignGovernor(gov, SpecDestructiveTestUtil.AnyFocus());
            comp.AssignSpecialist(res, null);
            comp.AssignSpecialist(spec, SpecDestructiveTestUtil.AnyRole());

            List<Pawn> walk = comp.Members.Select(m => m.pawn).ToList();
            TestAssert.AreEqual(3, walk.Count, "every member should be walked once");
            TestAssert.IsTrue(walk[0] == spec && walk[1] == res && walk[2] == gov,
                "members should be specialists, then residents, then the governor");
            TestAssert.IsTrue(comp.Members.Last() is SettlementGovernor, "the governor entry should come last");
            TestAssert.IsTrue(comp.AllPawnsSnapshot().SequenceEqual(walk), "the snapshot should follow the member walk");
            TestAssert.IsTrue(SpecUtil.FindOwningComp(gov) == comp, "the governor should resolve to its comp");
            DestructiveTestUtil.AssertEmpireInvariants(f, "Members_SpecialistsThenResidentsThenGovernor");
        }
        finally
        {
            SpecDestructiveTestUtil.CleanupRoster(comp);
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    /*-*-*- Best-resource scans (only read the settlement's resource list) -*-*-*/

    private static List<ResourceTypeDef> TwoResources(WorldSettlementFC s)
    {
        List<ResourceTypeDef> defs = s.Resources?.Select(r => r.def).Where(d => d is not null).Distinct().ToList() ?? [];
        if (defs.Count < 2) TestAssert.Fail($"Settlement '{s.Name}' has fewer than two resources");
        return defs;
    }

    private static SpecialistRoleDef BonusRole(ResourceTypeDef a, float aValue, ResourceTypeDef b, float bValue)
    {
        SpecialistRoleDef role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, a, aValue);
        role.resourceBonuses.Add(new() { resource = b, baseValue = bValue });
        return role;
    }

    [EmpireDestructiveTest("SG.Destructive.Modifiers")]
    public static void BestSpecialistResource_PicksLargestBonus_TiesKeepFirst()
    {
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            List<ResourceTypeDef> defs = TwoResources(s);
            ResourceTypeDef first = defs[0], second = defs[1];

            SettlementSpecialist larger = SpecTestHelper.Specialist(p, BonusRole(first, 0.01f, second, 0.02f));
            TestAssert.IsTrue(SpecUtil.BestSpecialistResource(larger, s, out double bonus) == second,
                "the resource with the larger bonus should win");
            TestAssert.AreEqual(SpecUtil.SpecialistAdditiveForResource(larger, second), bonus, 0.000001,
                "the reported bonus should be that resource's bonus");

            SettlementSpecialist tied = SpecTestHelper.Specialist(p, BonusRole(first, 0.02f, second, 0.02f));
            TestAssert.IsTrue(SpecUtil.BestSpecialistResource(tied, s, out _) == first,
                "on a tie the earlier resource in settlement order should win");

            SettlementSpecialist none = SpecTestHelper.Specialist(p, SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f));
            TestAssert.IsNull(SpecUtil.BestSpecialistResource(none, s, out double zero), "no bonus anywhere: no best resource");
            TestAssert.AreEqual(0.0, zero);
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    [EmpireDestructiveTest("SG.Destructive.Modifiers")]
    public static void BestGovernorResource_PicksLargestMultiplier()
    {
        WorldSettlementFC s = DestructiveTestUtil.RequireTransientSettlement();
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        try
        {
            List<ResourceTypeDef> defs = TwoResources(s);
            ResourceTypeDef second = defs[1];

            SettlementGovernor g = SpecTestHelper.Governor(p, SpecTestHelper.Focus(SkillDefOf.Plants, 1f, second, 0.01f));
            TestAssert.IsTrue(SpecUtil.BestGovernorResource(g, s, out double mult) == second,
                "the boosted resource should win");
            TestAssert.GreaterThan(mult, 1.0, "the boosted resource's multiplier should exceed 1");

            // With no bonus every resource sits at 1.0, so the first one is reported at 1.0 (callers
            // only show multipliers above 1).
            SettlementGovernor flat = SpecTestHelper.Governor(p, SpecTestHelper.Focus(SkillDefOf.Plants, 1f, null, 0f));
            TestAssert.IsTrue(SpecUtil.BestGovernorResource(flat, s, out double flatMult) == defs[0],
                "with equal multipliers the first resource should be reported");
            TestAssert.AreEqual(1.0, flatMult, 0.000001);
        }
        finally
        {
            DestructiveTestUtil.SafeRemoveSettlement(s);
        }
    }

    /*-*-*- Quest-tied pawns are not assignable -*-*-*/

    // Adds a bare quest holding the part, marks it accepted without initiating it (so no part
    // activates), runs the check, then removes the quest.
    private static void WithQuestPart(QuestPart part, Action check)
    {
        Quest quest = Quest.MakeRaw();
        quest.AddPart(part);
        Find.QuestManager.Add(quest);
        try
        {
            quest.SetInitiallyAccepted();
            if (quest.State != QuestState.Ongoing) TestAssert.Fail("Could not make an ongoing quest");
            check();
        }
        finally
        {
            Find.QuestManager.Remove(quest);
        }
    }

    [EmpireDestructiveTest("SG.Destructive.Assignable")]
    public static void IsAssignablePawn_BorrowedPawn_IsRejected()
    {
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        TestAssert.IsTrue(SpecUtil.IsAssignablePawn(p), "precondition: the pawn is assignable before the loan");
        QuestPart_LendColonistsToFaction lend = new() { inSignalEnable = "EmpireSpecTest.NeverSent" };
        lend.LentColonistsListForReading.Add(p);
        WithQuestPart(lend, () =>
        {
            TestAssert.IsTrue(p.IsBorrowedByAnyFaction(), "the quest should mark the pawn as lent");
            TestAssert.IsFalse(SpecUtil.IsAssignablePawn(p), "a lent pawn must not be assignable");
        });
        TestAssert.IsTrue(SpecUtil.IsAssignablePawn(p), "the pawn is assignable again once the quest is gone");
    }

    [EmpireDestructiveTest("SG.Destructive.Assignable")]
    public static void IsAssignablePawn_QuestLodger_IsRejected()
    {
        Pawn p = SpecDestructiveTestUtil.MakeAssignable(8);
        Faction home = TestAssert.NotNull(FindFC.EmpireFaction, "No Empire faction");
        QuestPart_ExtraFaction lodger = new()
        {
            inSignalEnable = "EmpireSpecTest.NeverSent",
            extraFaction = new ExtraFaction(home, ExtraFactionType.HomeFaction),
            affectedPawns = [p]
        };
        WithQuestPart(lodger, () =>
        {
            TestAssert.IsTrue(p.IsQuestLodger(), "the quest should make the pawn a lodger");
            TestAssert.IsFalse(SpecUtil.IsAssignablePawn(p), "a quest lodger must not be assignable");
        });
    }
}
