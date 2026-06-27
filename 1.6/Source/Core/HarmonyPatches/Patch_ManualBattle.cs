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

    /* Catch-all on Pawn.Kill — guarantees a death letter (and roster cleanup) whenever a roster
       member dies for ANY reason. Auto-resolve deaths route through NotifyMemberDied directly and
       remove the entry before Kill, so this no-ops for them (IsAssigned is already false). */
    [HarmonyPatch(typeof(Pawn))]
    [HarmonyPatch("Kill")]
    public static class Patch_Kill_SpecialistDeath
    {
        static void Postfix(Pawn __instance, DamageInfo? dinfo, Hediff exactCulprit)
        {
            if (!SpecialistRoster.IsAssigned(__instance)) return; // O(1) fast reject
            WorldObjectComp_SettlementSpecialists comp = SpecUtil.FindOwningComp(__instance);
            if (comp is null) return;
            SpecDeathCause cause = comp.PawnsDeployedToBattle
                ? SpecDeathCause.ManualBattle
                : SpecDeathCause.Other;
            // Append the base-game cause of death only when a real one exists (otherwise the helper
            // returns the redundant "<pawn> has died.", which our base letter line already says).
            string causeText = (dinfo.HasValue || exactCulprit is object)
                ? (string)HealthUtility.GetDiedLetterText(__instance, dinfo, exactCulprit)
                : null;
            comp.NotifyMemberDied(__instance, cause, causeText);
        }
    }
}
