namespace FactionColonies.Specialists
{
    /// <summary>
    /// Defense role behavior: death-chance reduction math. No game required. RoleBehavior_Defense
    /// reads its config from the <c>extension</c> field, so it can be exercised in isolation (the
    /// settlement arg is unused by the Defense override).
    /// </summary>
    public static class RoleBehaviorTests
    {
        private static RoleBehavior_Defense Behavior(float perPoint, float floor)
        {
            return new RoleBehavior_Defense
            {
                extension = new RoleBehaviorExt_Defense
                {
                    baseReductionPerSkillPoint = perPoint,
                    minimumDeathChance = floor
                }
            };
        }

        [EmpireTest("SG.Behavior")]
        public static void ModifyDeathChances_ReducesAllBySkillTimesRate()
        {
            RoleBehavior_Defense b = Behavior(0.02f, 0f);
            float gov = 0.10f, spec = 0.20f, res = 0.30f;
            // reduction = 5 * 0.02 = 0.10
            b.ModifyDeathChances(null, 5f, ref gov, ref spec, ref res);
            TestAssert.AreEqual(0.0, gov);
            TestAssert.AreEqual(0.10, spec);
            TestAssert.AreEqual(0.20, res);
        }

        [EmpireTest("SG.Behavior")]
        public static void ModifyDeathChances_ClampsAtMinimum()
        {
            RoleBehavior_Defense b = Behavior(0.02f, 0.05f);
            float gov = 0.10f, spec = 0.20f, res = 0.30f;
            // huge reduction -> all clamp to the 0.05 floor
            b.ModifyDeathChances(null, 100f, ref gov, ref spec, ref res);
            TestAssert.AreEqual(0.05, gov);
            TestAssert.AreEqual(0.05, spec);
            TestAssert.AreEqual(0.05, res);
        }

        [EmpireTest("SG.Behavior")]
        public static void ModifyDeathChances_ZeroSkill_NoChange()
        {
            RoleBehavior_Defense b = Behavior(0.02f, 0f);
            float gov = 0.10f, spec = 0.20f, res = 0.30f;
            b.ModifyDeathChances(null, 0f, ref gov, ref spec, ref res);
            TestAssert.AreEqual(0.10, gov);
            TestAssert.AreEqual(0.20, spec);
            TestAssert.AreEqual(0.30, res);
        }
    }
}
