using System.Collections.Generic;
using Verse;

namespace FactionColonies.Specialists
{
    public class SpecialistFC : IExposable
    {
        public Pawn pawn;
        public SpecialistRole role;
        public List<string> governorFocuses = new List<string>();
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

        public bool HasFocus(string resourceDefName)
        {
            return governorFocuses != null && governorFocuses.Contains(resourceDefName);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref role, "role", SpecialistRole.Resident);
            Scribe_Collections.Look(ref governorFocuses, "governorFocuses", LookMode.Value);
            Scribe_Values.Look(ref assignedTick, "assignedTick");
            if (governorFocuses == null)
            {
                governorFocuses = new List<string>();
            }
        }
    }
}
