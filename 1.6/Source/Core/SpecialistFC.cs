using Verse;

namespace FactionColonies.Specialists
{
    public class SpecialistFC : IExposable
    {
        public Pawn pawn;
        public SpecialistRole role;
        public string governorFocusDefName;
        public int assignedTick;

        public SpecialistFC()
        {
        }

        public SpecialistFC(Pawn pawn, SpecialistRole role)
        {
            this.pawn = pawn;
            this.role = role;
            this.assignedTick = Find.TickManager.TicksGame;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref role, "role", SpecialistRole.Resident);
            Scribe_Values.Look(ref governorFocusDefName, "governorFocusDefName");
            Scribe_Values.Look(ref assignedTick, "assignedTick");
        }
    }
}
