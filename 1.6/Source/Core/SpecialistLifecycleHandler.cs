using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public class SpecialistLifecycleHandler : LifecycleParticipantBase
    {
        public override void OnBattleResolved(WorldSettlementFC settlement,
            MilitaryJobDef job, bool victory, BattleResult result)
        {
            if (victory) return;

            WorldObjectComp_SettlementSpecialists comp =
                settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp == null || comp.TotalCount == 0) return;

            // Skip if specialists were deployed to a manual battle — deaths handled by RecoverFromBattle
            if (comp.PawnsDeployedToBattle) return;

            int defenseCount = comp.DefenseSpecialists.Count();
            FCPolicyDef garrisonDef = SpecialistsCache.TraitDef("garrisonDoctrine");
            FCPolicyDef profArmyDef = SpecialistsCache.TraitDef("professionalArmy");
            bool garrison = garrisonDef != null && FactionCache.FactionComp.HasTrait(garrisonDef);
            bool profArmy = profArmyDef != null && FactionCache.FactionComp.HasTrait(profArmyDef);
            List<SpecialistFC> toKill = new List<SpecialistFC>();

            foreach (SpecialistFC s in comp.AllPawnsSnapshot())
            {
                if (s.pawn == null || s.pawn.Dead) continue;
                float deathChance = GetDeathChance(s.role, defenseCount, garrison, profArmy);
                if (Rand.Chance(deathChance))
                {
                    toKill.Add(s);
                }
            }

            foreach (SpecialistFC s in toKill)
            {
                Pawn pawn = s.pawn;
                SpecialistRole role = s.role;
                comp.RemoveSpecialist(s);
                pawn.Kill(null);

                LetterDef letterDef = role == SpecialistRole.Governor
                    ? LetterDefOf.Death : LetterDefOf.NegativeEvent;
                string label = role == SpecialistRole.Governor
                    ? "FCS_LetterGovernorKilled".Translate() : "FCS_LetterSpecialistKilled".Translate();
                string text = "FCS_LetterDeathAttack".Translate(pawn.LabelShort, role.Translate(), settlement.Name);
                Find.LetterStack.ReceiveLetter(label, text, letterDef);
            }
        }

        private float GetDeathChance(SpecialistRole role, int defenseCount, bool garrison, bool profArmy)
        {
            float reductionPerDefender = FCSSettings.deathReductionPerDefender;
            if (profArmy) reductionPerDefender *= 2f;
            float reduction = defenseCount * reductionPerDefender;

            switch (role)
            {
                case SpecialistRole.Specialist:
                    return Math.Max(0f, FCSSettings.civilianDeathChance - reduction);
                case SpecialistRole.Governor:
                    return Math.Max(0f, FCSSettings.governorDeathChance - reduction);
                case SpecialistRole.Defense:
                    float defChance = FCSSettings.defenseDeathChance;
                    if (garrison) defChance *= 0.5f;
                    float defReduction = Math.Max(0, defenseCount - 1) * reductionPerDefender;
                    return Math.Max(0f, defChance - defReduction);
                case SpecialistRole.Resident:
                    return Math.Max(0f, FCSSettings.residentDeathChance - reduction);
                default:
                    return 0f;
            }
        }
    }
}
