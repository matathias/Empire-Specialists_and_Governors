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

        /* ReceivePawns has already run, so re-derive each pawn's actual outcome from the roster:
           the one matching the live governor was installed, the rest overflowed to residents. */
        public override string ArrivalMessage(List<Pawn> pawns)
        {
            WorldObjectComp_SettlementSpecialists roster = Roster;
            Pawn governor = null;
            int residents = 0;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (roster is object && roster.Governor is object && roster.Governor.pawn == pawns[i])
                    governor = pawns[i];
                else
                    residents++;
            }

            if (governor is object && residents == 0)
                return "FCS_ArrivalGovernorDone".Translate(governor.LabelShortCap, Settlement.Label);
            if (governor is null)
                return "FCS_ArrivalResidentDone".Translate(residents, Settlement.Label);
            return "FCS_ArrivalGovernorAndResidents".Translate(governor.LabelShortCap, Settlement.Label, residents);
        }

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
