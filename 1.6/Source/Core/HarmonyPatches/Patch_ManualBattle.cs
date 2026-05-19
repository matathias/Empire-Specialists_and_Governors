using System.Linq;
using HarmonyLib;
using Verse;
using Verse.AI.Group;

namespace FactionColonies.Specialists
{
    /* Postfix on BattlefieldContext.ZoomIntoTile — fires only on fresh manual battles
       (Path 3), after the map exists and defender pawns + their lord have been spawned. */
    [HarmonyPatch(typeof(BattlefieldContext))]
    [HarmonyPatch("ZoomIntoTile")]
    public static class Patch_ZoomIntoTile_InjectSpecialists
    {
        static void Postfix(BattlefieldContext __instance, MilitaryOperation op)
        {
            if (op?.defender?.homeSettlement is null) return;
            WorldSettlementFC settlement = op.defender.homeSettlement;

            WorldObjectComp_SettlementSpecialists specComp =
                settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (specComp is null || specComp.TotalCount == 0) return;
            if (specComp.PawnsDeployedToBattle) return;

            Map map = __instance.map;
            if (map is null) return;

            Lord defenseLord = op.defender.pawns
                .Select(p => p?.GetLord())
                .FirstOrDefault(l => l is object);
            if (defenseLord is null) return;

            specComp.DeployToBattle(map, defenseLord);
        }
    }

    /* Prefix on BattlefieldContext.EndAttack — fires once per battle after all ops
       complete, before the map is torn down and combat hediffs stripped. */
    [HarmonyPatch(typeof(BattlefieldContext))]
    [HarmonyPatch("EndAttack")]
    public static class Patch_EndAttack_RecoverSpecialists
    {
        static void Prefix(BattlefieldContext __instance)
        {
            WorldSettlementFC settlement = __instance.ParentSettlement;
            if (settlement is null) return;

            WorldObjectComp_SettlementSpecialists specComp =
                settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (specComp is null || !specComp.PawnsDeployedToBattle) return;

            specComp.RecoverFromBattle();
        }
    }
}
