using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Validation + caching coverage for the role-behavior extension seam. No active game required:
    /// <see cref="SpecialistRoleBehaviorExtension.ConfigErrors"/> and
    /// <see cref="SpecialistRoleBehaviorExtension.CreateBehavior"/> run off reference identity, and
    /// <see cref="SettlementSpecialist.Behavior"/> resolves lazily from the role's mod extension
    /// without touching the pawn. Asserts on the submod-specific ConfigError substrings only.
    /// </summary>
    public static class SpecBehaviorExtensionTests
    {
        private static List<string> Errors(SpecialistRoleBehaviorExtension ext) => ext.ConfigErrors().ToList();

        [EmpireTest("SG.Behavior")]
        public static void RoleBehaviorExt_NullClass_YieldsError()
        {
            SpecialistRoleBehaviorExtension ext = new SpecialistRoleBehaviorExtension { behaviorClass = null };
            TestAssert.IsTrue(Errors(ext).Any(e => e.Contains("behaviorClass is null")),
                "Expected a null-behaviorClass ConfigError");
        }

        [EmpireTest("SG.Behavior")]
        public static void RoleBehaviorExt_NonSubclass_YieldsError()
        {
            // A type that does not extend SpecialistRoleBehavior must be rejected.
            SpecialistRoleBehaviorExtension ext = new SpecialistRoleBehaviorExtension { behaviorClass = typeof(string) };
            TestAssert.IsTrue(Errors(ext).Any(e => e.Contains("must extend SpecialistRoleBehavior")),
                "Expected a wrong-base-type ConfigError");
        }

        [EmpireTest("SG.Behavior")]
        public static void RoleBehaviorExt_ValidClass_NoSubmodError()
        {
            SpecialistRoleBehaviorExtension ext = new SpecialistRoleBehaviorExtension
            {
                behaviorClass = typeof(RoleBehavior_Defense)
            };
            List<string> errs = Errors(ext);
            TestAssert.IsFalse(errs.Any(e => e.Contains("behaviorClass")),
                "A valid behaviorClass should raise no behaviorClass ConfigError");
        }

        [EmpireTest("SG.Behavior")]
        public static void CreateBehavior_ValidClass_WiresExtension()
        {
            SpecialistRoleBehaviorExtension ext = new SpecialistRoleBehaviorExtension
            {
                behaviorClass = typeof(RoleBehavior_Defense)
            };
            SpecialistRoleBehavior b = ext.CreateBehavior();
            TestAssert.IsNotNull(b, "CreateBehavior should instantiate a valid behaviorClass");
            TestAssert.IsTrue(b.extension == ext, "CreateBehavior should back-reference the extension");
        }

        // DirtySkillScore also invalidates the lazily-cached behavior, so a role/behavior swap can't
        // leave a stale behavior instance behind. Needs no game: Behavior resolves from the role's
        // mod extension only.
        [EmpireTest("SG.Behavior")]
        public static void DirtySkillScore_InvalidatesCachedBehavior()
        {
            SpecialistRoleDef role = new SpecialistRoleDef
            {
                defName = "TEST_BehaviorRole",
                label = "test behavior role",
                modExtensions = new List<DefModExtension>
                {
                    new SpecialistRoleBehaviorExtension { behaviorClass = typeof(RoleBehavior_Defense) }
                }
            };
            SettlementSpecialist s = new SettlementSpecialist { role = role };

            SpecialistRoleBehavior first = s.Behavior;
            TestAssert.IsNotNull(first, "expected a behavior from the role extension");
            TestAssert.IsTrue(ReferenceEquals(first, s.Behavior), "Behavior should be cached between reads");

            s.DirtySkillScore();
            TestAssert.IsFalse(ReferenceEquals(first, s.Behavior),
                "DirtySkillScore should drop the cached behavior so it is re-created");
        }
    }
}
