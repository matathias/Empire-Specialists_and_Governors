using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    /* DESTRUCTIVE regression guard for H4: the daily skill tick raises levels but must also dirty the
       cached SkillScore so the grown levels feed production/stat/upkeep bonuses in the same session.
       Before the fix, DoDailySkillTick never invalidated the cache and the score stayed frozen at the
       assignment-time value until the next reload. */
    public static class SpecSkillGrowthDestructiveTests
    {
        [EmpireDestructiveTest("SG.Destructive.SkillGrowth")]
        public static void DailySkillTick_DirtiesSkillScoreCache()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC a = null;
            WorldObjectComp_SettlementSpecialists comp = null;
            try
            {
                a = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null) TestAssert.Skip("No valid tile");
                comp = a.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) TestAssert.Skip("No specialists comp");
                if (comp.MaxSpecialists < 1) TestAssert.Skip("MaxSpecialists too low");

                // Role weighted purely on Plants, and a pawn whose skills are force-enabled at level 1.
                SpecialistRoleDef role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f);
                Pawn pawn = SpecDestructiveTestUtil.MakeAssignable(1);
                if (pawn is null) TestAssert.Skip("No pawn");
                comp.AssignSpecialist(pawn, role);
                if (comp.SpecialistCount != 1) TestAssert.Skip("Roster setup failed");

                SettlementSpecialist s = comp.Specialists[0];
                float before = s.SkillScore; // caches at level 1

                // Force a level jump on the weighted skill; the cache must not reflect it yet.
                pawn.skills.GetSkill(SkillDefOf.Plants).Level = 18;
                TestAssert.IsTrue(s.SkillScore == before, "SkillScore should be cached (stale) before the daily tick");

                comp.DoDailySkillTick();

                // The daily tick dirtied the cache, so the score now reflects the grown level.
                TestAssert.IsTrue(s.SkillScore > before, "daily tick should dirty the SkillScore cache so it reflects the new level");

                DestructiveTestUtil.AssertEmpireInvariants(f, "DailySkillTick_DirtiesSkillScoreCache");
            }
            finally
            {
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(a);
            }
        }
    }
}
