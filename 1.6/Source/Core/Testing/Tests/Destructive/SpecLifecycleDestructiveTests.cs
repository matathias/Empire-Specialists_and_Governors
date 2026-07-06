using Verse;

namespace FactionColonies.Specialists
{
    /* DESTRUCTIVE: drives SpecialistLifecycleHandler.OnBattleResolved's abstract death roll against
       real settlement comps -- it removes and kills a roster pawn. Regression guard for the
       "wrong settlement" bug: the roll must target the ATTACKED settlement (op.targetObject), not
       the reinforcing settlement whose squad happened to defend (op.defender.homeSettlement, which
       auto-defender selection overwrites to the foreign billet). Creates two transient settlements,
       resolves one auto-battle, asserts invariants, then tears everything down. */
    public static class SpecLifecycleDestructiveTests
    {
        [EmpireDestructiveTest("SG.Destructive.Lifecycle")]
        public static void OnBattleResolved_RollsAttackedSettlement_NotReinforcer()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC a = null;
            WorldSettlementFC b = null;
            WorldObjectCompProperties_SettlementSpecialists props = null;
            float savedResident = 0f, savedSpecialist = 0f, savedGovernor = 0f;
            WorldObjectComp_SettlementSpecialists compA = null;
            WorldObjectComp_SettlementSpecialists compB = null;
            try
            {
                // A = the settlement under attack (op.targetObject). B = the settlement whose squad was
                // sent to defend it, i.e. the value auto-defender selection writes into homeSettlement.
                a = DestructiveTestUtil.CreateTransientSettlement();
                b = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null || b is null) TestAssert.Skip("No valid tile(s)");

                compA = a.GetComponent<WorldObjectComp_SettlementSpecialists>();
                compB = b.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (compA is null || compB is null) TestAssert.Skip("No specialists comp");

                Pawn pawnA = SpecDestructiveTestUtil.MakeAssignable(8);
                Pawn pawnB = SpecDestructiveTestUtil.MakeAssignable(8);
                if (pawnA is null || pawnB is null) TestAssert.Skip("No pawn");

                // Props is the shared def-level CompProperties, so this forces a certain resident death
                // on defeat for BOTH comps. That is deliberate: if the handler wrongly rolled B, B's
                // resident would also die -- so the "B survives" assertion actually exercises the fix.
                props = compA.Props;
                savedResident = props.residentDeathChanceDefeat;
                savedSpecialist = props.specialistDeathChanceDefeat;
                savedGovernor = props.governorDeathChanceDefeat;
                props.residentDeathChanceDefeat = 1f;
                props.specialistDeathChanceDefeat = 0f;
                props.governorDeathChanceDefeat = 0f;

                // One resident each (null role -> resident). With no specialists present, the
                // defense-behavior death-chance reduction loop never runs, so residentDeathChanceDefeat
                // stays 1.0 and the kill is deterministic.
                compA.AssignSpecialist(pawnA, null);
                compB.AssignSpecialist(pawnB, null);
                if (compA.ResidentCount != 1 || compB.ResidentCount != 1) TestAssert.Skip("Roster setup failed");

                // Attacked settlement is A (targetObject); the defending squad's home is overwritten to B.
                MilitaryOperation op = new MilitaryOperation(-9999, MilitaryJobDefOf.DefendOwnSettlement, a.Tile, a);
                op.defender.faction = FindFC.EmpireFaction;
                op.defender.homeSettlement = b;
                BattleResult result = new BattleResult(); // wasManualBattle == false -> auto-resolved roll runs

                new SpecialistLifecycleHandler().OnBattleResolved(op, false, result);

                TestAssert.AreEqual(0, compA.ResidentCount); // attacked settlement took the casualty
                TestAssert.AreEqual(1, compB.ResidentCount); // reinforcer untouched -- the regression guard
                DestructiveTestUtil.AssertEmpireInvariants(f, "OnBattleResolved_RollsAttackedSettlement_NotReinforcer");
            }
            finally
            {
                if (props is object)
                {
                    props.residentDeathChanceDefeat = savedResident;
                    props.specialistDeathChanceDefeat = savedSpecialist;
                    props.governorDeathChanceDefeat = savedGovernor;
                }
                SpecDestructiveTestUtil.CleanupRoster(compA);
                SpecDestructiveTestUtil.CleanupRoster(compB);
                DestructiveTestUtil.SafeRemoveSettlement(a);
                DestructiveTestUtil.SafeRemoveSettlement(b);
            }
        }
    }
}
