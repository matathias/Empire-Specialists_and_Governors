using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Specialists &amp; Governors helpers for the destructive test tier. Builds on the base mod's
    /// <see cref="DestructiveTestUtil"/> (transient-settlement create/teardown + invariant battery)
    /// and adds the fixtures the roster tests share: assignable pawns, shipped def lookups, and a
    /// best-effort roster teardown.
    /// </summary>
    public static class SpecDestructiveTestUtil
    {
        // An assignable colonist (skills set), or null if no game (caller skips).
        public static Pawn MakeAssignable(int level)
        {
            return SpecTestHelper.TryMakeControlledPawn(level);
        }

        public static SpecialistRoleDef AnyRole()
        {
            return DefDatabase<SpecialistRoleDef>.AllDefsListForReading.FirstOrDefault();
        }

        public static GovernorFocusDef AnyFocus()
        {
            return DefDatabase<GovernorFocusDef>.AllDefsListForReading.FirstOrDefault();
        }

        public static List<GovernorFocusDef> AllFocuses()
        {
            return DefDatabase<GovernorFocusDef>.AllDefsListForReading;
        }

        // Clears the global SpecialistRoster of every pawn this comp tracks. RemoveSpecialist already
        // recalls specialists/residents from the roster (no caravan); the governor has no caravan-free
        // removal, so we recall its pawn directly. The comp itself is discarded when the transient
        // settlement is torn down.
        public static void CleanupRoster(WorldObjectComp_SettlementSpecialists comp)
        {
            if (comp is null) return;
            foreach (SettlementSpecialist s in comp.Specialists.ToList())
                comp.RemoveSpecialist(s);
            foreach (SettlementSpecialist r in comp.Residents.ToList())
                comp.RemoveSpecialist(r);
            if (comp.HasGovernor && comp.Governor?.pawn is object)
                SpecialistRoster.Recall(comp.Governor.pawn);
        }
    }
}
