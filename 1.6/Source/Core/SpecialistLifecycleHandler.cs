using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies.Specialists
{
    public class SpecialistLifecycleHandler : IMilitaryOperationListener
    {
        public void OnOperationCreated(MilitaryOperation op) { }
        public void OnOperationResolved(MilitaryOperation op) { }

        public void OnBattleResolved(MilitaryOperation op, bool victory, BattleResult result)
        {
            if (op is null) return;
            // Defense-only: the abstract roll only ever touches the settlement being defended. In an
            // offensive op the player's settlement is the aggressor, so this resolves to the enemy
            // (no specialists comp -> early return). Specialists play no part in offense.
            WorldSettlementFC settlement = op.defender?.homeSettlement;
            if (settlement is null) return;

            WorldObjectComp_SettlementSpecialists comp =
                settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null || comp.TotalCount == 0) return;
            if (comp.PawnsDeployedToBattle) return;
            if (result is object && result.wasManualBattle) return; // only auto-resolved battles roll

            WorldObjectCompProperties_SettlementSpecialists props = comp.Props;

            float govChance = victory ? props.governorDeathChanceVictory : props.governorDeathChanceDefeat;
            float specChance = victory ? props.specialistDeathChanceVictory : props.specialistDeathChanceDefeat;
            float residentChance = victory ? props.residentDeathChanceVictory : props.residentDeathChanceDefeat;

            // Let Defense specialist behaviors modify chances
            foreach (SettlementSpecialist s in comp.Specialists)
            {
                if (s.role is null || !s.HasUsableSkills) continue;
                SpecialistRoleBehavior roleBehavior = s.Behavior;
                if (roleBehavior is null) continue;
                roleBehavior.ModifyDeathChances(settlement, s.SkillScore,
                    ref govChance, ref specChance, ref residentChance);
            }

            // Snapshot lists before mutation
            List<SettlementSpecialist> specToKill = new List<SettlementSpecialist>();
            foreach (SettlementSpecialist s in comp.Specialists)
            {
                if (!s.IsAlive) continue;
                if (Rand.Chance(specChance))
                    specToKill.Add(s);
            }

            List<SettlementSpecialist> residentsToKill = new List<SettlementSpecialist>();
            foreach (SettlementSpecialist r in comp.Residents)
            {
                if (!r.IsAlive) continue;
                if (Rand.Chance(residentChance))
                    residentsToKill.Add(r);
            }

            bool governorDied = comp.HasGovernor && Rand.Chance(govChance);

            // Process deaths. NotifyMemberDied removes the roster entry and sends the letter before
            // Kill, so the catch-all Pawn.Kill patch no-ops (the pawn is no longer assigned).
            foreach (SettlementSpecialist s in specToKill)
            {
                Pawn pawn = s.pawn;
                comp.NotifyMemberDied(pawn, SpecDeathCause.AutoBattle);
                pawn.Kill(null);
            }

            foreach (SettlementSpecialist r in residentsToKill)
            {
                Pawn pawn = r.pawn;
                comp.NotifyMemberDied(pawn, SpecDeathCause.AutoBattle);
                pawn.Kill(null);
            }

            if (governorDied)
            {
                Pawn pawn = comp.Governor.pawn;
                comp.NotifyMemberDied(pawn, SpecDeathCause.AutoBattle);
                pawn.Kill(null);
            }
        }
    }
}
