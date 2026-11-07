namespace FactionColonies.Specialists;

/// <summary>
/// Specialists &amp; Governors helpers for the destructive test tier. Builds on the base mod's
/// <see cref="DestructiveTestUtil"/> (transient-settlement create/teardown + invariant battery)
/// and adds the fixtures the roster tests share: a transient settlement with its roster comp,
/// assignable pawns, shipped def lookups, a pinned specialist cap, and a best-effort roster
/// teardown. Every fixture forces its state and fails the test when it cannot.
/// </summary>
public static class SpecDestructiveTestUtil
{
    // An assignable colonist with every skill at `level`. Fails the test if generation fails.
    public static Pawn MakeAssignable(int level)
    {
        return SpecTestHelper.MakeControlledPawn(level);
    }

    // The roster comp this mod injects into every surface/orbital settlement def, so a settlement
    // without it is a failure, not a precondition.
    public static WorldObjectComp_SettlementSpecialists RequireRoster(WorldSettlementFC settlement)
    {
        return TestAssert.NotNull(settlement.GetComponent<WorldObjectComp_SettlementSpecialists>(),
            $"Settlement '{settlement.Name}' has no WorldObjectComp_SettlementSpecialists (injected by InjectComp.xml)");
    }

    // A fresh transient surface settlement plus its (empty) roster comp, in one call.
    public static WorldSettlementFC CreateRosterSettlement(out WorldObjectComp_SettlementSpecialists comp)
    {
        WorldSettlementFC settlement = DestructiveTestUtil.RequireTransientSettlement();
        comp = settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
        if (comp is null)
        {
            DestructiveTestUtil.SafeRemoveSettlement(settlement);
            RequireRoster(settlement);
        }
        return settlement;
    }

    public static SpecialistRoleDef AnyRole()
    {
        return TestAssert.NotNull(DefDatabase<SpecialistRoleDef>.AllDefsListForReading.FirstOrDefault(),
            "No SpecialistRoleDef loaded");
    }

    public static GovernorFocusDef AnyFocus()
    {
        return TestAssert.NotNull(DefDatabase<GovernorFocusDef>.AllDefsListForReading.FirstOrDefault(),
            "No GovernorFocusDef loaded");
    }

    public static List<GovernorFocusDef> AllFocuses()
    {
        return DefDatabase<GovernorFocusDef>.AllDefsListForReading;
    }

    // Pins comp.MaxSpecialists to exactly `max` by adjusting the global specialistBaseMax setting
    // (the level- and policy-derived part of the cap is left as is), restoring the setting on
    // dispose. Use as `using SpecialistCapScope cap = PinMaxSpecialists(comp, 3);`.
    public static SpecialistCapScope PinMaxSpecialists(WorldObjectComp_SettlementSpecialists comp, int max)
    {
        SpecialistCapScope scope = new(FCSSettings.specialistBaseMax);
        FCSSettings.specialistBaseMax += max - comp.MaxSpecialists;
        if (comp.MaxSpecialists != max)
        {
            scope.Dispose();
            TestAssert.Fail($"Could not pin MaxSpecialists to {max}");
        }
        return scope;
    }

    // Raises comp.MaxSpecialists to at least `atLeast` (leaving a higher cap alone) for the
    // lifetime of the returned scope.
    public static SpecialistCapScope EnsureMaxSpecialists(WorldObjectComp_SettlementSpecialists comp, int atLeast)
    {
        return PinMaxSpecialists(comp, Math.Max(comp.MaxSpecialists, atLeast));
    }

    public readonly struct SpecialistCapScope(int savedBaseMax) : IDisposable
    {
        public void Dispose() => FCSSettings.specialistBaseMax = savedBaseMax;
    }

    // Clears the global SpecialistRoster of every pawn this comp tracks. RemoveSpecialist already
    // recalls specialists/residents from the roster (no caravan); the governor has no caravan-free
    // removal, so we recall its pawn directly. The comp itself is discarded when the transient
    // settlement is torn down.
    public static void CleanupRoster(WorldObjectComp_SettlementSpecialists comp)
    {
        if (comp is null) return;
        foreach (SettlementSpecialist s in comp.Specialists.ToList())
            comp.RemoveSpecialist(s);
        foreach (SettlementSpecialist r in comp.Residents.ToList())
            comp.RemoveSpecialist(r);
        if (comp.HasGovernor && comp.Governor?.pawn is not null)
            SpecialistRoster.Recall(comp.Governor.pawn);
    }
}
