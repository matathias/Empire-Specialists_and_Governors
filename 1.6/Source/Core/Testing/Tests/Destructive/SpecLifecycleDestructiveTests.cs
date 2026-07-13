using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
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

        /* DESTRUCTIVE regression guard for H2: a Deploy op (mercs called in to a manual defense)
           targets the player's own settlement but is aggressor-side with no defender faction, so
           IsDefensive == false. The abstract death roll must be skipped -- otherwise a WON manual
           defense re-rolls DEFEAT death chances against the survivors. Forces a certain-kill defeat
           chance, then asserts the resident survives because the op is not defensive. */
        [EmpireDestructiveTest("SG.Destructive.Lifecycle")]
        public static void OnBattleResolved_DeployOpDoesNotRoll()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC a = null;
            WorldObjectCompProperties_SettlementSpecialists props = null;
            float savedResident = 0f, savedSpecialist = 0f, savedGovernor = 0f;
            WorldObjectComp_SettlementSpecialists compA = null;
            try
            {
                a = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null) TestAssert.Skip("No valid tile");

                compA = a.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (compA is null) TestAssert.Skip("No specialists comp");

                Pawn pawnA = SpecDestructiveTestUtil.MakeAssignable(8);
                if (pawnA is null) TestAssert.Skip("No pawn");

                // Certain resident kill on defeat -- so if the roll wrongly ran, the resident dies.
                props = compA.Props;
                savedResident = props.residentDeathChanceDefeat;
                savedSpecialist = props.specialistDeathChanceDefeat;
                savedGovernor = props.governorDeathChanceDefeat;
                props.residentDeathChanceDefeat = 1f;
                props.specialistDeathChanceDefeat = 0f;
                props.governorDeathChanceDefeat = 0f;

                compA.AssignSpecialist(pawnA, null); // null role -> resident
                if (compA.ResidentCount != 1) TestAssert.Skip("Roster setup failed");

                // Deploy op mirroring CreateDeployOp: targetObject is the player's own settlement,
                // aggressor is the empire, and there is NO defender faction -> IsDefensive == false.
                MilitaryOperation op = new MilitaryOperation(-9998, MilitaryJobDefOf.Deploy, a.Tile, a);
                op.aggressor.faction = FindFC.EmpireFaction;
                BattleResult result = new BattleResult(); // wasManualBattle == false

                new SpecialistLifecycleHandler().OnBattleResolved(op, false, result);

                TestAssert.AreEqual(1, compA.ResidentCount); // roll skipped -- resident survives
                DestructiveTestUtil.AssertEmpireInvariants(f, "OnBattleResolved_DeployOpDoesNotRoll");
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
                DestructiveTestUtil.SafeRemoveSettlement(a);
            }
        }

        /* DESTRUCTIVE regression guard: removing a settlement must disband its roster --
           every member cleared from the static SpecialistRoster (no stale thingIDNumbers) and the
           survivors returned to the player in a caravan. Without the ISettlementListener handler the
           pawns leak as invisible Empire-faction world pawns and the static roster stays stale. */
        [EmpireDestructiveTest("SG.Destructive.Lifecycle")]
        public static void OnSettlementRemoved_DisbandsRosterAndClearsStaticRoster()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC a = null;
            WorldObjectComp_SettlementSpecialists comp = null;
            Pawn specPawn = null, resPawn = null, govPawn = null;
            try
            {
                a = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null) TestAssert.Skip("No valid tile");
                comp = a.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) TestAssert.Skip("No specialists comp");
                if (comp.MaxSpecialists < 1) TestAssert.Skip("MaxSpecialists too low");

                SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
                GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
                if (role is null || focus is null) TestAssert.Skip("Missing role/focus defs");

                specPawn = SpecDestructiveTestUtil.MakeAssignable(6);
                resPawn = SpecDestructiveTestUtil.MakeAssignable(6);
                govPawn = SpecDestructiveTestUtil.MakeAssignable(6);
                if (specPawn is null || resPawn is null || govPawn is null) TestAssert.Skip("No pawn");

                comp.AssignSpecialist(specPawn, role);
                comp.AssignSpecialist(resPawn, null); // resident
                comp.AssignGovernor(govPawn, focus);
                if (comp.TotalCount != 3) TestAssert.Skip("Roster setup failed");

                new SpecialistLifecycleHandler().OnSettlementRemoved(a);

                // Roster emptied and every member cleared from the static roster (the core leak guard).
                TestAssert.AreEqual(0, comp.TotalCount);
                TestAssert.IsFalse(comp.HasGovernor, "governor should be cleared");
                TestAssert.IsFalse(SpecialistRoster.IsAssigned(specPawn), "specialist not in static roster");
                TestAssert.IsFalse(SpecialistRoster.IsAssigned(resPawn), "resident not in static roster");
                TestAssert.IsFalse(SpecialistRoster.IsGovernor(govPawn), "governor not in static roster");
                TestAssert.IsFalse(SpecialistRoster.IsAssigned(govPawn), "ex-governor not in static roster");

                // Survivors returned to the player.
                TestAssert.IsTrue(specPawn.Faction == Faction.OfPlayer, "specialist returned to player");
                TestAssert.IsTrue(govPawn.Faction == Faction.OfPlayer, "governor returned to player");

                DestructiveTestUtil.AssertEmpireInvariants(f, "OnSettlementRemoved_DisbandsRosterAndClearsStaticRoster");
            }
            finally
            {
                // The disband forms a caravan of the survivors at the tile; destroy it so the pawns
                // don't linger, then clear any static-roster residue and remove the settlement.
                if (a is object)
                {
                    List<Caravan> caravans = new List<Caravan>(Find.WorldObjects.Caravans);
                    foreach (Caravan c in caravans)
                        if (c is object && !c.Destroyed && c.Tile == a.Tile)
                            c.Destroy();
                }
                SpecialistRoster.Recall(specPawn);
                SpecialistRoster.Recall(resPawn);
                SpecialistRoster.Recall(govPawn);
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(a);
            }
        }
    }
}
