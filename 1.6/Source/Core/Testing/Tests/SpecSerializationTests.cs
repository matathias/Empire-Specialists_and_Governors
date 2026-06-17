using RimWorld;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Value-only serialization semantics. No game required. A full isolated Scribe round-trip is
    /// infeasible (pawn Scribe_References needs a live save cycle), so we verify the value/def fields
    /// that survive a load and the satisfaction clamp invariant that PostExposeData restores. The
    /// live save/reload cycle is covered (best-effort) by SpecSerializationDestructiveTests.
    /// </summary>
    public static class SpecSerializationTests
    {
        [EmpireTest("SG.Serialization")]
        public static void Specialist_ValueFields_HoldExactly()
        {
            SpecialistRoleDef role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f);
            SettlementSpecialist s = new SettlementSpecialist { assignedTick = 12345, role = role };
            // These are the fields PostExposeData persists via Scribe_Values / Scribe_Defs.
            TestAssert.AreEqual(12345, s.assignedTick);
            TestAssert.IsTrue(s.role == role, "role reference should be retained");
            TestAssert.IsTrue(s.IsResident == false, "a role-bearing entry is not a resident");
        }

        [EmpireTest("SG.Serialization")]
        public static void Resident_NullRole_IsResident()
        {
            SettlementSpecialist r = new SettlementSpecialist { assignedTick = 7, role = null };
            TestAssert.IsTrue(r.IsResident, "null role means resident");
        }

        [EmpireTest("SG.Serialization")]
        public static void Comp_FoodSatisfaction_Clamps01()
        {
            WorldObjectComp_SettlementSpecialists c = new WorldObjectComp_SettlementSpecialists();
            c.FoodSatisfaction = 2f;
            TestAssert.AreEqual(1.0, c.FoodSatisfaction);
            c.FoodSatisfaction = -1f;
            TestAssert.AreEqual(0.0, c.FoodSatisfaction);
            c.FoodSatisfaction = 0.5f;
            TestAssert.AreEqual(0.5, c.FoodSatisfaction);
        }

        [EmpireTest("SG.Serialization")]
        public static void Comp_MedicineSatisfaction_Clamps01()
        {
            WorldObjectComp_SettlementSpecialists c = new WorldObjectComp_SettlementSpecialists();
            c.MedicineSatisfaction = 5f;
            TestAssert.AreEqual(1.0, c.MedicineSatisfaction);
            c.MedicineSatisfaction = -0.5f;
            TestAssert.AreEqual(0.0, c.MedicineSatisfaction);
        }

        [EmpireTest("SG.Serialization")]
        public static void Comp_FreshInstance_EmptyRoster()
        {
            WorldObjectComp_SettlementSpecialists c = new WorldObjectComp_SettlementSpecialists();
            TestAssert.AreEqual(0, c.SpecialistCount);
            TestAssert.AreEqual(0, c.ResidentCount);
            TestAssert.IsFalse(c.HasGovernor, "fresh comp has no governor");
        }
    }
}
