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
            WorldSettlementFC settlement = op.aggressor?.homeSettlement ?? op.defender?.homeSettlement;
            if (settlement is null) return;

            WorldObjectComp_SettlementSpecialists comp =
                settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null || comp.TotalCount == 0) return;
            if (comp.PawnsDeployedToBattle) return;

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

            // Process specialist deaths
            foreach (SettlementSpecialist s in specToKill)
            {
                Pawn pawn = s.pawn;
                string roleLabel = s.role?.LabelCap ?? "Specialist";
                comp.RemoveSpecialist(s);
                pawn.Kill(null);
                SpecUtil.SendDeathLetter(roleLabel,
                    "FCS_LetterDeathAttack".Translate(pawn.LabelShort, roleLabel, settlement.Name));
            }

            // Process resident deaths
            foreach (SettlementSpecialist r in residentsToKill)
            {
                Pawn pawn = r.pawn;
                comp.RemoveSpecialist(r);
                pawn.Kill(null);
                SpecUtil.SendDeathLetter("FCS_RoleResident".Translate(),
                    "FCS_LetterDeathAttack".Translate(pawn.LabelShort, "FCS_RoleResident".Translate(), settlement.Name));
            }

            // Process governor death
            if (governorDied)
            {
                Pawn pawn = comp.Governor.pawn;
                comp.RecallGovernor();
                pawn.Kill(null);
                SpecUtil.SendDeathLetter("FCS_RoleGovernor".Translate(),
                    "FCS_LetterDeathAttack".Translate(pawn.LabelShort, "FCS_RoleGovernor".Translate(), settlement.Name));
            }
        }
    }
}
