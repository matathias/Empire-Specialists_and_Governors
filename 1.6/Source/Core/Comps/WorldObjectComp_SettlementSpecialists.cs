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

        public void AssignSpecialist(Pawn pawn, SpecialistRoleDef role)
        {
            if (pawn is null) return;

            if (role is object && SpecialistCount >= MaxSpecialists)
            {
                LogSG.Message($"Cannot assign {pawn.LabelShort}: max specialists reached at {Settlement.Name}");
                Messages.Message("FCS_CannotAssignSpecialistRole".Translate(
                    pawn.LabelShort, role.LabelCap, Settlement.Name, MaxSpecialists),
                    MessageTypeDefOf.RejectInput);
                return;
            }

            if (role is object && role.maxPerSettlement > 0)
            {
                int existing = 0;
                foreach (SettlementSpecialist s in specialists)
                    if (s.role == role) existing++;
                if (existing >= role.maxPerSettlement)
                {
                    LogSG.Message($"Cannot assign {role.defName}: max per settlement reached");
                    Messages.Message("FCS_CannotAssignSpecialistRole".Translate(
                        pawn.LabelShort, role.LabelCap, Settlement.Name, role.maxPerSettlement),
                        MessageTypeDefOf.RejectInput);
                    return;
                }
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
        }

        public void AssignGovernor(Pawn pawn, GovernorFocusDef focus)
        {
            if (pawn is null || focus is null) return;

            if (governor is object)
            {
                LogSG.Warning("Settlement already has a governor");
                return;
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
        }

        public void RecallSpecialist(SettlementSpecialist entry)
        {
            if (entry?.pawn is null) return;
            Pawn pawn = entry.pawn;
            specialists.Remove(entry);
            residents.Remove(entry);
            SpecialistRoster.Recall(pawn);
            SpawnPawnInCaravan(pawn);
            LogSG.Message($"Recalled {pawn.LabelShort} from {Settlement.Name}");
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
                SpawnPawnInCaravan(pawn);
            LogSG.Message($"Recalled governor from {Settlement.Name}");
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

        private void SpawnPawnInCaravan(Pawn pawn)
        {
            if (pawn is null) return;
            pawn.SetFaction(Faction.OfPlayer);
            if (pawn.IsWorldPawn())
                Find.WorldPawns.RemovePawn(pawn);
            CaravanMaker.MakeCaravan(
                new List<Pawn> { pawn },
                Faction.OfPlayer,
                Settlement.Tile,
                false);
        }

        private void InvalidateAll()
        {
            foreach (SettlementSpecialist s in specialists) s.DirtySkillScore();
            foreach (SettlementSpecialist r in residents) r.DirtySkillScore();
            if (governor is object) governor.DirtySkillScore();
            Settlement.InvalidateStatCache();
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

            // Collect dead specialists
            List<SettlementSpecialist> deadSpecs = new List<SettlementSpecialist>();
            foreach (SettlementSpecialist s in specialists)
            {
                if (s.pawn is null || s.pawn.Dead)
                    deadSpecs.Add(s);
            }
            List<SettlementSpecialist> deadResidents = new List<SettlementSpecialist>();
            foreach (SettlementSpecialist r in residents)
            {
                if (r.pawn is null || r.pawn.Dead)
                    deadResidents.Add(r);
            }
            bool governorDead = governor is object && (governor.pawn is null || governor.pawn.Dead);

            // Despawn surviving pawns back to world
            foreach (SettlementSpecialist s in specialists)
            {
                if (s.pawn is null || s.pawn.Dead) continue;
                if (s.pawn.Spawned) s.pawn.DeSpawn();
                s.pawn.SetFaction(FindFC.EmpireFaction);
                Find.WorldPawns.PassToWorld(s.pawn, PawnDiscardDecideMode.KeepForever);
            }
            foreach (SettlementSpecialist r in residents)
            {
                if (r.pawn is null || r.pawn.Dead) continue;
                if (r.pawn.Spawned) r.pawn.DeSpawn();
                r.pawn.SetFaction(FindFC.EmpireFaction);
                Find.WorldPawns.PassToWorld(r.pawn, PawnDiscardDecideMode.KeepForever);
            }
            if (governor is object && !governorDead)
            {
                if (governor.pawn.Spawned) governor.pawn.DeSpawn();
                governor.pawn.SetFaction(FindFC.EmpireFaction);
                Find.WorldPawns.PassToWorld(governor.pawn, PawnDiscardDecideMode.KeepForever);
            }

            // Process deaths
            foreach (SettlementSpecialist s in deadSpecs)
            {
                Pawn pawn = s.pawn;
                string roleLabel = s.role?.LabelCap ?? "Specialist";
                RemoveSpecialist(s);
                if (pawn is null) continue;
                SpecUtil.SendDeathLetter(roleLabel,
                    "FCS_LetterDeathDefending".Translate(pawn.LabelShort, roleLabel, Settlement.Name));
            }
            foreach (SettlementSpecialist r in deadResidents)
            {
                Pawn pawn = r.pawn;
                RemoveSpecialist(r);
                if (pawn is null) continue;
                SpecUtil.SendDeathLetter("FCS_RoleResident".Translate(),
                    "FCS_LetterDeathDefending".Translate(pawn.LabelShort, "FCS_RoleResident".Translate(), Settlement.Name));
            }
            if (governorDead)
            {
                Pawn pawn = governor.pawn;
                SpecialistRoster.Recall(pawn);
                governor = null;
                if (pawn is object)
                    SpecUtil.SendDeathLetter("FCS_RoleGovernor".Translate(),
                        "FCS_LetterDeathDefending".Translate(pawn.LabelShort, "FCS_RoleGovernor".Translate(), Settlement.Name));
            }

            deployedPawns.Clear();
            pawnsDeployedToBattle = false;
            InvalidateAll();
            LogSG.Message("Recovered specialists from battle at " + Settlement.Name);
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
            foreach (SettlementSpecialist s in specialists)
            {
                double bonus = SpecUtil.SpecialistAdditiveForResource(s, resource.def);
                if (Math.Abs(bonus) > 0.001)
                    sb.AppendLine(TextUtil.ColorizeAdditiveBonus(bonus) + " - "
                        + s.pawn.LabelShort + " (" + s.role.LabelCap + ")");
            }
            string result = sb.ToString().TrimEnd();
            return result.Length > 0 ? result : null;
        }

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            if (governor is null || !governor.HasUsableSkills) return null;
            double mult = SpecUtil.GovernorMultiplierForResource(governor, resource.def);
            if (Math.Abs(mult - 1.0) < 0.001) return null;
            return TextUtil.ColorizeMultiplierBonus(mult) + " - "
                + governor.pawn.LabelShort + " (" + governor.focus.LabelCap + ")";
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

            return value;
        }

        public string GetStatModifierDesc(FCStatDef stat)
        {
            StringBuilder sb = new StringBuilder();

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
                    sb.Append($"+{Math.Round(statBonus, 2)} - {s.pawn.LabelShort} ({s.role.LabelCap})");
                }
            }

            if (stat == FCStatDefOf.mercHealRateMultiplier)
            {
                double healBonus = SpecUtil.MedicalHealRateBonus(specialists, governor);
                if (healBonus > 0)
                {
                    if (sb.Length > 0) sb.Append("\n");
                    sb.Append("FCS_HealRateLine".Translate(Math.Round(healBonus, 2)));
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        // ── IProfitContributor ──

        public double GetUpkeepContribution()
        {
            double total = 0;
            foreach (SettlementSpecialist s in specialists)
                total += SpecUtil.SpecialistUpkeep(s);
            if (governor is object)
                total += SpecUtil.GovernorUpkeep();
            return total;
        }

        public string GetUpkeepContributionDesc()
        {
            double total = GetUpkeepContribution();
            if (total <= 0) return null;
            return $"+{Math.Round(total, 2)} - {"FCS_UpkeepSettlementLine".Translate()}";
        }

        public double GetIncomeContribution() { return 0; }
        public string GetIncomeContributionDesc() { return null; }
    }
}
