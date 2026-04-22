using System;
using Verse;

namespace FactionColonies.Specialists
{
    public class RoleBehaviorExt_Defense : SpecialistRoleBehaviorExtension
    {
        public float baseReductionPerSkillPoint = 0.02f;
        public float minimumDeathChance = 0f;
    }

    public class RoleBehavior_Defense : SpecialistRoleBehavior
    {
        public override void ModifyDeathChances(WorldSettlementFC settlement, float skillScore,
            ref float govChance, ref float specChance, ref float residentChance)
        {
            RoleBehaviorExt_Defense ext = Ext<RoleBehaviorExt_Defense>();
            float reduction = skillScore * ext.baseReductionPerSkillPoint;
            float floor = ext.minimumDeathChance;

            govChance = Math.Max(floor, govChance - reduction);
            specChance = Math.Max(floor, specChance - reduction);
            residentChance = Math.Max(floor, residentChance - reduction);
        }
    }
}
