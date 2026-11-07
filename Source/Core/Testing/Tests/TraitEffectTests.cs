namespace FactionColonies.Specialists;

/// <summary>
/// Policy-trait effects resurrected against the def-driven model (Professional Army, Garrison
/// Doctrine, Meritocratic) plus the retired-Patrician cleanup. Trait-gated formulas mirror the
/// live trait state (the suite runs in a game where the trait is usually inactive, so the factor
/// resolves to the no-trait branch) so the wiring is verified without toggling faction policy.
/// </summary>
public static class TraitEffectTests
{
    private const int Lvl = 10; // every enabled skill set to 10 -> TaperedLevel(10) == 10

    // Reads a role's modifier value for a stat straight from the def, so the expectation tracks
    // the XML rather than a copied magic number.
    private static double StatModValue(SpecialistRoleDef role, FCStatDef stat)
    {
        if (role?.statModifiers is null) return 0;
        foreach (FCStatModifier m in role.statModifiers)
            if (m.stat == stat) return m.value;
        return 0;
    }

    [EmpireTest("SG.Trait")]
    public static void ProfessionalArmy_DoublesCommanderMilitary()
    {
        SpecialistRoleDef commander = TestAssert.NotNull(SpecialistRoleDefOf.Commander, "Commander role not loaded");
        FCStatDef mil = TestAssert.NotNull(FCStatDefOf.militaryBaseLevel, "militaryBaseLevel stat not loaded");
        Pawn p = SpecTestHelper.MakeControlledPawn(Lvl);
        SpecTestHelper.WithStandardTaper(() =>
        {
            SettlementSpecialist s = SpecTestHelper.Specialist(p, commander);
            double factor = SpecUtil.HasTrait(SpecPolicyDefOf.FCSprofessionalArmy) ? 2.0 : 1.0;
            double expected = StatModValue(commander, mil) * s.SkillScore * factor;
            TestAssert.AreEqual(expected, SpecUtil.SpecialistStatBonus(s, mil));
        });
    }

    [EmpireTest("SG.Trait")]
    public static void GarrisonHappiness_ScalesWithCommanderSkill()
    {
        SpecialistRoleDef commander = TestAssert.NotNull(SpecialistRoleDefOf.Commander, "Commander role not loaded");
        Pawn p = SpecTestHelper.MakeControlledPawn(Lvl);
        SpecTestHelper.WithStandardTaper(() =>
        {
            SettlementSpecialist s = SpecTestHelper.Specialist(p, commander);
            List<SettlementSpecialist> list = [s];
            double expected = SpecUtil.HasTrait(SpecPolicyDefOf.FCSgarrisonDoctrine)
                ? s.SkillScore * SpecUtil.GarrisonHappinessPerSkill
                : 0.0;
            TestAssert.AreEqual(expected, SpecUtil.GarrisonHappinessBonus(list));
        });
    }

    [EmpireTest("SG.Trait")]
    public static void GovernorEffectiveness_MatchesMeritocracy()
    {
        double expected = SpecUtil.HasTrait(SpecPolicyDefOf.FCSmeritocratic) ? 2.0 / 1.5 : 1.0;
        TestAssert.AreEqual(expected, SpecUtil.GovernorEffectiveness());
    }

    // The retired Patrician trait is scrubbed out of a faction's trait slots on load.
    [EmpireTest("SG.Trait")]
    public static void RemovePatricianTrait_ClearsAdoptedSlot()
    {
        TestAssert.NotNull(SpecPolicyDefOf.FCSpatrician, "FCSpatrician def not loaded");

        PolicyManager pm = new();
        pm.factionTraits[0] = new FCPolicy(SpecPolicyDefOf.FCSpatrician);
        TestAssert.IsTrue(pm.HasTrait(SpecPolicyDefOf.FCSpatrician), "precondition: trait adopted");

        bool removed = GameComponent_SpecialistRoster.RemovePatricianTrait(pm);

        TestAssert.IsTrue(removed, "reported a removal");
        TestAssert.IsFalse(pm.HasTrait(SpecPolicyDefOf.FCSpatrician), "trait cleared");
        TestAssert.AreEqual(FCPolicyDefOf.empty, pm.factionTraits[0].def);
        // Idempotent: a second pass over a clean manager finds nothing.
        TestAssert.IsFalse(GameComponent_SpecialistRoster.RemovePatricianTrait(pm), "second pass is a no-op");
    }

    // The dummy def is intentionally kept (so old saves resolve) but out of the Trait category.
    [EmpireTest("SG.Trait")]
    public static void Patrician_DefKeptButHidden()
    {
        TestAssert.IsNotNull(SpecPolicyDefOf.FCSpatrician, "dummy def still resolves");
        TestAssert.IsFalse(SpecPolicyDefOf.FCSpatrician.category == FCPolicyCategory.Trait,
            "no longer selectable as a Trait");
    }
}
