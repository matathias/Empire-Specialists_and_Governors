using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public static class SpecialistRoster
    {
        private static Dictionary<int, SpecialistRole> assignments = new Dictionary<int, SpecialistRole>();

        public static void Assign(Pawn pawn, SpecialistRole role)
        {
            if (pawn != null)
                assignments[pawn.thingIDNumber] = role;
        }

        public static void Recall(Pawn pawn)
        {
            if (pawn != null)
                assignments.Remove(pawn.thingIDNumber);
        }

        public static SpecialistRole? GetRole(Pawn pawn)
        {
            if (pawn != null && assignments.TryGetValue(pawn.thingIDNumber, out SpecialistRole role))
                return role;
            return null;
        }

        public static bool IsAssigned(Pawn pawn)
        {
            return pawn != null && assignments.ContainsKey(pawn.thingIDNumber);
        }

        /// <summary>
        /// Clears and rebuilds the dictionary from all settlement specialist comps.
        /// Called from GameComponent_SpecialistRoster.FinalizeInit after all cross-refs are resolved.
        /// </summary>
        public static void Rebuild()
        {
            assignments.Clear();
            List<WorldSettlementFC> settlements = FactionCache.FactionComp?.settlements;
            if (settlements is null) return;
            foreach (WorldSettlementFC settlement in settlements)
            {
                List<SpecialistFC> pawns = settlement.GetComponent<WorldObjectComp_SettlementSpecialists>()?.AllPawnsInternal;
                if (pawns is null) continue;
                foreach (SpecialistFC s in pawns)
                {
                    if (s.IsAlive)
                        assignments[s.pawn.thingIDNumber] = s.role;
                }
            }
        }
    }

    public abstract class RecordWorker_SpecialistBase : RecordWorker
    {
        protected static SpecialistRole? GetRole(Pawn pawn)
        {
            return SpecialistRoster.GetRole(pawn);
        }
    }

    public class RecordWorker_FCS_TimeAssigned : RecordWorker_SpecialistBase
    {
        public override bool ShouldMeasureTimeNow(Pawn pawn)
        {
            return GetRole(pawn).HasValue;
        }
    }

    public class RecordWorker_FCS_TimeAsResident : RecordWorker_SpecialistBase
    {
        public override bool ShouldMeasureTimeNow(Pawn pawn)
        {
            return GetRole(pawn) == SpecialistRole.Resident;
        }
    }

    public class RecordWorker_FCS_TimeAsSpecialist : RecordWorker_SpecialistBase
    {
        public override bool ShouldMeasureTimeNow(Pawn pawn)
        {
            return GetRole(pawn) == SpecialistRole.Specialist;
        }
    }

    public class RecordWorker_FCS_TimeAsDefense : RecordWorker_SpecialistBase
    {
        public override bool ShouldMeasureTimeNow(Pawn pawn)
        {
            return GetRole(pawn) == SpecialistRole.Defense;
        }
    }

    public class RecordWorker_FCS_TimeAsGovernor : RecordWorker_SpecialistBase
    {
        public override bool ShouldMeasureTimeNow(Pawn pawn)
        {
            return GetRole(pawn) == SpecialistRole.Governor;
        }
    }
}
