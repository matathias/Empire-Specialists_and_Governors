namespace FactionColonies.Specialists;

public class GameComponent_SpecialistRoster : GameComponent
{
    public GameComponent_SpecialistRoster(Game game)
    {
    }

    public override void FinalizeInit()
    {
        SpecialistRoster.Rebuild();
        KeepRosterPawns();

        // Patrician was removed as a feature. Strip it from any faction that had adopted it.
        if (RemovePatricianTrait(FindFC.PolicyManager))
            Messages.Message("FCS_PatricianRemoved".Translate(), MessageTypeDefOf.NeutralEvent);
    }

    /* Older saves assigned caravan and pod pawns without adding them to the forcefully kept set
       (see SpecUtil.KeepAsWorldPawn). Only touches pawns already in WorldPawns; idempotent, so it
       simply runs on every load. */
    private static void KeepRosterPawns()
    {
        List<WorldSettlementFC> settlements = FindFC.Settlements;
        if (settlements is null) return;
        foreach (WorldSettlementFC settlement in settlements)
        {
            WorldObjectComp_SettlementSpecialists comp =
                settlement?.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) continue;
            foreach (Pawn pawn in comp.AllPawnsSnapshot())
                if (pawn.IsWorldPawn()) Find.WorldPawns.ForcefullyKeptPawns.Add(pawn);
        }
    }

    /* Scrubs the retired Patrician trait out of a faction's fixed trait slots, restoring each to
       the empty policy. Returns true if anything was removed. Idempotent, and safe to call with a
       null manager (pre-faction loads). Kept as a static so it can be unit-tested without a load. */
    public static bool RemovePatricianTrait(PolicyManager pm)
    {
        if (pm?.factionTraits is null) return false;

        bool removed = false;
        for (int i = 0; i < pm.factionTraits.Count; i++)
        {
            FCPolicy p = pm.factionTraits[i];
            if (p?.def != SpecPolicyDefOf.FCSpatrician) continue;

            p.behavior?.OnRemoved(FindFC.FactionComp); // Patrician has no behavior today; generic-safe
            pm.factionTraits[i] = new FCPolicy(FCPolicyDefOf.empty);
            removed = true;
        }

        if (removed) pm.RebuildBehaviorCache();
        return removed;
    }
}
