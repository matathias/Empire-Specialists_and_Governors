using Verse;

namespace FactionColonies.Specialists
{
    /* Shared base for the submod's transport-pod arrival comps (resident / specialist / governor).
       Subclasses the base mod's WorldObjectComp_SettlementPawnArrival seam: WorldSettlementFC
       auto-discovers each concrete subclass and offers a float-menu option when a launched pod
       carries an assignable colonist. Holds the common roster lookup and eligibility; concrete
       subclasses supply CanReceive, ArrivalMenuLabel, and ReceivePawns. */
    public abstract class WorldObjectComp_SpecialistArrivalBase : WorldObjectComp_SettlementPawnArrival
    {
        protected WorldObjectComp_SettlementSpecialists Roster =>
            Settlement?.GetComponent<WorldObjectComp_SettlementSpecialists>();

        public override bool AcceptsPawn(Pawn pawn) => SpecUtil.IsAssignablePawn(pawn);
    }
}
