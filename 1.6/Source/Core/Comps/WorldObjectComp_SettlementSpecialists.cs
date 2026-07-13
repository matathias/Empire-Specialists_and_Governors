using System;
using System.Collections.Generic;
using System.Text;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace FactionColonies.Specialists
{
    /* Why a roster member died — selects the death-letter wording in NotifyMemberDied. */
    public enum SpecDeathCause { Other, AutoBattle, ManualBattle }

    public class WorldObjectComp_SettlementSpecialists : WorldObjectComp,
        ISettlementWindowOverview, IStatModifierProvider, IResourceProductionModifier, IProfitContributor
    {
        // ── Roster ──
        private List<SettlementSpecialist> specialists = new List<SettlementSpecialist>();
        private List<SettlementSpecialist> residents = new List<SettlementSpecialist>();
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

        // ── Battle state ──
        private bool pawnsDeployedToBattle = false;
        public bool PawnsDeployedToBattle => pawnsDeployedToBattle;
        private List<Pawn> deployedPawns = new List<Pawn>();

        // ── Properties shortcut ──
        public WorldObjectCompProperties_SettlementSpecialists Props =>
            (WorldObjectCompProperties_SettlementSpecialists)props;

        // ── Settlement accessor ──
        public WorldSettlementFC Settlement => (WorldSettlementFC)parent;

        // ── Roster accessors ──
        public IReadOnlyList<SettlementSpecialist> Specialists => specialists;
        public IReadOnlyList<SettlementSpecialist> Residents => residents;
        public SettlementGovernor Governor => governor;
        public bool HasGovernor => governor is object && governor.IsAlive;
        public int SpecialistCount => specialists.Count;
        public int ResidentCount => residents.Count;
        public int TotalCount => specialists.Count + residents.Count + (governor is object ? 1 : 0);

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
        public bool RoleAtCap(SpecialistRoleDef role)
        {
            if (role is null || role.maxPerSettlement <= 0) return false;
            int existing = 0;
            foreach (SettlementSpecialist s in specialists)
                if (s.role == role) existing++;
            return existing >= role.maxPerSettlement;
        }

        /// <summary>
        /// True if a specialist assignment for this role would be accepted right now. Residents
        /// (null role) always fit. Mirrors the guards in <see cref="AssignSpecialist"/> so callers
        /// can decide (before touching a caravan) whether the placement is guaranteed.
        /// </summary>
        public bool CanAssignSpecialist(SpecialistRoleDef role)
        {
            if (role is null) return true;
            return SpecialistCount < MaxSpecialists && !RoleAtCap(role);
        }

        /// <summary>
        /// True if a governor assignment would be accepted right now (the settlement has no governor).
        /// Mirrors the guard in <see cref="AssignGovernor"/>.
        /// </summary>
        public bool CanAssignGovernor => governor is null;

        public bool AssignSpecialist(Pawn pawn, SpecialistRoleDef role)
        {
            if (pawn is null) return false;

            if (role is object && SpecialistCount >= MaxSpecialists)
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

            SettlementSpecialist entry = new SettlementSpecialist(pawn, role);
            if (role is null)
                residents.Add(entry);
            else
                specialists.Add(entry);

            SpecialistRoster.Assign(pawn, role);
            pawn.SetFaction(FindFC.EmpireFaction);
            if (!pawn.IsWorldPawn())
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);

            LogSG.Message($"Assigned {pawn.LabelShort} as {role?.LabelCap ?? "Resident"} to {Settlement.Name}");
            InvalidateAll();
            return true;
        }

        public bool AssignGovernor(Pawn pawn, GovernorFocusDef focus)
        {
            if (pawn is null || focus is null) return false;

            if (governor is object)
            {
                LogSG.Warning("Settlement already has a governor");
                return false;
            }

            governor = new SettlementGovernor(pawn, focus);
            SpecialistRoster.AssignGovernor(pawn);

            pawn.SetFaction(FindFC.EmpireFaction);
            if (!pawn.IsWorldPawn())
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);

            if (governor.Behavior is object)
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
            if (governor.Behavior is object)
                governor.Behavior.OnFocusDeactivated(Settlement);
            SpecialistRoster.Recall(pawn);
            governor = null;
            if (pawn is object)
                DeliverRecalledPawn(pawn);
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
            if (governor is object)
            {
                Pawn oldGovPawn = governor.pawn;
                if (governor.Behavior is object)
                    governor.Behavior.OnFocusDeactivated(Settlement);
                SpecialistRoster.Recall(oldGovPawn);

                SettlementSpecialist demoted = new SettlementSpecialist(oldGovPawn, null);
                residents.Add(demoted);
                SpecialistRoster.Assign(oldGovPawn, null);
            }

            // Update roster tracking
            SpecialistRoster.Recall(pawn);
            SpecialistRoster.AssignGovernor(pawn);

            // Create new governor
            governor = new SettlementGovernor(pawn, focus);
            if (governor.Behavior is object)
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
            if (newRole is object && entry.role is null && SpecialistCount >= MaxSpecialists)
            {
                Messages.Message("FCS_CannotAssignSpecialistRole".Translate(
                    entry.pawn.LabelShort, newRole.LabelCap, Settlement.Name, MaxSpecialists),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            // Moving between specialist and resident lists
            if (entry.role is null && newRole is object)
            {
                residents.Remove(entry);
                specialists.Add(entry);
            }
            else if (entry.role is object && newRole is null)
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
            if (entry.pawn is object)
                SpecialistRoster.Recall(entry.pawn);
            ResetSatisfactionIfRosterEmpty();
            InvalidateAll();
        }

        public List<Pawn> AllPawnsSnapshot()
        {
            List<Pawn> result = new List<Pawn>();
            foreach (SettlementSpecialist s in specialists)
                if (s.IsAlive) result.Add(s.pawn);
            foreach (SettlementSpecialist r in residents)
                if (r.IsAlive) result.Add(r.pawn);
            if (governor is object && governor.IsAlive)
                result.Add(governor.pawn);
            return result;
        }

        private void DeliverRecalledPawn(Pawn pawn)
        {
            if (pawn is null) return;
            DeliverPawnsToPlayer(new List<Pawn> { pawn });
        }

        /// <summary>
        /// Return recalled/released pawns to the player, layer-aware: a caravan at the settlement tile
        /// on caravan-capable layers (surface), or a drop pod to the player's home colony on layers
        /// that can't form caravans (orbit) so the pawns are never stranded. The pawns are normalized
        /// to player-faction world pawns first; DropThingsNear -> MakeDropPodAt then removes each from
        /// WorldPawns on landing, releasing the KeepForever hold.
        /// </summary>
        private void DeliverPawnsToPlayer(List<Pawn> pawns)
        {
            if (pawns is null || pawns.Count == 0) return;

            foreach (Pawn pawn in pawns)
            {
                if (pawn.Spawned) pawn.DeSpawn();
                pawn.SetFaction(Faction.OfPlayer);
                if (!pawn.IsWorldPawn())
                    Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }

            if (Settlement.Tile.LayerDef.canFormCaravans)
            {
                CaravanMaker.MakeCaravan(pawns, Faction.OfPlayer, Settlement.Tile, false);
                return;
            }

            // Non-caravan layer (orbit): drop-pod to the player's home colony, with a letter since the
            // pawns land off-screen.
            Map map = FindFC.TaxMap;
            if (map is object)
            {
                IntVec3 cell = DropCellFinder.TradeDropSpot(map);
                DropPodUtility.DropThingsNear(cell, map, pawns, 110, false, false, false, false);
                Find.LetterStack.ReceiveLetter(
                    "FCS_LetterRecallDropPodLabel".Translate(),
                    "FCS_LetterRecallDropPodText".Translate(pawns.Count, Settlement.Name, map.Parent.Label),
                    LetterDefOf.PositiveEvent);
                return;
            }

            // No player home map at all: last-resort caravan so the pawns are never lost.
            LogSG.Warning($"DeliverPawnsToPlayer: no player home map; forming caravan at {Settlement.Name} tile as last resort");
            CaravanMaker.MakeCaravan(pawns, Faction.OfPlayer, Settlement.Tile, false);
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
            List<Pawn> survivors = new List<Pawn>();
            foreach (SettlementSpecialist s in specialists)
            {
                if (s.pawn is null) continue;
                SpecialistRoster.Recall(s.pawn);
                if (s.IsAlive) survivors.Add(s.pawn);
            }
            foreach (SettlementSpecialist r in residents)
            {
                if (r.pawn is null) continue;
                SpecialistRoster.Recall(r.pawn);
                if (r.IsAlive) survivors.Add(r.pawn);
            }
            if (governor is object)
            {
                if (governor.Behavior is object)
                    governor.Behavior.OnFocusDeactivated(Settlement);
                if (governor.pawn is object)
                {
                    SpecialistRoster.Recall(governor.pawn);
                    if (governor.IsAlive) survivors.Add(governor.pawn);
                }
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
            foreach (SettlementSpecialist s in specialists) s.DirtySkillScore();
            foreach (SettlementSpecialist r in residents) r.DirtySkillScore();
            if (governor is object) governor.DirtySkillScore();
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
                if (pawn.IsWorldPawn())
                    Find.WorldPawns.RemovePawn(pawn);

                if (pawn.Faction != FindFC.EmpireFaction)
                    pawn.SetFaction(FindFC.EmpireFaction);

                IntVec3 loc = CellFinder.RandomClosewalkCellNear(map.Center, map, 15);
                GenSpawn.Spawn(pawn, loc, map);
                if (pawn.drafter is null)
                    pawn.drafter = new Pawn_DraftController(pawn);
                map.mapPawns.RegisterPawn(pawn);

                defenseLord.AddPawn(pawn);
                deployedPawns.Add(pawn);
            }

            pawnsDeployedToBattle = true;
            LogSG.Message("Deployed " + deployedPawns.Count + " specialists to defend " + Settlement.Name);
        }

        public void RecoverFromBattle()
        {
            if (!pawnsDeployedToBattle) return;

            // Deaths during the on-map battle were already handled the moment each pawn was killed
            // (Patch_Kill_SpecialistDeath -> NotifyMemberDied), so any roster entries still present
            // here are survivors. Just despawn them back to the world.
            // A drop pod launched before the attack can complete in-flight during the manual battle,
            // making the arriving member already a world pawn; guard PassToWorld (as the assign paths
            // do) so vanilla doesn't log an "already here" error per such pawn.
            foreach (SettlementSpecialist s in specialists)
            {
                if (s.pawn is null || s.pawn.Dead) continue;
                if (s.pawn.Spawned) s.pawn.DeSpawn();
                s.pawn.SetFaction(FindFC.EmpireFaction);
                if (!s.pawn.IsWorldPawn())
                    Find.WorldPawns.PassToWorld(s.pawn, PawnDiscardDecideMode.KeepForever);
            }
            foreach (SettlementSpecialist r in residents)
            {
                if (r.pawn is null || r.pawn.Dead) continue;
                if (r.pawn.Spawned) r.pawn.DeSpawn();
                r.pawn.SetFaction(FindFC.EmpireFaction);
                if (!r.pawn.IsWorldPawn())
                    Find.WorldPawns.PassToWorld(r.pawn, PawnDiscardDecideMode.KeepForever);
            }
            if (governor is object && governor.pawn is object && !governor.pawn.Dead)
            {
                if (governor.pawn.Spawned) governor.pawn.DeSpawn();
                governor.pawn.SetFaction(FindFC.EmpireFaction);
                if (!governor.pawn.IsWorldPawn())
                    Find.WorldPawns.PassToWorld(governor.pawn, PawnDiscardDecideMode.KeepForever);
            }

            deployedPawns.Clear();
            pawnsDeployedToBattle = false;
            InvalidateAll();
            LogSG.Message("Recovered specialists from battle at " + Settlement.Name);
        }

        /* Single funnel for every roster-member death, regardless of cause. Removes the member from
           the roster and sends exactly one death letter (wording chosen by cause). Idempotent: a
           second call for an already-removed pawn is a no-op. causeText, when supplied, is the
           base-game cause-of-death sentence appended on a new line. */
        public void NotifyMemberDied(Pawn pawn, SpecDeathCause cause, string causeText = null)
        {
            if (pawn is null) return;

            string roleLabel;

            SettlementSpecialist spec = specialists.Find(s => s.pawn == pawn);
            if (spec is object)
            {
                roleLabel = spec.role?.LabelCap ?? "Specialist";
                RemoveSpecialist(spec);
            }
            else
            {
                SettlementSpecialist res = residents.Find(r => r.pawn == pawn);
                if (res is object)
                {
                    roleLabel = "FCS_RoleResident".Translate();
                    RemoveSpecialist(res);
                }
                else if (governor is object && governor.pawn == pawn)
                {
                    roleLabel = "FCS_RoleGovernor".Translate();
                    governor.Behavior?.OnFocusDeactivated(Settlement);
                    SpecialistRoster.Recall(pawn);
                    governor = null;
                    ResetSatisfactionIfRosterEmpty();
                    InvalidateAll();
                }
                else
                {
                    return; // not a member of this settlement (or already removed)
                }
            }

            string body;
            switch (cause)
            {
                case SpecDeathCause.AutoBattle:
                    body = "FCS_LetterDeathAttack".Translate(pawn.LabelShort, roleLabel, Settlement.Name);
                    break;
                case SpecDeathCause.ManualBattle:
                    body = "FCS_LetterDeathDefending".Translate(pawn.LabelShort, roleLabel, Settlement.Name);
                    break;
                default:
                    body = "FCS_LetterDeathGeneric".Translate(pawn.LabelShort, roleLabel, Settlement.Name);
                    break;
            }
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
                if (specialists is null) specialists = new List<SettlementSpecialist>();
                if (residents is null) residents = new List<SettlementSpecialist>();
                if (deployedPawns is null) deployedPawns = new List<Pawn>();
                specialists.RemoveAll(s => s?.pawn is null);
                residents.RemoveAll(r => r?.pawn is null);
                deployedPawns.RemoveAll(p => p is null);
                if (governor is object && governor.pawn is null) governor = null;
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
                if (!s.HasUsableSkills || s.role is null || s.role.skillWeights is null) continue;
                foreach (SkillWeight sw in s.role.skillWeights)
                {
                    if (sw.skill is null) continue;
                    SkillRecord rec = s.pawn.skills.GetSkill(sw.skill);
                    if (rec is object && !rec.TotallyDisabled)
                        rec.Learn(xp * sw.weight, true);
                }
            }

            if (governor is object && governor.HasUsableSkills && governor.focus?.skillWeights is object)
            {
                foreach (SkillWeight sw in governor.focus.skillWeights)
                {
                    if (sw.skill is null) continue;
                    SkillRecord rec = governor.pawn.skills.GetSkill(sw.skill);
                    if (rec is object && !rec.TotallyDisabled)
                        rec.Learn(xp * sw.weight, true);
                }
                // Governor always gets Social XP
                SkillRecord social = governor.pawn.skills.GetSkill(SkillDefOf.Social);
                if (social is object && !social.TotallyDisabled)
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

        private SpecialistsTabRenderer TabRenderer =>
            tabRenderer ?? (tabRenderer = new SpecialistsTabRenderer(this));

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

            float satisfaction = foodSatisfaction * medicineSatisfaction;
            return total * satisfaction;
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            if (governor is null || !governor.HasUsableSkills) return 1.0;
            float satisfaction = foodSatisfaction * medicineSatisfaction;
            double rawMult = SpecUtil.GovernorMultiplierForResource(governor, resource.def);
            return 1.0 + (rawMult - 1.0) * satisfaction;
        }

        public string GetResourceAdditiveDesc(ResourceFC resource)
        {
            StringBuilder sb = new StringBuilder();
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
            float satisfaction = foodSatisfaction * medicineSatisfaction;
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
            float satisfaction = foodSatisfaction * medicineSatisfaction;
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
            float satisfaction = foodSatisfaction * medicineSatisfaction;
            foreach (SettlementSpecialist s in specialists)
            {
                value += SpecUtil.SpecialistStatBonus(s, stat) * satisfaction;
            }

            // Governor stat multiplier
            if (governor is object && governor.HasUsableSkills)
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
            StringBuilder sb = new StringBuilder();
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
            if (governor is object && governor.HasUsableSkills)
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
            float satisfaction = foodSatisfaction * medicineSatisfaction;
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
            if (governor is object)
                total += SpecUtil.GovernorUpkeep(governor);
            return total;
        }

        public string GetDailyUpkeepContributionDesc()
        {
            double total = GetDailyUpkeepContribution();
            if (total <= 0) return null;
            return $"+{Math.Round(total, 2)} - {"FCS_UpkeepSettlementLine".Translate()}";
        }

        public double GetDailyIncomeContribution() { return 0; }
        public string GetDailyIncomeContributionDesc() { return null; }
    }
}
