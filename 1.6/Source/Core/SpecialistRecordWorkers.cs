using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public abstract class RecordWorker_SpecialistBase : RecordWorker
    {
        protected static SpecialistRole? GetRole(Pawn pawn)
        {
            FactionFC fc = FactionCache.FactionComp;
            if (fc is null) return null;
            List<WorldSettlementFC> settlements = fc.settlements;
            for (int i = 0; i < settlements.Count; i++)
            {
                WorldObjectComp_SettlementSpecialists comp =
                    settlements[i].GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) continue;
                List<SpecialistFC> pawns = comp.AllPawnsInternal;
                for (int j = 0; j < pawns.Count; j++)
                {
                    if (pawns[j].pawn == pawn)
                        return pawns[j].role;
                }
            }
            return null;
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
