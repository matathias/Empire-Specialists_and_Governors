using System.Linq;
using HarmonyLib;
using Verse;
using Verse.AI.Group;

namespace FactionColonies.Specialists
{
    [HarmonyPatch(typeof(WorldObjectComp_SettlementMilitary))]
    [HarmonyPatch("ZoomIntoTile")]
    public static class Patch_ZoomIntoTile_InjectSpecialists
    {
        static void Postfix(WorldObjectComp_SettlementMilitary __instance)
        {
            WorldSettlementFC settlement = __instance.WorldSettlement;
            if (settlement == null) return;

            WorldObjectComp_SettlementSpecialists specComp = settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (specComp == null || specComp.TotalCount == 0) return;

            Map map = settlement.Map;
            if (map == null) return;

            Lord defenseLord = map.lordManager.lords.FirstOrDefault(l => l.LordJob is LordJob_DefendColony);
            if (defenseLord == null) return;

            specComp.DeployToBattle(map, __instance.defenders, defenseLord);
        }
    }

    [HarmonyPatch(typeof(WorldObjectComp_SettlementMilitary))]
    [HarmonyPatch("EndAttack")]
    public static class Patch_EndAttack_RecoverSpecialists
    {
        static void Prefix(WorldObjectComp_SettlementMilitary __instance)
        {
            WorldSettlementFC settlement = __instance.WorldSettlement;
            if (settlement == null) return;

            WorldObjectComp_SettlementSpecialists specComp = settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (specComp == null) return;

            specComp.RecoverFromBattle();
        }
    }
}
