using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    /* Transport-pod arrival: add the pod's colonists as specialists, each auto-assigned the role
       it fits best (a combat pawn naturally lands the military "Commander" role). Pawns beyond the
       settlement's specialist capacity overflow to residents so nothing in the pod is lost. */
    public class WorldObjectComp_PawnArrival_Specialist : WorldObjectComp_SpecialistArrivalBase
    {
        public override FloatMenuAcceptanceReport CanReceive
        {
            get
            {
                WorldObjectComp_SettlementSpecialists roster = Roster;
                if (roster is null) return false;
                if (roster.SpecialistCount >= roster.MaxSpecialists)
                    return FloatMenuAcceptanceReport.WithFailReason("FCS_ArrivalSpecialistFull".Translate());
                return true;
            }
        }

        public override string ArrivalMenuLabel => "FCS_ArrivalSpecialist".Translate(Settlement.Label);

        public override void ReceivePawns(List<Pawn> pawns)
        {
            WorldObjectComp_SettlementSpecialists roster = Roster;
            if (roster is null) return;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (roster.SpecialistCount < roster.MaxSpecialists)
                    roster.AssignSpecialist(pawn, SpecUtil.BestSpecialistRole(pawn, roster));
                else
                    roster.AssignSpecialist(pawn, null); // capacity reached -> resident overflow
            }
        }
    }
}
