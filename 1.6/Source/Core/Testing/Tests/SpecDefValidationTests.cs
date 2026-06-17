using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Def-shape validation for <see cref="SpecialistRoleDef"/> / <see cref="GovernorFocusDef"/>.
    /// No game required. Asserts on the submod-specific ConfigError substrings only (base Def may
    /// add generic errors for a def that isn't registered in the DefDatabase).
    /// </summary>
    public static class SpecDefValidationTests
    {
        private static List<string> Errors(SpecialistRoleDef def) => def.ConfigErrors().ToList();
        private static List<string> Errors(GovernorFocusDef def) => def.ConfigErrors().ToList();

        [EmpireTest("SG.Defs")]
        public static void RoleDef_NullSkill_YieldsError()
        {
            SpecialistRoleDef role = new SpecialistRoleDef
            {
                defName = "TEST_NullSkill",
                label = "test",
                skillWeights = new List<SkillWeight> { new SkillWeight { skill = null } }
            };
            List<string> errs = Errors(role);
            TestAssert.IsTrue(errs.Any(e => e.Contains("skillWeights[0] has null skill")),
                "Expected a null-skill ConfigError");
        }

        [EmpireTest("SG.Defs")]
        public static void RoleDef_NullResource_YieldsError()
        {
            SpecialistRoleDef role = new SpecialistRoleDef
            {
                defName = "TEST_NullRes",
                label = "test",
                resourceBonuses = new List<ResourceProductionBonus>
                {
                    new ResourceProductionBonus { resource = null }
                }
            };
            List<string> errs = Errors(role);
            TestAssert.IsTrue(errs.Any(e => e.Contains("resourceBonuses[0] has null resource")),
                "Expected a null-resource ConfigError");
        }

        [EmpireTest("SG.Defs")]
        public static void RoleDef_ValidShape_NoSubmodErrors()
        {
            ResourceTypeDef anyRes = DefDatabase<ResourceTypeDef>.AllDefsListForReading.FirstOrDefault();
            if (anyRes is null) TestAssert.Skip("No ResourceTypeDef loaded");

            SpecialistRoleDef role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, anyRes, 0.05f);
            List<string> errs = Errors(role);
            TestAssert.IsFalse(errs.Any(e => e.Contains("null skill") || e.Contains("null resource")),
                "Valid role should not yield null-skill / null-resource errors");
        }

        [EmpireTest("SG.Defs")]
        public static void FocusDef_NullSkill_YieldsError()
        {
            GovernorFocusDef focus = new GovernorFocusDef
            {
                defName = "TEST_FocusNullSkill",
                label = "test",
                skillWeights = new List<SkillWeight> { new SkillWeight { skill = null } }
            };
            List<string> errs = Errors(focus);
            TestAssert.IsTrue(errs.Any(e => e.Contains("skillWeights[0] has null skill")),
                "Expected a null-skill ConfigError");
        }

        [EmpireTest("SG.Defs")]
        public static void FocusDef_NullResource_YieldsError()
        {
            GovernorFocusDef focus = new GovernorFocusDef
            {
                defName = "TEST_FocusNullRes",
                label = "test",
                resourceBonuses = new List<ResourceProductionBonus>
                {
                    new ResourceProductionBonus { resource = null }
                }
            };
            List<string> errs = Errors(focus);
            TestAssert.IsTrue(errs.Any(e => e.Contains("resourceBonuses[0] has null resource")),
                "Expected a null-resource ConfigError");
        }

        [EmpireTest("SG.Defs")]
        public static void ShippedRoleDefs_HaveNoConfigErrors()
        {
            List<SpecialistRoleDef> defs = DefDatabase<SpecialistRoleDef>.AllDefsListForReading;
            if (defs is null || defs.Count == 0) TestAssert.Skip("No SpecialistRoleDef loaded");
            foreach (SpecialistRoleDef def in defs)
            {
                List<string> errs = def.ConfigErrors().ToList();
                TestAssert.IsFalse(errs.Any(e => e.Contains("null skill")
                        || e.Contains("null resource") || e.Contains("null stat")),
                    $"Shipped role '{def.defName}' has a null-reference ConfigError");
            }
        }

        [EmpireTest("SG.Defs")]
        public static void ShippedFocusDefs_HaveNoConfigErrors()
        {
            List<GovernorFocusDef> defs = DefDatabase<GovernorFocusDef>.AllDefsListForReading;
            if (defs is null || defs.Count == 0) TestAssert.Skip("No GovernorFocusDef loaded");
            foreach (GovernorFocusDef def in defs)
            {
                List<string> errs = def.ConfigErrors().ToList();
                TestAssert.IsFalse(errs.Any(e => e.Contains("null skill")
                        || e.Contains("null resource") || e.Contains("null stat")),
                    $"Shipped focus '{def.defName}' has a null-reference ConfigError");
            }
        }
    }
}
