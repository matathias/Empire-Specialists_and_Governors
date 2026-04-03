using System;
using System.Collections.Generic;
using System.Linq;
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
        private List<SpecialistFC> allPawns = new List<SpecialistFC>();

        // Supply chain integration: satisfaction from resource needs.
        // Defaults to 1.0 (full satisfaction) when supply chain submod is not loaded.
        // Written by the compat bridge comp (WorldObjectComp_SpecialistNeeds).

        public float FoodSatisfaction { get; set; } = 1f;

        public float MedicineSatisfaction { get; set; } = 1f;

        public IEnumerable<SpecialistFC> Residents
        {
            get { return allPawns.Where(s => s.role == SpecialistRole.Resident); }
        }

        public IEnumerable<SpecialistFC> CivilianSpecialists
        {
            get { return allPawns.Where(s => s.role == SpecialistRole.Specialist); }
        }

        public IEnumerable<SpecialistFC> DefenseSpecialists
        {
            get { return allPawns.Where(s => s.role == SpecialistRole.Defense); }
        }

        public SpecialistFC Governor
        {
            get { return allPawns.FirstOrDefault(s => s.role == SpecialistRole.Governor); }
        }

        public int SpecialistCount
        {
            get { return allPawns.Count(s => s.role.IsRegularSpecialist()); }
        }

        public int CivilianSpecialistCount
        {
            get { return allPawns.Count(s => s.role == SpecialistRole.Specialist); }
        }

        public int DefenseSpecialistCount
        {
            get { return allPawns.Count(s => s.role == SpecialistRole.Defense); }
        }

        public int ResidentCount
        {
            get { return allPawns.Count(s => s.role == SpecialistRole.Resident); }
        }

        public int LiveResidentCount
        {
            get
            {
                int count = 0;
                foreach (SpecialistFC s in allPawns)
                    if (s.role == SpecialistRole.Resident && s.IsAlive)
                        count++;
                return count;
            }
        }

        public bool HasGovernor
        {
            get { return allPawns.Any(s => s.role == SpecialistRole.Governor); }
        }

        public int TotalCount => allPawns.Count;

        public WorldSettlementFC Settlement => (WorldSettlementFC)parent;

        // --- Trait/Policy helpers ---

        public int MaxSpecialists
        {
            get
            {
                int baseMax = FCSSettings.specialistBaseMax + (int)Math.Floor(Settlement.settlementLevel / (double)FCSSettings.specialistPerLevels);
                if (SpecUtil.HasTrait(SpecPolicyDefOf.FCSspecialistCorps)) baseMax += 2;
                return baseMax;
            }
        }

        internal List<SpecialistFC> AllPawnsInternal => allPawns;

        // --- Core roster operations ---

        public void AssignPawn(Pawn pawn, SpecialistRole role)
        {
            if (pawn == null) return;

            if (role.IsRegularSpecialist() && SpecialistCount >= MaxSpecialists)
            {
                LogSG.Message($"Cannot assign {pawn.LabelShort}: max specialists reached at {Settlement.Name}");
                Messages.Message("FCS_CannotAssignSpecialistRole".Translate(pawn.LabelShort, role.Translate(), Settlement.Name, MaxSpecialists), MessageTypeDefOf.RejectInput);
                return;
            }

            if (role == SpecialistRole.Governor && Governor != null)
            {
                LogSG.Message("Settlement already has a governor. Demoting existing governor to Specialist.");
                ChangeRole(Governor, SpecialistRole.Specialist);
            }

            SpecialistFC specialist = new SpecialistFC(pawn, role);
            allPawns.Add(specialist);
            SpecialistRoster.Assign(pawn, role);

            pawn.SetFaction(FactionCache.PlayerColonyFaction);
            if (!pawn.IsWorldPawn())
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }

            LogSG.Message($"Assigned {pawn.LabelShort} as {role} to {Settlement.Name}");
            Settlement.InvalidateStatCache();
        }

        public void RecallPawn(SpecialistFC specialist)
        {
            if (specialist?.pawn is null) return;

            Pawn pawn = specialist.pawn;
            allPawns.Remove(specialist);
            SpecialistRoster.Recall(pawn);

            pawn.SetFaction(Faction.OfPlayer);

            if (pawn.IsWorldPawn())
            {
                Find.WorldPawns.RemovePawn(pawn);
            }

            CaravanMaker.MakeCaravan(
                new List<Pawn> { pawn },
                Faction.OfPlayer,
                Settlement.Tile,
                false
            );

            LogSG.Message($"Recalled {pawn.LabelShort} from {Settlement.Name}");
            Settlement.InvalidateStatCache();
        }

        public void ChangeRole(SpecialistFC specialist, SpecialistRole newRole, bool ignoreLimit = false)
        {
            if (specialist is null) return;

            // If the specialist's current role isn't max limited, but its target role is, and we're at the max, then reject the role change
            if (!ignoreLimit && newRole.IsRegularSpecialist() && !specialist.role.IsRegularSpecialist() && SpecialistCount >= MaxSpecialists)
            {
                LogSG.Message($"Cannot assign {specialist.pawn.LabelShort}: max specialists reached at {Settlement.Name}");
                Messages.Message("FCS_CannotAssignSpecialistRole".Translate(specialist.pawn.LabelShort, newRole.Translate(), Settlement.Name, MaxSpecialists), MessageTypeDefOf.RejectInput);
                return;
            }

            if (newRole == SpecialistRole.Governor && Governor != null && Governor != specialist)
            {
                LogSG.Message("Settlement already has a governor. Demoting existing governor to Specialist.");
                // When we demote the old governor, we haven't yet promoted the new governor, so we might technically go over the max limit of specialists
                //   This is only a temporary result of the state change, though, so in this specific instant, we want to ignore the max limit
                ChangeRole(Governor, SpecialistRole.Specialist, true);
            }

            if (specialist.role == SpecialistRole.Governor && newRole != SpecialistRole.Governor)
            {
                specialist.governorFocuses.Clear();
            }

            specialist.role = newRole;
            SpecialistRoster.Assign(specialist.pawn, newRole);
            Settlement.InvalidateStatCache();
        }

        public void RemoveDeadPawns()
        {
            int removed = allPawns.RemoveAll(s =>
            {
                if (!s.IsAlive)
                {
                    SpecialistRoster.Recall(s.pawn);
                    return true;
                }
                return false;
            });
            if (removed > 0)
            {
                Settlement.InvalidateStatCache();
            }
        }

        // --- Manual Battle Integration ---

        private bool pawnsDeployedToBattle = false;
        public bool PawnsDeployedToBattle => pawnsDeployedToBattle;
        private List<Pawn> deployedPawns = new List<Pawn>();

        public void DeployToBattle(Map map, List<Pawn> defenders, Lord defenseLord)
        {
            if (pawnsDeployedToBattle) return;
            deployedPawns.Clear();

            foreach (SpecialistFC s in allPawns)
            {
                Pawn pawn = s.pawn;
                if (!s.IsAlive) continue;

                if (pawn.IsWorldPawn())
                {
                    Find.WorldPawns.RemovePawn(pawn);
                }

                if (pawn.Faction != FactionCache.PlayerColonyFaction)
                {
                    pawn.SetFaction(FactionCache.PlayerColonyFaction);
                }

                IntVec3 loc = CellFinder.RandomClosewalkCellNear(map.Center, map, 15);
                GenSpawn.Spawn(pawn, loc, map);
                if (pawn.drafter == null)
                {
                    pawn.drafter = new Pawn_DraftController(pawn);
                }
                map.mapPawns.RegisterPawn(pawn);

                defenders.Add(pawn);
                defenseLord.AddPawn(pawn);
                deployedPawns.Add(pawn);
            }

            pawnsDeployedToBattle = true;
            LogSG.Message("Deployed " + deployedPawns.Count + " specialists to defend " + Settlement.Name);
        }

        public void RecoverFromBattle()
        {
            if (!pawnsDeployedToBattle) return;

            List<SpecialistFC> dead = new List<SpecialistFC>();

            foreach (SpecialistFC s in allPawns)
            {
                Pawn pawn = s.pawn;
                if (pawn == null) continue;

                if (pawn.Dead)
                {
                    dead.Add(s);
                    continue;
                }

                if (pawn.Spawned)
                {
                    pawn.DeSpawn();
                }

                pawn.SetFaction(FactionCache.PlayerColonyFaction);
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }

            foreach (SpecialistFC s in dead)
            {
                SpecialistRole role = s.role;
                Pawn pawn = s.pawn;
                allPawns.Remove(s);
                SpecialistRoster.Recall(pawn);

                SpecUtil.SendDeathLetter(role,
                    "FCS_LetterDeathDefending".Translate(pawn.LabelShort, role.Translate(), Settlement.Name));
            }

            WorldObjectComp_SettlementMilitary milComp =
                Settlement.GetComponent<WorldObjectComp_SettlementMilitary>();
            if (milComp != null)
            {
                foreach (Pawn p in deployedPawns)
                {
                    milComp.defenders.Remove(p);
                }
            }

            deployedPawns.Clear();
            pawnsDeployedToBattle = false;
            Settlement.InvalidateStatCache();
            LogSG.Message("Recovered specialists from battle at " + Settlement.Name);
        }

        // --- Serialization ---

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref allPawns, "allPawns", LookMode.Deep);
            if (allPawns == null)
            {
                allPawns = new List<SpecialistFC>();
            }
            Scribe_Values.Look(ref pawnsDeployedToBattle, "pawnsDeployedToBattle", false);
            Scribe_Collections.Look(ref deployedPawns, "deployedPawns", LookMode.Reference);
            if (deployedPawns == null)
            {
                deployedPawns = new List<Pawn>();
            }
        }

        // --- XP Ticking ---

        public override void CompTick()
        {
            base.CompTick();
            if (Find.TickManager.TicksGame % GenDate.TicksPerDay != 0) return;

            foreach (SpecialistFC s in allPawns)
            {
                if (s.role == SpecialistRole.Resident) continue;
                if (!s.HasUsableSkills) continue;

                float xp = SpecUtil.XPPerDay();
                foreach (SkillDef skill in GetRelevantSkills(s))
                {
                    SkillRecord rec = s.pawn.skills.GetSkill(skill);
                    if (rec != null && !rec.TotallyDisabled)
                    {
                        rec.Learn(xp, true);
                    }
                }
            }
        }

        private IEnumerable<SkillDef> GetRelevantSkills(SpecialistFC s)
        {
            if (s.role == SpecialistRole.Defense)
            {
                yield return SkillDefOf.Melee;
                yield return SkillDefOf.Shooting;
                yield break;
            }

            HashSet<SkillDef> seen = new HashSet<SkillDef>();
            foreach (ResourceFC resource in Settlement.Resources)
            {
                if (resource.def.associatedSkills is null) continue;
                foreach (SkillDef skill in resource.def.associatedSkills)
                {
                    if (SpecialistsCache.SkillWeight(skill) != null && seen.Add(skill))
                    {
                        yield return skill;
                    }
                }
            }

            if (s.role == SpecialistRole.Governor && seen.Add(SkillDefOf.Social))
            {
                yield return SkillDefOf.Social;
            }
        }

        // --- Roster helpers ---

        public List<SpecialistFC> AllPawnsSnapshot()
        {
            return new List<SpecialistFC>(allPawns);
        }

        public void RemoveSpecialist(SpecialistFC s)
        {
            allPawns.Remove(s);
            SpecialistRoster.Recall(s.pawn);
            Settlement.InvalidateStatCache();
        }

        // --- Caravan gizmos ---

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

        // --- ISettlementWindowOverview ---

        private SpecialistsTabRenderer tabRenderer;

        private SpecialistsTabRenderer TabRenderer => tabRenderer ?? (tabRenderer = new SpecialistsTabRenderer(this));

        public void PreOpenWindow(WorldSettlementFC settlement) { TabRenderer.PreOpenWindow(settlement); }
        public void OnTabSwitch() { TabRenderer.OnTabSwitch(); }

        public void DrawOverviewTab(Rect boundingBox)
        {
            SpecialistFC toRecall = TabRenderer.DrawOverviewTab(boundingBox);
            if (toRecall != null)
                RecallPawn(toRecall);
        }

        public void PostCloseWindow() { TabRenderer.PostCloseWindow(); }
        public string OverviewTabName() => TabRenderer.OverviewTabName();

        public void SetGovernorFocus(SpecialistFC gov, ResourceTypeDef def, int slot = 0)
        {
            if (gov is null || gov.role != SpecialistRole.Governor) return;
            while (gov.governorFocuses.Count <= slot)
            {
                gov.governorFocuses.Add(null);
            }
            gov.governorFocuses[slot] = def;
            Settlement.InvalidateStatCache();
        }

        // --- Upkeep ---

        public double CalculateTotalUpkeep()
        {
            double total = 0;
            foreach (SpecialistFC s in allPawns)
            {
                total += CalculatePawnUpkeep(s);
            }
            return total;
        }
        /// <summary>
        /// Calculates the upkeep of regular Specialists only, excluding Governors. Includes Defense Specialists.
        /// </summary>
        /// <returns></returns>
        public double CalculateSpecialistUpkeep()
        {
            double total = 0;
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role.IsRegularSpecialist())
                    total += CalculatePawnUpkeep(s);
            }
            return total;
        }

        public double CalculatePawnUpkeep(SpecialistFC s)
        {
            if (s.pawn is null || s.role == SpecialistRole.Resident) return 0;
            double skillSum = SpecUtil.PawnSkillSum(s.pawn);
            double upkeep = SpecUtil.BaseUpkeep(skillSum);
            if (s.role == SpecialistRole.Governor)
            {
                upkeep *= SpecUtil.GovernorUpkeepMultiplier();
            }
            return upkeep;
        }

        // --- IUpkeepContributor ---

        public double GetUpkeepContribution()
        {
            return CalculateTotalUpkeep();
        }

        public string GetUpkeepContributionDesc()
        {
            double total = CalculateTotalUpkeep();
            if (total <= 0) return null;
            return $"+{Math.Round(total, 2)} - {"FCS_UpkeepSettlementLine".Translate()}";
        }
        // stub for the interface
        public double GetIncomeContribution()
        {
            return 0;
        }
        // stub for the interface
        public string GetIncomeContributionDesc()
        {
            return null;
        }

        // --- IStatModifierProvider ---

        public double GetStatModifier(FCStatDef stat)
        {
            double value = stat.IdentityValue;

            // Residents contribute bonus workers
            if (stat == FCStatDefOf.workerBaseMax)
            {
                value += SpecUtil.WorkerBonusFromResidents(LiveResidentCount);
            }

            // SpecialistStatEffectDefs: skill -> stat contributions
            bool profArmy = SpecUtil.HasTrait(SpecPolicyDefOf.FCSprofessionalArmy);
            bool garrison = SpecUtil.HasTrait(SpecPolicyDefOf.FCSgarrisonDoctrine);
            List<SpecialistStatEffectDef> effects = SpecialistsCache.StatEffectsForStat(stat);
            if (effects?.Count > 0)
            {
                foreach (SpecialistStatEffectDef def in effects)
                {
                    foreach (SpecialistFC s in allPawns)
                    {
                        if (!s.HasUsableSkills) continue;
                        if (s.role != def.roleFilter) continue;
                        SkillRecord skill = s.pawn.skills.GetSkill(def.skill);
                        if (skill != null)
                        {
                            double contribution = skill.Level * def.specialistValuePerLevel;
                            // Professional Army: defense specialists contribute 2x military level
                            if (profArmy && s.role == SpecialistRole.Defense && stat == FCStatDefOf.militaryBaseLevel)
                            {
                                contribution *= 2.0;
                            }
                            value += contribution;
                        }
                    }
                }
            }

            // Garrison Doctrine: defense specialists contribute to happiness
            if (garrison && stat == FCStatDefOf.happinessGainedBase)
            {
                foreach (SpecialistFC s in allPawns)
                {
                    if (s.role != SpecialistRole.Defense) continue;
                    if (!s.HasUsableSkills) continue;
                    double bonus = SpecUtil.GetMilBonus(s);
                    value += bonus;
                }
            }

            return value;
        }

        public string GetStatModifierDesc(FCStatDef stat)
        {
            StringBuilder sb = new StringBuilder();

            // Worker bonus from residents
            if (stat == FCStatDefOf.workerBaseMax)
            {
                int residentCount = LiveResidentCount;
                int bonus = SpecUtil.WorkerBonusFromResidents(residentCount);
                if (bonus > 0)
                {
                    sb.Append("FCS_StatWorkerBonus".Translate(bonus, residentCount));
                }
            }

            // Stat effect contributions
            List<SpecialistStatEffectDef> effects = SpecialistsCache.StatEffectsForStat(stat);
            if (effects != null)
            {
                foreach (SpecialistStatEffectDef def in effects)
                {
                    double total = 0;
                    List<string> parts = new List<string>();
                    foreach (SpecialistFC s in allPawns)
                    {
                        if (!s.HasUsableSkills) continue;
                        if (s.role != def.roleFilter) continue;
                        SkillRecord skill = s.pawn.skills.GetSkill(def.skill);
                        if (skill != null && skill.Level > 0)
                        {
                            double contribution = skill.Level * def.specialistValuePerLevel;
                            total += contribution;
                            parts.Add(s.pawn.LabelShort + " " + skill.Level);
                        }
                    }
                    if (total > 0)
                    {
                        total = Math.Round(total, 2);
                        if (sb.Length > 0) sb.Append("\n");
                        sb.Append("FCS_StatEffectLine".Translate(total, def.skill.skillLabel.CapitalizeFirst(), string.Join(", ", parts)));
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        // --- IResourceProductionModifier ---

        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            if (resource.def.associatedSkills is null) return 0;

            double bonus = 0;
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Specialist) continue;
                if (!s.HasUsableSkills) continue;
                bonus += SpecUtil.SpecialistAdditiveForResource(s.pawn, resource);
            }

            if (bonus > 0 && SpecUtil.HasTrait(SpecPolicyDefOf.FCSspecialistCorps))
            {
                bonus *= 1.2;
            }

            return bonus * FoodSatisfaction;
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            SpecialistFC gov = Governor;
            if (gov == null || !gov.HasUsableSkills)
                return 1.0;

            if (resource.def.associatedSkills is null)
                return 1.0;

            SkillRecord social = gov.pawn.skills.GetSkill(SkillDefOf.Social);
            double socialFactor = SpecUtil.GovSocialFactor(social?.Level ?? 0);

            double raw = SpecUtil.GovernorRawMultiplierForResource(gov.pawn, resource);

            bool isFocused = gov.HasFocus(resource.def);
            double focusBonus = isFocused ? SpecUtil.FocusBonusMultiplier() : 1.0;

            double multiplier = raw * socialFactor * focusBonus;
            return 1.0 + (multiplier * FoodSatisfaction);
        }

        public string GetResourceAdditiveDesc(ResourceFC resource)
        {
            if (resource.def.associatedSkills is null) return null;

            double addTotal = 0;
            List<string> addParts = new List<string>();
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Specialist) continue;
                if (!s.HasUsableSkills) continue;

                double pawnBonus = SpecUtil.SpecialistAdditiveForResource(s.pawn, resource);
                if (pawnBonus > 0)
                {
                    addTotal += pawnBonus;
                    // Find the best skill for the label
                    string bestSkillName = null;
                    int bestSkillLevel = 0;
                    foreach (SkillDef skillDef in resource.def.associatedSkills)
                    {
                        SkillRecord skill = s.pawn.skills.GetSkill(skillDef);
                        if (skill != null && skill.Level > bestSkillLevel)
                        {
                            bestSkillLevel = skill.Level;
                            bestSkillName = skillDef.skillLabel;
                        }
                    }
                    addParts.Add(s.pawn.LabelShort + " " + bestSkillName + " " + bestSkillLevel);
                }
            }
            if (addTotal > 0)
            {
                return $"{TextUtil.ColorizeAdditiveBonus(Math.Round(addTotal,2))} - {"FCS_ResSpecialists".Translate(string.Join(", ", addParts))}";
            }
            return null;
        }

        public string GetResourceMultiplierDesc(ResourceFC resource)
        {
            if (resource.def.associatedSkills is null) return null;

            SpecialistFC gov = Governor;
            if (gov is null || !gov.HasUsableSkills)
                return null;

            double mult = GetResourceMultiplierModifier(resource);
            if (Math.Abs(mult - 1.0) <= 0.001)
                return null;

            bool isFocused = gov.HasFocus(resource.def);
            string focusTag = isFocused ? "FCS_ResFocusTag".Translate().ToString() : "";
            SkillRecord social = gov.pawn.skills.GetSkill(SkillDefOf.Social);
            int socialLevel = social?.Level ?? 0;
            return $"{TextUtil.ColorizeMultiplierBonus(Math.Round(mult, 2))} - {"FCS_ResGovernor".Translate(gov.pawn.LabelShort, socialLevel, focusTag)}";
        }
    }
}
