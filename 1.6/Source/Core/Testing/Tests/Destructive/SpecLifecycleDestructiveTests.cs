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
    /* Test-only role behavior: records that OnBattleResolved invoked ModifyDeathChances and zeroes
       every death chance so the abstract roll's outcome is deterministic (nobody dies). Public with
       a parameterless ctor so SpecialistRoleBehaviorExtension.CreateBehavior can Activator-instantiate
       it. */
    public class SpecSpyRoleBehavior : SpecialistRoleBehavior
    {
        public static int InvokeCount;

        public override void ModifyDeathChances(WorldSettlementFC settlement, float skillScore,
            ref float govChance, ref float specChance, ref float residentChance)
        {
            InvokeCount++;
            govChance = 0f;
            specChance = 0f;
            residentChance = 0f;
        }
    }

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

        /* DESTRUCTIVE: OnBattleResolved must run each Commander specialist's ModifyDeathChances before
           rolling. A spy behavior zeroes every chance, so with certain (1.0) resident death configured
           the resident surviving proves the behavior loop ran (and the spy's invocation flag confirms
           it). Guards the isolation-tested RoleBehavior against being silently skipped by the handler. */
        [EmpireDestructiveTest("SG.Destructive.Lifecycle")]
        public static void OnBattleResolved_InvokesRoleBehaviorDeathModifiers()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC a = null;
            WorldObjectCompProperties_SettlementSpecialists props = null;
            float savedResident = 0f, savedSpecialist = 0f, savedGovernor = 0f;
            WorldObjectComp_SettlementSpecialists comp = null;
            try
            {
                a = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null) TestAssert.Skip("No valid tile");
                comp = a.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) TestAssert.Skip("No specialists comp");
                if (comp.MaxSpecialists < 1) TestAssert.Skip("MaxSpecialists too low");

                Pawn specPawn = SpecDestructiveTestUtil.MakeAssignable(8);
                Pawn resPawn = SpecDestructiveTestUtil.MakeAssignable(8);
                if (specPawn is null || resPawn is null) TestAssert.Skip("No pawn");

                // Certain death on defeat for everyone: without the behavior loop the resident dies.
                props = comp.Props;
                savedResident = props.residentDeathChanceDefeat;
                savedSpecialist = props.specialistDeathChanceDefeat;
                savedGovernor = props.governorDeathChanceDefeat;
                props.residentDeathChanceDefeat = 1f;
                props.specialistDeathChanceDefeat = 1f;
                props.governorDeathChanceDefeat = 1f;

                // Specialist whose role carries the spy behavior + a resident as the observable control.
                SpecialistRoleDef spyRole = new SpecialistRoleDef
                {
                    defName = "TEST_SpyRole",
                    label = "spy role",
                    modExtensions = new List<DefModExtension>
                    {
                        new SpecialistRoleBehaviorExtension { behaviorClass = typeof(SpecSpyRoleBehavior) }
                    }
                };
                SpecSpyRoleBehavior.InvokeCount = 0;
                comp.AssignSpecialist(specPawn, spyRole);
                comp.AssignSpecialist(resPawn, null);
                if (comp.SpecialistCount != 1 || comp.ResidentCount != 1) TestAssert.Skip("Roster setup failed");

                MilitaryOperation op = new MilitaryOperation(-9996, MilitaryJobDefOf.DefendOwnSettlement, a.Tile, a);
                op.defender.faction = FindFC.EmpireFaction;
                BattleResult result = new BattleResult(); // wasManualBattle == false -> auto-resolved roll runs

                new SpecialistLifecycleHandler().OnBattleResolved(op, false, result);

                TestAssert.IsTrue(SpecSpyRoleBehavior.InvokeCount > 0, "OnBattleResolved should invoke the role behavior");
                TestAssert.AreEqual(1, comp.ResidentCount); // spy zeroed residentChance -> resident survives
                TestAssert.AreEqual(1, comp.SpecialistCount); // spy zeroed specChance -> the spy specialist survives
                DestructiveTestUtil.AssertEmpireInvariants(f, "OnBattleResolved_InvokesRoleBehaviorDeathModifiers");
            }
            finally
            {
                if (props is object)
                {
                    props.residentDeathChanceDefeat = savedResident;
                    props.specialistDeathChanceDefeat = savedSpecialist;
                    props.governorDeathChanceDefeat = savedGovernor;
                }
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(a);
            }
        }

        /* DESTRUCTIVE: the governor death branch. With a certain (1.0) governor death chance and no
           specialists to reduce it, the governor must die, be killed, and be cleared from the static
           roster. */
        [EmpireDestructiveTest("SG.Destructive.Lifecycle")]
        public static void OnBattleResolved_GovernorDies_WhenChanceCertain()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC a = null;
            WorldObjectCompProperties_SettlementSpecialists props = null;
            float savedGovernor = 0f;
            WorldObjectComp_SettlementSpecialists comp = null;
            Pawn govPawn = null;
            try
            {
                a = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null) TestAssert.Skip("No valid tile");
                comp = a.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) TestAssert.Skip("No specialists comp");
                GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
                if (focus is null) TestAssert.Skip("No GovernorFocusDef loaded");
                govPawn = SpecDestructiveTestUtil.MakeAssignable(8);
                if (govPawn is null) TestAssert.Skip("No pawn");

                props = comp.Props;
                savedGovernor = props.governorDeathChanceDefeat;
                props.governorDeathChanceDefeat = 1f;

                comp.AssignGovernor(govPawn, focus);
                if (!comp.HasGovernor) TestAssert.Skip("Roster setup failed");

                MilitaryOperation op = new MilitaryOperation(-9995, MilitaryJobDefOf.DefendOwnSettlement, a.Tile, a);
                op.defender.faction = FindFC.EmpireFaction;
                BattleResult result = new BattleResult();

                new SpecialistLifecycleHandler().OnBattleResolved(op, false, result);

                TestAssert.IsFalse(comp.HasGovernor, "governor should die when its death chance is certain");
                TestAssert.IsFalse(SpecialistRoster.IsGovernor(govPawn), "dead governor should leave the static roster");
                TestAssert.IsTrue(govPawn.Dead, "governor pawn should be killed");
                DestructiveTestUtil.AssertEmpireInvariants(f, "OnBattleResolved_GovernorDies_WhenChanceCertain");
            }
            finally
            {
                if (props is object) props.governorDeathChanceDefeat = savedGovernor;
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(a);
            }
        }

        /* DESTRUCTIVE: a WON auto-battle must roll the VICTORY death chances, not the defeat ones.
           residentDeathChanceVictory = 1 with residentDeathChanceDefeat = 0 means the resident dies
           only if the victory chances are selected. */
        [EmpireDestructiveTest("SG.Destructive.Lifecycle")]
        public static void OnBattleResolved_VictoryUsesVictoryChances()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC a = null;
            WorldObjectCompProperties_SettlementSpecialists props = null;
            float savedResVictory = 0f, savedResDefeat = 0f;
            WorldObjectComp_SettlementSpecialists comp = null;
            try
            {
                a = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null) TestAssert.Skip("No valid tile");
                comp = a.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) TestAssert.Skip("No specialists comp");
                Pawn resPawn = SpecDestructiveTestUtil.MakeAssignable(8);
                if (resPawn is null) TestAssert.Skip("No pawn");

                props = comp.Props;
                savedResVictory = props.residentDeathChanceVictory;
                savedResDefeat = props.residentDeathChanceDefeat;
                props.residentDeathChanceVictory = 1f;
                props.residentDeathChanceDefeat = 0f;

                comp.AssignSpecialist(resPawn, null); // resident
                if (comp.ResidentCount != 1) TestAssert.Skip("Roster setup failed");

                MilitaryOperation op = new MilitaryOperation(-9994, MilitaryJobDefOf.DefendOwnSettlement, a.Tile, a);
                op.defender.faction = FindFC.EmpireFaction;
                BattleResult result = new BattleResult();

                new SpecialistLifecycleHandler().OnBattleResolved(op, true, result); // victory

                TestAssert.AreEqual(0, comp.ResidentCount, "victory roll should use residentDeathChanceVictory");
                DestructiveTestUtil.AssertEmpireInvariants(f, "OnBattleResolved_VictoryUsesVictoryChances");
            }
            finally
            {
                if (props is object)
                {
                    props.residentDeathChanceVictory = savedResVictory;
                    props.residentDeathChanceDefeat = savedResDefeat;
                }
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(a);
            }
        }

        /* DESTRUCTIVE: disband must clear EVERY member from the static roster, dead ones included.
           A member killed without going through NotifyMemberDied leaves a dead entry still assigned in
           the static SpecialistRoster; disband must recall it (no stale thingIDNumber) while returning
           only the living to the player. */
        [EmpireDestructiveTest("SG.Destructive.Lifecycle")]
        public static void DisbandRosterOnRemoval_DeadMember_ClearedFromStaticRoster()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC a = null;
            WorldObjectComp_SettlementSpecialists comp = null;
            Pawn deadPawn = null, livePawn = null;
            try
            {
                a = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null) TestAssert.Skip("No valid tile");
                comp = a.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) TestAssert.Skip("No specialists comp");
                if (comp.MaxSpecialists < 1) TestAssert.Skip("MaxSpecialists too low");
                SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
                if (role is null) TestAssert.Skip("No SpecialistRoleDef loaded");

                deadPawn = SpecDestructiveTestUtil.MakeAssignable(6);
                livePawn = SpecDestructiveTestUtil.MakeAssignable(6);
                if (deadPawn is null || livePawn is null) TestAssert.Skip("No pawn");

                comp.AssignSpecialist(deadPawn, role);
                comp.AssignSpecialist(livePawn, null); // resident survivor

                // Kill the specialist WITHOUT NotifyMemberDied: leaves a dead entry still in the roster
                // and still assigned in the static SpecialistRoster -- the leak the disband guards.
                deadPawn.Kill(null);
                if (!deadPawn.Dead) TestAssert.Skip("Could not kill test pawn");
                TestAssert.IsTrue(SpecialistRoster.IsAssigned(deadPawn), "precondition: dead pawn still assigned pre-disband");

                comp.DisbandRosterOnRemoval();

                TestAssert.IsFalse(SpecialistRoster.IsAssigned(deadPawn), "dead member should be cleared from the static roster");
                TestAssert.IsFalse(SpecialistRoster.IsAssigned(livePawn), "survivor should be cleared from the static roster");
                TestAssert.AreEqual(0, comp.TotalCount, "roster should be emptied");
                TestAssert.IsTrue(livePawn.Faction == Faction.OfPlayer, "survivor should be returned to the player");
                DestructiveTestUtil.AssertEmpireInvariants(f, "DisbandRosterOnRemoval_DeadMember_ClearedFromStaticRoster");
            }
            finally
            {
                // The disband forms a caravan of the survivor(s); destroy it, then clear any residue.
                if (a is object)
                {
                    List<Caravan> caravans = new List<Caravan>(Find.WorldObjects.Caravans);
                    foreach (Caravan c in caravans)
                        if (c is object && !c.Destroyed && c.Tile == a.Tile)
                            c.Destroy();
                }
                if (deadPawn is object) SpecialistRoster.Recall(deadPawn);
                if (livePawn is object) SpecialistRoster.Recall(livePawn);
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(a);
            }
        }
    }
}
