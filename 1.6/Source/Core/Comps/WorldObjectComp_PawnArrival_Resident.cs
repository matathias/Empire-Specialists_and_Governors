using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    /* Transport-pod arrival: add the pod's colonists as residents (worker bonus, no role).
       Residents are uncapped. */
    public class WorldObjectComp_PawnArrival_Resident : WorldObjectComp_SpecialistArrivalBase
    {
        public override FloatMenuAcceptanceReport CanReceive => Roster is object;

        public override string ArrivalMenuLabel => "FCS_ArrivalResident".Translate(Settlement.Label);

        public override string ArrivalMessage(List<Pawn> pawns)
            => "FCS_ArrivalResidentDone".Translate(pawns.Count, Settlement.Label);

        public override void ReceivePawns(List<Pawn> pawns)
        {
            WorldObjectComp_SettlementSpecialists roster = Roster;
            if (roster is null) return;
            for (int i = 0; i < pawns.Count; i++)
                roster.AssignSpecialist(pawns[i], null);
        }
    }
}
