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

            int defenseCount = comp.DefenseSpecialists.Count();
            List<SpecialistFC> toKill = new List<SpecialistFC>();

            foreach (SpecialistFC s in comp.AllPawnsSnapshot())
            {
                if (s.pawn == null || s.pawn.Dead) continue;
                float deathChance = GetDeathChance(s.role, defenseCount);
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
                    ? "Governor killed" : "Specialist killed";
                string text = pawn.LabelShort + " (" + role + ") was killed in the attack on "
                    + settlement.Name + ".";
                Find.LetterStack.ReceiveLetter(label, text, letterDef);
            }
        }

        private float GetDeathChance(SpecialistRole role, int defenseCount)
        {
            float reduction = defenseCount * FCSSettings.deathReductionPerDefender;
            switch (role)
            {
                case SpecialistRole.Specialist:
                    return Math.Max(0f, FCSSettings.civilianDeathChance - reduction);
                case SpecialistRole.Governor:
                    return Math.Max(0f, FCSSettings.governorDeathChance - reduction);
                case SpecialistRole.Defense:
                    return Math.Max(0f, FCSSettings.defenseDeathChance);
                case SpecialistRole.Resident:
                    return Math.Max(0f, FCSSettings.residentDeathChance - reduction);
                default:
                    return 0f;
            }
        }
    }
}
