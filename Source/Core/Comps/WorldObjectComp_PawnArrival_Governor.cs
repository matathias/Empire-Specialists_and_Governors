namespace FactionColonies.Specialists;

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
            if (roster.Governor is not null)
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
            if (roster is not null && roster.Governor is not null && roster.Governor.pawn == pawns[i])
                governor = pawns[i];
            else
                residents++;
        }

        if (governor is not null && residents == 0)
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
            // Governor slot filled, or the governor assignment failed -> resident, so the pawn (already
            // out of the pod) is never left unplaced.
            if (roster.Governor is not null || !roster.AssignGovernor(pawn, SpecUtil.BestGovernorFocus(pawn)))
                roster.AssignSpecialist(pawn, null);
        }
    }
}
