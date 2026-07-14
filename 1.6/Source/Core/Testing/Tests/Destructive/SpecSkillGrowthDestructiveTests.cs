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

        /* DESTRUCTIVE: a governor always gains Social XP on the daily tick, even when its focus does
           not weight Social. Uses a synthetic focus weighted only on Plants and a large xpPerDay so the
           Social level provably jumps. */
        [EmpireDestructiveTest("SG.Destructive.SkillGrowth")]
        public static void DailySkillTick_GovernorAlwaysGainsSocial()
        {
            FactionFC f = DestructiveTestUtil.RequireFaction();

            WorldSettlementFC a = null;
            WorldObjectComp_SettlementSpecialists comp = null;
            float savedXp = FCSSettings.xpPerDay;
            try
            {
                a = DestructiveTestUtil.CreateTransientSettlement();
                if (a is null) TestAssert.Skip("No valid tile");
                comp = a.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) TestAssert.Skip("No specialists comp");

                // Focus weighted only on Plants -- no Social weight -- so a Social gain can only come
                // from the "governor always gets Social XP" clause.
                GovernorFocusDef focus = SpecTestHelper.Focus(SkillDefOf.Plants, 1f, null, 0f);
                Pawn govPawn = SpecDestructiveTestUtil.MakeAssignable(6);
                if (govPawn is null) TestAssert.Skip("No pawn");

                comp.AssignGovernor(govPawn, focus);
                if (!comp.HasGovernor) TestAssert.Skip("Roster setup failed");

                SkillRecord social = govPawn.skills.GetSkill(SkillDefOf.Social);
                if (social is null || social.TotallyDisabled) TestAssert.Skip("Social skill unavailable on test pawn");
                int before = social.Level;

                FCSSettings.xpPerDay = 1_000_000f; // guarantees a level jump
                comp.DoDailySkillTick();

                TestAssert.IsTrue(social.Level > before, "governor should gain Social XP even without a Social focus weight");
                DestructiveTestUtil.AssertEmpireInvariants(f, "DailySkillTick_GovernorAlwaysGainsSocial");
            }
            finally
            {
                FCSSettings.xpPerDay = savedXp;
                SpecDestructiveTestUtil.CleanupRoster(comp);
                DestructiveTestUtil.SafeRemoveSettlement(a);
            }
        }
    }
}
