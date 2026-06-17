using System.Collections.Generic;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    /* Transport-pod arrival: install the pod's colonist as governor, with an auto-picked focus that
       fits the pawn best. A settlement has one governor, so if the pod carries several pawns the
       first becomes governor and the rest are added as residents (nothing in the pod is lost). */
    public class WorldObjectComp_PawnArrival_Governor : WorldObjectComp_SpecialistArrivalBase
    {
        public override FloatMenuAcceptanceReport CanReceive
        {
            get
            {
                WorldObjectComp_SettlementSpecialists roster = Roster;
                if (roster is null) return false;
                if (roster.Governor is object)
                    return FloatMenuAcceptanceReport.WithFailReason("FCS_ArrivalGovernorTaken".Translate());
                return true;
            }
        }

        public override string ArrivalMenuLabel => "FCS_ArrivalGovernor".Translate(Settlement.Label);

        public override string ArrivalMessage(List<Pawn> pawns)
            => "FCS_ArrivalGovernorDone".Translate(pawns[0].LabelShortCap, Settlement.Label);

        public override void ReceivePawns(List<Pawn> pawns)
        {
            WorldObjectComp_SettlementSpecialists roster = Roster;
            if (roster is null) return;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (roster.Governor is null)
                    roster.AssignGovernor(pawn, SpecUtil.BestGovernorFocus(pawn));
                else
                    roster.AssignSpecialist(pawn, null); // governor slot filled -> resident
            }
        }
    }
}
