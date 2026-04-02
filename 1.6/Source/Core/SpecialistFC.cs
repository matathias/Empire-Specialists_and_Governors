using System.Collections.Generic;
using Verse;

namespace FactionColonies.Specialists
{
    public class SpecialistFC : IExposable
    {
        public Pawn pawn;
        public SpecialistRole role;
        public List<ResourceTypeDef> governorFocuses = new List<ResourceTypeDef>();
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

        public bool HasFocus(ResourceTypeDef def)
        {
            return governorFocuses != null && governorFocuses.Contains(def);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref role, "role", SpecialistRole.Resident);
            Scribe_Collections.Look(ref governorFocuses, "governorFocuses", LookMode.Def);
            Scribe_Values.Look(ref assignedTick, "assignedTick");
            if (governorFocuses == null)
            {
                governorFocuses = new List<ResourceTypeDef>();
            }
        }
    }
}
