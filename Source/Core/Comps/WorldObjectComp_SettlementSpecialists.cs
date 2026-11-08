using System.Text;
using UnityEngine;
using Verse.AI.Group;

namespace FactionColonies.Specialists;

/* Why a roster member died — selects the death-letter wording in NotifyMemberDied. */
public enum SpecDeathCause { Other, AutoBattle, ManualBattle }

public class WorldObjectComp_SettlementSpecialists : WorldObjectComp,
    ISettlementWindowOverview, IStatModifierProvider, IResourceProductionModifier, IProfitContributor
{
    // ── Roster ──
    private List<SettlementSpecialist> specialists = [];
    private List<SettlementSpecialist> residents = [];
    private SettlementGovernor governor;

    // ── Supply chain satisfaction ──
    private float foodSatisfaction = 1f;
    private float medicineSatisfaction = 1f;

    public float FoodSatisfaction
    {
        get { return foodSatisfaction; }
        set { foodSatisfaction = Mathf.Clamp01(value); }
    }

    public float MedicineSatisfaction
    {
        get { return medicineSatisfaction; }
        set { medicineSatisfaction = Mathf.Clamp01(value); }
    }

    /// <summary>Combined supply satisfaction (food times medicine) that scales every member bonus.</summary>
    public float Satisfaction => foodSatisfaction * medicineSatisfaction;

    // ── Battle state ──
    private bool pawnsDeployedToBattle = false;
    public bool PawnsDeployedToBattle => pawnsDeployedToBattle;
    private List<Pawn> deployedPawns = [];

    // ── Properties shortcut ──
    public WorldObjectCompProperties_SettlementSpecialists Props =>
        (WorldObjectCompProperties_SettlementSpecialists)props;

    // ── Settlement accessor ──
    public WorldSettlementFC Settlement => (WorldSettlementFC)parent;

    // ── Roster accessors ──
    public IReadOnlyList<SettlementSpecialist> Specialists => specialists;
    public IReadOnlyList<SettlementSpecialist> Residents => residents;
    public SettlementGovernor Governor => governor;

    /// <summary>
    /// Every roster member, dead ones included: specialists, then residents, then the governor.
    /// </summary>
    public IEnumerable<RosterEntry> Members
    {
        get
        {
            foreach (SettlementSpecialist s in specialists)
                yield return s;
            foreach (SettlementSpecialist r in residents)
                yield return r;
            if (governor is not null)
                yield return governor;
        }
    }

    public bool HasGovernor => governor is not null && governor.IsAlive;
    public int SpecialistCount => specialists.Count;
    public int ResidentCount => residents.Count;
    public int TotalCount => specialists.Count + residents.Count + (governor is not null ? 1 : 0);

    public int MaxSpecialists
    {
        get
        {
            int max = FCSSettings.specialistBaseMax
                + (int)Math.Floor(Settlement.settlementLevel / (double)FCSSettings.specialistPerLevels);
            if (SpecUtil.HasTrait(SpecPolicyDefOf.FCSspecialistCorps))
                max += 2;
            return max;
        }
    }

    public int LiveResidentCount
    {
        get
        {
            int count = 0;
            foreach (SettlementSpecialist r in residents)
                if (r.IsAlive) count++;
            return count;
        }
    }

    // ── Core roster operations ──

    /// <summary>
    /// True if the role defines a per-settlement cap and this settlement is already at it. A pawn
    /// assigned to a capped role is rejected without being placed, so callers that must not drop the
    /// pawn should fall back to a Resident (null role) when this returns true.
    /// </summary>
    public bool RoleAtCap(SpecialistRoleDef role) => RoleAtCap(role, 0);

    /// <summary>
    /// <see cref="RoleAtCap(SpecialistRoleDef)"/>, counting pendingForRole not-yet-assigned pawns
    /// (e.g. queued in a dialog) as already holding the role.
    /// </summary>
    public bool RoleAtCap(SpecialistRoleDef role, int pendingForRole)
    {
        if (role is null || role.maxPerSettlement <= 0) return false;
        int existing = pendingForRole;
        foreach (SettlementSpecialist s in specialists)
            if (s.role == role) existing++;
        return existing >= role.maxPerSettlement;
    }

    /// <summary>
    /// True if every specialist slot is taken, counting pending not-yet-assigned specialists
    /// (e.g. queued in a dialog) as already assigned.
    /// </summary>
    public bool SpecialistSlotsFull(int pending = 0) => SpecialistCount + pending >= MaxSpecialists;

    /// <summary>
    /// True if a specialist assignment for this role would be accepted right now. Residents
    /// (null role) always fit. Mirrors the guards in <see cref="AssignSpecialist"/> so callers
    /// can decide (before touching a caravan) whether the placement is guaranteed.
    /// </summary>
    public bool CanAssignSpecialist(SpecialistRoleDef role)
    {
        if (role is null) return true;
        return !SpecialistSlotsFull() && !RoleAtCap(role);
    }

    /// <summary>
    /// True if a governor assignment would be accepted right now (the settlement has no governor).
    /// Mirrors the guard in <see cref="AssignGovernor"/>.
    /// </summary>
    public bool CanAssignGovernor => governor is null;

    public bool AssignSpecialist(Pawn pawn, SpecialistRoleDef role)
    {
        if (pawn is null) return false;

        if (role is not null && SpecialistCount >= MaxSpecialists)
        {
            LogSG.Message($"Cannot assign {pawn.LabelShort}: max specialists reached at {Settlement.Name}");
            Messages.Message("FCS_CannotAssignSpecialistRole".Translate(
                pawn.LabelShort, role.LabelCap, Settlement.Name, MaxSpecialists),
                MessageTypeDefOf.RejectInput);
            return false;
        }

        if (RoleAtCap(role))
        {
            LogSG.Message($"Cannot assign {role.defName}: max per settlement reached");
            Messages.Message("FCS_CannotAssignSpecialistRole".Translate(
                pawn.LabelShort, role.LabelCap, Settlement.Name, role.maxPerSettlement),
                MessageTypeDefOf.RejectInput);
            return false;
        }

        SettlementSpecialist entry = new(pawn, role);
        if (role is null)
            residents.Add(entry);
        else
            specialists.Add(entry);

        SpecialistRoster.Assign(pawn, role);
        pawn.SetFaction(FindFC.EmpireFaction);
        SpecUtil.KeepAsWorldPawn(pawn);

        LogSG.Message($"Assigned {pawn.LabelShort} as {role?.LabelCap ?? "Resident"} to {Settlement.Name}");
        InvalidateAll();
        return true;
    }

    public bool AssignGovernor(Pawn pawn, GovernorFocusDef focus)
    {
        if (pawn is null || focus is null) return false;

        if (governor is not null)
        {
            LogSG.Warning("Settlement already has a governor");
            return false;
        }

        governor = new SettlementGovernor(pawn, focus);
        SpecialistRoster.AssignGovernor(pawn);

        pawn.SetFaction(FindFC.EmpireFaction);
        SpecUtil.KeepAsWorldPawn(pawn);

        if (governor.Behavior is not null)
            governor.Behavior.OnFocusActivated(Settlement);

        LogSG.Message($"Assigned {pawn.LabelShort} as Governor ({focus.LabelCap}) to {Settlement.Name}");
        InvalidateAll();
        return true;
    }

    public void RecallSpecialist(SettlementSpecialist entry)
    {
        if (entry?.pawn is null) return;
        Pawn pawn = entry.pawn;
        specialists.Remove(entry);
        residents.Remove(entry);
        deployedPawns.Remove(pawn);   // battle recovery must not reclaim a pawn handed back to the player
        SpecialistRoster.Recall(pawn);
        DeliverRecalledPawn(pawn);
        LogSG.Message($"Recalled {pawn.LabelShort} from {Settlement.Name}");
        ResetSatisfactionIfRosterEmpty();
        InvalidateAll();
    }

    public void RecallGovernor()
    {
        if (governor is null) return;
        Pawn pawn = governor.pawn;
        if (governor.Behavior is not null)
            governor.Behavior.OnFocusDeactivated(Settlement);
        SpecialistRoster.Recall(pawn);
        governor = null;
        if (pawn is not null)
        {
            deployedPawns.Remove(pawn);   // battle recovery must not reclaim a pawn handed back to the player
            DeliverRecalledPawn(pawn);
        }
        LogSG.Message($"Recalled governor from {Settlement.Name}");
        ResetSatisfactionIfRosterEmpty();
        InvalidateAll();
    }

    public void PromoteToGovernor(SettlementSpecialist entry, GovernorFocusDef focus)
    {
        if (entry?.pawn is null || focus is null) return;
        Pawn pawn = entry.pawn;

        // Remove from specialist/resident lists
        specialists.Remove(entry);
        residents.Remove(entry);

        // Demote existing governor to resident (if any)
        if (governor is not null)
        {
            Pawn oldGovPawn = governor.pawn;
            if (governor.Behavior is not null)
                governor.Behavior.OnFocusDeactivated(Settlement);
            SpecialistRoster.Recall(oldGovPawn);

            SettlementSpecialist demoted = new(oldGovPawn, null);
            residents.Add(demoted);
            SpecialistRoster.Assign(oldGovPawn, null);
        }

        // Update roster tracking
        SpecialistRoster.Recall(pawn);
        SpecialistRoster.AssignGovernor(pawn);

        // Create new governor
        governor = new SettlementGovernor(pawn, focus);
        if (governor.Behavior is not null)
            governor.Behavior.OnFocusActivated(Settlement);

        LogSG.Message($"Promoted {pawn.LabelShort} to Governor ({focus.LabelCap}) at {Settlement.Name}");
        InvalidateAll();
    }

    public void ChangeGovernorFocus(GovernorFocusDef newFocus)
    {
        if (governor is null || newFocus is null) return;
        governor.SetFocus(newFocus, Settlement);
        InvalidateAll();
    }

    public void ChangeRole(SettlementSpecialist entry, SpecialistRoleDef newRole)
    {
        if (entry is null) return;

        // Check capacity when moving to a specialist role
        if (newRole is not null && entry.role is null && SpecialistCount >= MaxSpecialists)
        {
            Messages.Message("FCS_CannotAssignSpecialistRole".Translate(
                entry.pawn.LabelShort, newRole.LabelCap, Settlement.Name, MaxSpecialists),
                MessageTypeDefOf.RejectInput);
            return;
        }

        // Moving between specialist and resident lists
        if (entry.role is null && newRole is not null)
        {
            residents.Remove(entry);
            specialists.Add(entry);
        }
        else if (entry.role is not null && newRole is null)
        {
            specialists.Remove(entry);
            residents.Add(entry);
        }

        entry.role = newRole;
        entry.DirtySkillScore();
        SpecialistRoster.Assign(entry.pawn, newRole);
        InvalidateAll();
    }

    public void RemoveSpecialist(SettlementSpecialist entry)
    {
        if (entry is null) return;
        specialists.Remove(entry);
        residents.Remove(entry);
        if (entry.pawn is not null)
        {
            deployedPawns.Remove(entry.pawn);
            SpecialistRoster.Recall(entry.pawn);
        }
        ResetSatisfactionIfRosterEmpty();
        InvalidateAll();
    }

    public List<Pawn> AllPawnsSnapshot()
    {
        List<Pawn> result = [];
        foreach (RosterEntry m in Members)
            if (m.IsAlive) result.Add(m.pawn);
        return result;
    }

    private void DeliverRecalledPawn(Pawn pawn)
    {
        if (pawn is null) return;
        DeliverPawnsToPlayer([pawn]);
    }

    /// <summary>
    /// Return recalled/released pawns to the player, wherever each one currently is. A roster member
    /// is an allied-faction world pawn, but an external mod (e.g. Hospitality) can pull it onto a map
    /// as a visitor; each pawn is first released from any visitor Lord/guest state, then handed over.
    /// A pawn already standing on one of the player's own maps is claimed in place (no pointless
    /// teleport); every other pawn is detached from where it's held and delivered layer-aware: a
    /// caravan at the settlement tile on caravan-capable layers (surface), or a drop
    /// pod to the player's home colony on layers that can't form caravans (orbit) so it's never
    /// stranded. DropThingsNear -> MakeDropPodAt removes each from WorldPawns on landing, releasing
    /// the KeepForever hold.
    /// </summary>
    private void DeliverPawnsToPlayer(List<Pawn> pawns)
    {
        if (pawns is null || pawns.Count == 0) return;

        List<Pawn> toDeliver = [];
        foreach (Pawn pawn in pawns)
        {
            if (pawn is null || pawn.Dead) continue; // dead members are cleaned up by the roster, never delivered

            // Release any visitor Lord/guest-bed state before touching faction/location, with no
            // goodwill penalty (no-op when the pawn isn't a spawned guest).
            SpecUtil.ReleaseVisitorState(pawn);

            // Already on one of the player's own maps: just hand it over where it stands instead of despawning and shipping it back.
            if (pawn.Spawned && pawn.Map is not null && pawn.Map.IsPlayerHome)
            {
                pawn.SetFaction(Faction.OfPlayer);
                continue;
            }

            if (pawn.Spawned) pawn.DeSpawn();
            else SpecUtil.DetachFromHolder(pawn); // un-hold from a caravan / container
            pawn.SetFaction(Faction.OfPlayer);
            if (!pawn.IsWorldPawn())
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            toDeliver.Add(pawn);
        }

        if (toDeliver.Count == 0) return; // everyone was claimed in place (or filtered out)

        // Release the roster's KeepForever hold now that they are player pawns again; otherwise a
        // pawn that later dies in the caravan would stay pinned as a world pawn forever.
        foreach (Pawn pawn in toDeliver)
            Find.WorldPawns.ForcefullyKeptPawns.Remove(pawn);

        if (Settlement.Tile.LayerDef.canFormCaravans)
        {
            CaravanMaker.MakeCaravan(toDeliver, Faction.OfPlayer, Settlement.Tile, false);
            return;
        }

        // Non-caravan layer (orbit): drop-pod to the player's home colony, with a letter since the
        // pawns land off-screen.
        Map map = FindFC.TaxMap;
        if (map is not null)
        {
            IntVec3 cell = DropCellFinder.TradeDropSpot(map);
            DropPodUtility.DropThingsNear(cell, map, toDeliver, 110, false, false, false, false);
            Find.LetterStack.ReceiveLetter(
                "FCS_LetterRecallDropPodLabel".Translate(),
                "FCS_LetterRecallDropPodText".Translate(toDeliver.Count, Settlement.Name, map.Parent.Label),
                LetterDefOf.PositiveEvent);
            return;
        }

        // No player home map at all: last-resort caravan so the pawns are never lost.
        LogSG.Warning($"DeliverPawnsToPlayer: no player home map; forming caravan at {Settlement.Name} tile as last resort");
        CaravanMaker.MakeCaravan(toDeliver, Faction.OfPlayer, Settlement.Tile, false);
    }

    /// <summary>
    /// Disband the entire roster when the settlement is removed (abandoned or lost). Clears every
    /// member from the static SpecialistRoster -- otherwise their thingIDNumbers stay assigned for
    /// the rest of the session -- and returns the survivors to the player (caravan on the surface,
    /// drop pod from orbit; see <see cref="DeliverPawnsToPlayer"/>). Called from
    /// SpecialistLifecycleHandler.OnSettlementRemoved, before the comp is destroyed with its
    /// WorldObject.
    /// </summary>
    public void DisbandRosterOnRemoval()
    {
        if (specialists.Count == 0 && residents.Count == 0 && governor is null) return;

        // Recall EVERY member (dead included) from the static roster so no stale thingIDNumber
        // survives, and collect the living for delivery.
        List<Pawn> survivors = [];
        foreach (RosterEntry m in Members)
        {
            if (m is SettlementGovernor gov)
                gov.Behavior?.OnFocusDeactivated(Settlement);
            if (m.pawn is null) continue;
            SpecialistRoster.Recall(m.pawn);
            if (m.IsAlive) survivors.Add(m.pawn);
        }

        specialists.Clear();
        residents.Clear();
        governor = null;
        ResetSatisfactionIfRosterEmpty();

        // Return survivors to the player (layer-aware) and mark the settlement-loss event. The
        // delivery may be a drop pod (orbit), so the disband letter stays delivery-agnostic.
        if (survivors.Count > 0)
        {
            DeliverPawnsToPlayer(survivors);

            Find.LetterStack.ReceiveLetter(
                "FCS_LetterRosterDisbandedLabel".Translate(),
                "FCS_LetterRosterDisbandedText".Translate(survivors.Count, Settlement.Name),
                LetterDefOf.NeutralEvent);
        }

        LogSG.Message($"Disbanded roster of {Settlement.Name} on removal ({survivors.Count} survivor(s) returned)");
    }

    private void InvalidateAll()
    {
        foreach (RosterEntry m in Members) m.DirtySkillScore();
        Settlement.InvalidateStatCache();
    }

    /* When the roster empties, reset supply-chain satisfaction to 1. The Routes & Resources bridge
     * only writes these fields while the roster has members (CollectNeeds early-outs on an empty
     * roster), so a starved-then-emptied settlement would otherwise keep a stale <1 value scribed
     * forever -- silently scaling every future member's bonuses, and permanently so if R&R is later
     * removed (the read sites apply satisfaction unconditionally). */
    private void ResetSatisfactionIfRosterEmpty()
    {
        if (specialists.Count == 0 && residents.Count == 0 && governor is null)
        {
            foodSatisfaction = 1f;
            medicineSatisfaction = 1f;
        }
    }

    // ── Manual Battle Integration ──

    public void DeployToBattle(Map map, Lord defenseLord)
    {
        if (pawnsDeployedToBattle) return;
        deployedPawns.Clear();

        List<Pawn> allPawns = AllPawnsSnapshot();
        foreach (Pawn pawn in allPawns)
        {
            // A member that is away (e.g. pulled onto another map as a Hospitality visitor, or held in a
            // caravan/container) can't defend; leave it where it is so its visit Lord keeps it.
            if (pawn.Spawned || pawn.holdingOwner is not null) continue;

            if (pawn.IsWorldPawn())
                Find.WorldPawns.RemovePawn(pawn);

            if (pawn.Faction != FindFC.EmpireFaction)
                pawn.SetFaction(FindFC.EmpireFaction);

            // Pick a standable, reachable cell so pawns don't spawn inside walls/structures.
            // GenSpawn.Spawn already registers the pawn with mapPawns (idempotent), so no explicit RegisterPawn.
            IntVec3 loc;
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 15,
                    c => c.Standable(map) && !c.Fogged(map)
                         && map.reachability.CanReachMapEdge(c, TraverseParms.For(pawn)),
                    out loc))
                loc = map.Center;
            GenSpawn.Spawn(pawn, loc, map);
            pawn.drafter ??= new Pawn_DraftController(pawn);

            defenseLord.AddPawn(pawn);
            deployedPawns.Add(pawn);
        }

        pawnsDeployedToBattle = true;
        LogSG.Message($"Deployed {deployedPawns.Count} specialists to defend {Settlement.Name}");
    }

    public void RecoverFromBattle()
    {
        if (!pawnsDeployedToBattle) return;

        // Deaths during the on-map battle were already handled the moment each pawn was killed
        // (Patch_Kill_SpecialistDeath -> NotifyMemberDied), so any deployed pawn still alive here
        // is a survivor. Recover exactly who we deployed -- not the current roster: a drop pod
        // launched before the attack can arrive mid-battle and is passed to the world by the
        // arrival handler; it was never on this map, so it must not be touched here.
        foreach (Pawn pawn in deployedPawns)
        {
            if (pawn is null || pawn.Dead) continue;
            // A member recalled, removed or moved elsewhere mid-battle is no longer ours to reclaim
            if (SpecUtil.FindOwningComp(pawn) != this) continue;
            if (pawn.Spawned) pawn.DeSpawn();
            pawn.SetFaction(FindFC.EmpireFaction);
            Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
        }

        deployedPawns.Clear();
        pawnsDeployedToBattle = false;
        InvalidateAll();
        LogSG.Message($"Recovered specialists from battle at {Settlement.Name}");
    }

    /* Single funnel for every roster-member death, regardless of cause. Removes the member from
       the roster and sends exactly one death letter (wording chosen by cause). Idempotent: a
       second call for an already-removed pawn is a no-op. causeText, when supplied, is the
       base-game cause-of-death sentence appended on a new line. */
    public void NotifyMemberDied(Pawn pawn, SpecDeathCause cause, string causeText = null)
    {
        if (pawn is null) return;

        RosterEntry member = null;
        foreach (RosterEntry m in Members)
        {
            if (m.pawn == pawn)
            {
                member = m;
                break;
            }
        }

        string roleLabel;
        switch (member)
        {
            case SettlementGovernor:
                roleLabel = "FCS_RoleGovernor".Translate();
                governor.Behavior?.OnFocusDeactivated(Settlement);
                SpecialistRoster.Recall(pawn);
                governor = null;
                ResetSatisfactionIfRosterEmpty();
                InvalidateAll();
                break;
            case SettlementSpecialist entry:
                roleLabel = specialists.Contains(entry)
                    ? entry.role?.LabelCap ?? "Specialist"
                    : "FCS_RoleResident".Translate();
                RemoveSpecialist(entry);
                break;
            default:
                return; // not a member of this settlement (or already removed)
        }

        Find.WorldPawns.ForcefullyKeptPawns.Remove(pawn); // drop the roster's KeepForever hold

        string body = cause switch
        {
            SpecDeathCause.AutoBattle => "FCS_LetterDeathAttack".Translate(pawn.LabelShort, roleLabel, Settlement.Name),
            SpecDeathCause.ManualBattle => "FCS_LetterDeathDefending".Translate(pawn.LabelShort, roleLabel, Settlement.Name),
            _ => "FCS_LetterDeathGeneric".Translate(pawn.LabelShort, roleLabel, Settlement.Name),
        };
        if (!causeText.NullOrEmpty())
            body += "\n\n" + causeText;
        SpecUtil.SendDeathLetter(roleLabel, body);
    }

    // ── Serialization ──

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref specialists, "specialists", LookMode.Deep);
        Scribe_Collections.Look(ref residents, "residents", LookMode.Deep);
        Scribe_Deep.Look(ref governor, "governor");
        Scribe_Values.Look(ref foodSatisfaction, "foodSatisfaction", 1f);
        Scribe_Values.Look(ref medicineSatisfaction, "medicineSatisfaction", 1f);
        Scribe_Values.Look(ref pawnsDeployedToBattle, "pawnsDeployedToBattle", false);
        Scribe_Collections.Look(ref deployedPawns, "deployedPawns", LookMode.Reference);

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            specialists ??= [];
            residents ??= [];
            deployedPawns ??= [];
            specialists.RemoveAll(s => s?.pawn is null);
            residents.RemoveAll(r => r?.pawn is null);
            deployedPawns.RemoveAll(p => p is null);
            if (governor is not null && governor.pawn is null) governor = null;
        }
    }

    // ── XP Ticking ──

    public override void CompTick()
    {
        base.CompTick();
        if (Find.TickManager.TicksGame % GenDate.TicksPerDay != 0) return;
        DoDailySkillTick();
    }

    /// <summary>
    /// Daily skill growth: raise each member's weighted role/focus skills, then dirty the
    /// skill-score caches and the settlement stat cache so the grown levels feed production/stat/
    /// upkeep values in the same session. Without the final <see cref="InvalidateAll"/> the cached
    /// scores stay frozen at assignment-time values until the next reload.
    /// </summary>
    public void DoDailySkillTick()
    {
        // Only roles/foci grow; residents (null role) don't, so skip the no-op loops and the
        // needless daily stat-cache invalidation when nothing can grow.
        if (specialists.Count == 0 && governor is null) return;

        float xp = SpecUtil.XPPerDay();

        foreach (SettlementSpecialist s in specialists)
        {
            if (!s.HasUsableSkills || s.role is null) continue;
            SpecUtil.LearnWeighted(s.pawn, s.role.skillWeights, xp);
        }

        if (governor is not null && governor.HasUsableSkills && governor.focus?.skillWeights is not null)
        {
            SpecUtil.LearnWeighted(governor.pawn, governor.focus.skillWeights, xp);
            // Governor always gets Social XP
            SkillRecord social = governor.pawn.skills.GetSkill(SkillDefOf.Social);
            if (social is not null && !social.TotallyDisabled)
                social.Learn(xp, true);
        }

        // Surface the grown levels this session: dirty every member's cached SkillScore and the
        // settlement stat cache.
        InvalidateAll();
    }

    // ── Caravan gizmos ──

    public override IEnumerable<Gizmo> GetCaravanGizmos(Caravan caravan)
    {
        yield return new Command_Action
        {
            defaultLabel = "FCS_GizmoAssignLabel".Translate(),
            defaultDesc = "FCS_GizmoAssignDesc".Translate(Settlement.Name),
            icon = TexCommand.Install,
            action = delegate
            {
                Find.WindowStack.Add(new Dialog_AssignSpecialists(caravan, this));
            }
        };
    }

    // ── ISettlementWindowOverview ──

    private SpecialistsTabRenderer tabRenderer;

    private SpecialistsTabRenderer TabRenderer => tabRenderer ??= new SpecialistsTabRenderer(this);

    public void PreOpenWindow(WorldSettlementFC settlement) { TabRenderer.PreOpenWindow(settlement); }
    public void OnTabSwitch() { TabRenderer.OnTabSwitch(); }

    public void DrawOverviewTab(Rect boundingBox)
    {
        TabRenderer.DrawOverviewTab(boundingBox);
    }

    public void PostCloseWindow() { TabRenderer.PostCloseWindow(); }
    public string OverviewTabName() => TabRenderer.OverviewTabName();

    public bool ShouldShowOverviewTab(WorldSettlementFC settlement) => true;

    // ── IResourceProductionModifier ──

    public double GetResourceAdditiveModifier(ResourceFC resource)
    {
        double total = 0;
        foreach (SettlementSpecialist s in specialists)
        {
            total += SpecUtil.SpecialistAdditiveForResource(s, resource.def);
        }

        if (total > 0 && SpecUtil.HasTrait(SpecPolicyDefOf.FCSspecialistCorps))
            total *= 1.2;

        float satisfaction = Satisfaction;
        return total * satisfaction;
    }

    public double GetResourceMultiplierModifier(ResourceFC resource)
    {
        if (governor is null || !governor.HasUsableSkills) return 1.0;
        float satisfaction = Satisfaction;
        double rawMult = SpecUtil.GovernorMultiplierForResource(governor, resource.def);
        return 1.0 + (rawMult - 1.0) * satisfaction;
    }

    public string GetResourceAdditiveDesc(ResourceFC resource)
    {
        StringBuilder sb = new();
        double total = 0;
        foreach (SettlementSpecialist s in specialists)
        {
            double bonus = SpecUtil.SpecialistAdditiveForResource(s, resource.def);
            if (Math.Abs(bonus) > 0.001)
            {
                total += bonus;
                sb.AppendLine(TextUtil.AdditiveBonusLine(bonus,
                    s.pawn.LabelShort + " (" + s.role.LabelCap + ")"));
            }
        }
        if (sb.Length == 0) return null;

        // Mirror the multipliers GetResourceAdditiveModifier applies to the summed bonus so the
        // breakdown reconciles with the value shown.
        if (total > 0 && SpecUtil.HasTrait(SpecPolicyDefOf.FCSspecialistCorps))
            sb.AppendLine(TextUtil.MultiplierBonusLine(1.2, SpecPolicyDefOf.FCSspecialistCorps.LabelCap));
        float satisfaction = Satisfaction;
        if (satisfaction < 0.999f)
            sb.AppendLine(TextUtil.MultiplierBonusLine(satisfaction, "FCS_SatisfactionLabel".Translate()));

        return sb.ToString().TrimEnd();
    }

    public string GetResourceMultiplierDesc(ResourceFC resource)
    {
        if (governor is null || !governor.HasUsableSkills) return null;
        double rawMult = SpecUtil.GovernorMultiplierForResource(governor, resource.def);
        if (Math.Abs(rawMult - 1.0) < 0.001) return null;
        // Show the effective multiplier GetResourceMultiplierModifier actually applies (raw damped
        // by satisfaction) so the breakdown reconciles with the value.
        float satisfaction = Satisfaction;
        double effectiveMult = 1.0 + (rawMult - 1.0) * satisfaction;
        return TextUtil.MultiplierBonusLine(effectiveMult,
            governor.pawn.LabelShort + " (" + governor.focus.LabelCap + ")");
    }

    // ── IStatModifierProvider ──

    public double GetStatModifier(FCStatDef stat)
    {
        double value = stat.IdentityValue;

        // Worker bonus from residents
        if (stat == FCStatDefOf.workerBaseMax)
            value += SpecUtil.WorkerBonusFromResidents(LiveResidentCount);

        // Specialist stat bonuses (additive, skill-scaled)
        float satisfaction = Satisfaction;
        foreach (SettlementSpecialist s in specialists)
        {
            value += SpecUtil.SpecialistStatBonus(s, stat) * satisfaction;
        }

        // Governor stat multiplier
        if (governor is not null && governor.HasUsableSkills)
        {
            double govMult = SpecUtil.GovernorStatMultiplier(governor, stat);
            if (Math.Abs(govMult - 1.0) > 0.001)
                value += (govMult - 1.0) * satisfaction;
        }

        // Heal rate from Apothecary behavior
        if (stat == FCStatDefOf.mercHealRateMultiplier)
            value += SpecUtil.MedicalHealRateBonus(specialists, governor) * satisfaction;

        // Garrison Doctrine: Commander specialists boost settlement happiness
        if (stat == FCStatDefOf.happinessGainedBase)
            value += SpecUtil.GarrisonHappinessBonus(specialists) * satisfaction;

        return value;
    }

    public string GetStatModifierDesc(FCStatDef stat)
    {
        StringBuilder sb = new();
        // Whether any satisfaction-scaled contribution was added. The resident worker bonus below
        // is NOT satisfaction-scaled, but no shipped role/focus modifies workerBaseMax, so it never
        // coexists with the scaled contributions on the same stat.
        bool anyScaled = false;

        if (stat == FCStatDefOf.workerBaseMax)
        {
            int residentCount = LiveResidentCount;
            int bonus = SpecUtil.WorkerBonusFromResidents(residentCount);
            if (bonus > 0)
                sb.Append("FCS_StatWorkerBonus".Translate(bonus, residentCount));
        }

        foreach (SettlementSpecialist s in specialists)
        {
            double statBonus = SpecUtil.SpecialistStatBonus(s, stat);
            if (Math.Abs(statBonus) > 0.001)
            {
                if (sb.Length > 0) sb.Append("\n");
                sb.Append(TextUtil.AdditiveBonusLine(statBonus,
                    s.pawn.LabelShort + " (" + s.role.LabelCap + ")", invert: stat.invertedForDisplay));
                anyScaled = true;
            }
        }

        // Governor's additive stat contribution (mirrors the (govMult - 1.0) term in GetStatModifier)
        if (governor is not null && governor.HasUsableSkills)
        {
            double govMult = SpecUtil.GovernorStatMultiplier(governor, stat);
            if (Math.Abs(govMult - 1.0) > 0.001)
            {
                if (sb.Length > 0) sb.Append("\n");
                sb.Append(TextUtil.AdditiveBonusLine(govMult - 1.0,
                    governor.pawn.LabelShort + " (" + governor.focus.LabelCap + ")", invert: stat.invertedForDisplay));
                anyScaled = true;
            }
        }

        if (stat == FCStatDefOf.mercHealRateMultiplier)
        {
            double healBonus = SpecUtil.MedicalHealRateBonus(specialists, governor);
            if (healBonus > 0)
            {
                if (sb.Length > 0) sb.Append("\n");
                sb.Append("FCS_HealRateLine".Translate(Math.Round(healBonus, 2)));
                anyScaled = true;
            }
        }

        if (stat == FCStatDefOf.happinessGainedBase)
        {
            double garrisonBonus = SpecUtil.GarrisonHappinessBonus(specialists);
            if (garrisonBonus > 0)
            {
                if (sb.Length > 0) sb.Append("\n");
                sb.Append("FCS_GarrisonHappinessLine".Translate(Math.Round(garrisonBonus, 2)));
                anyScaled = true;
            }
        }

        // Supply satisfaction scales every contribution above (except the worker bonus); show it so
        // the breakdown reconciles with the applied value.
        float satisfaction = Satisfaction;
        if (anyScaled && satisfaction < 0.999f)
        {
            if (sb.Length > 0) sb.Append("\n");
            sb.Append(TextUtil.MultiplierBonusLine(satisfaction, "FCS_SatisfactionLabel".Translate()));
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    // ── IProfitContributor ──

    public double GetDailyUpkeepContribution()
    {
        double total = 0;
        foreach (SettlementSpecialist s in specialists)
            total += SpecUtil.SpecialistUpkeep(s);
        if (governor is not null)
            total += SpecUtil.GovernorUpkeep(governor);
        return total;
    }

    public string GetDailyUpkeepContributionDesc()
    {
        double total = GetDailyUpkeepContribution();
        if (total <= 0) return null;
        return $"+{Math.Round(total, 2)} - {"FCS_UpkeepSettlementLine".Translate()}";
    }
}
