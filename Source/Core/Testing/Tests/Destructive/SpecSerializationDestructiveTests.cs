using Verse;

namespace FactionColonies.Specialists
{
    /* DESTRUCTIVE: roster reconstruction consistency (the load-time rebuild path).
       SpecialistRoster.Rebuild() reconstructs the global roster purely from each settlement's comp --
       the same shape PostLoadInit leaves behind. Asserting it round-trips the comp's roster validates
       that the persisted state is internally consistent without driving a full Scribe save/load cycle.

       GAP: a true save-to-disk + reload cycle is intentionally not run here (it writes files and has a
       high crash surface against this lightweight runner). If a Scribe round-trip helper is ever added
       to the base DestructiveTestUtil, add a real save/reload assertion here. */
    public static class SpecSerializationDestructiveTests
    {
        [EmpireDestructiveTest("SG.Destructive.Serialization")]
        public static void Roster_SurvivesRebuild()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();
            WorldSettlementFC s = DestructiveTestUtil.CreateTransientSettlement();
            if (s is null) TestAssert.Skip("No valid tile");
            WorldObjectComp_SettlementSpecialists comp = s.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) TestAssert.Skip("No specialists comp");
            SpecialistRoleDef role = SpecDestructiveTestUtil.AnyRole();
            GovernorFocusDef focus = SpecDestructiveTestUtil.AnyFocus();
            if (role is null || focus is null) TestAssert.Skip("Missing role/focus defs");
            Pawn specPawn = SpecDestructiveTestUtil.MakeAssignable(7);
            Pawn govPawn = SpecDestructiveTestUtil.MakeAssignable(7);
            if (specPawn is null || govPawn is null) TestAssert.Skip("No pawn");
            try
            {
                comp.AssignSpecialist(specPawn, role);
                comp.AssignGovernor(govPawn, focus);

                // Reconstruct the global roster from comps (the load-time path).
                SpecialistRoster.Rebuild();

                TestAssert.IsTrue(SpecialistRoster.GetRole(specPawn) == role,
                    "rebuilt roster should map the specialist pawn to its role");
                TestAssert.IsTrue(SpecialistRoster.IsGovernor(govPawn),
                    "rebuilt roster should mark the governor pawn as governor");
                TestAssert.IsFalse(SpecialistRoster.IsGovernor(specPawn),
                    "specialist pawn should not be flagged as governor");
                DestructiveTestUtil.AssertEmpireInvariants(f, "Roster_SurvivesRebuild");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(s);
                // Roster was globally rebuilt above; rebuild once more so it reflects the teardown.
                TestAssert.DoesNotThrow(() => SpecialistRoster.Rebuild(), "post-teardown Rebuild threw");
            }
        }
    }
}
