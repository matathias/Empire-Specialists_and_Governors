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
        ISettlementWindowOverview, IStatModifierProvider, IResourceProductionModifier
    {
        private List<SpecialistFC> allPawns = new List<SpecialistFC>();

        // Supply chain integration: satisfaction from resource needs.
        // Defaults to 1.0 (full satisfaction) when supply chain submod is not loaded.
        // Written by the compat bridge comp (WorldObjectComp_SpecialistNeeds).
        private float foodSatisfaction = 1f;
        private float medicineSatisfaction = 1f;

        public float FoodSatisfaction
        {
            get { return foodSatisfaction; }
            set { foodSatisfaction = value; }
        }

        public float MedicineSatisfaction
        {
            get { return medicineSatisfaction; }
            set { medicineSatisfaction = value; }
        }

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
            get { return allPawns.Count(s => s.role != SpecialistRole.Resident); }
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

        public bool HasGovernor
        {
            get { return allPawns.Any(s => s.role == SpecialistRole.Governor); }
        }

        public int TotalCount
        {
            get { return allPawns.Count; }
        }

        public WorldSettlementFC Settlement
        {
            get { return (WorldSettlementFC)parent; }
        }

        // --- Trait/Policy helpers ---

        internal bool HasTrait(string defName)
        {
            FCPolicyDef def = SpecialistsCache.TraitDef(defName);
            return def != null && FactionCache.FactionComp.HasTrait(def);
        }

        public int MaxSpecialists
        {
            get
            {
                int baseMax = 5 + (int)Math.Floor(Settlement.settlementLevel / 3.0);
                if (HasTrait("specialistCorps")) baseMax += 2;
                return baseMax;
            }
        }

        internal List<SpecialistFC> AllPawnsInternal { get { return allPawns; } }

        // --- Core roster operations ---

        public void AssignPawn(Pawn pawn, SpecialistRole role)
        {
            if (pawn == null) return;

            if (role != SpecialistRole.Resident && SpecialistCount >= MaxSpecialists)
            {
                LogUtil.Warning("Cannot assign " + pawn.LabelShort + ": max specialists reached at " + Settlement.Name);
                return;
            }

            if (role == SpecialistRole.Governor && Governor != null)
            {
                LogUtil.Warning("Settlement already has a governor. Demoting existing governor to Specialist.");
                ChangeRole(Governor, SpecialistRole.Specialist);
            }

            SpecialistFC specialist = new SpecialistFC(pawn, role);
            allPawns.Add(specialist);

            pawn.SetFaction(FactionCache.PlayerColonyFaction);
            if (!pawn.IsWorldPawn())
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }

            LogUtil.Message("Assigned " + pawn.LabelShort + " as " + role + " to " + Settlement.Name);
            Settlement.InvalidateStatCache();
        }

        public void RecallPawn(SpecialistFC specialist)
        {
            if (specialist == null || specialist.pawn == null) return;

            Pawn pawn = specialist.pawn;
            allPawns.Remove(specialist);

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

            LogUtil.Message("Recalled " + pawn.LabelShort + " from " + Settlement.Name);
            Settlement.InvalidateStatCache();
        }

        public void ChangeRole(SpecialistFC specialist, SpecialistRole newRole)
        {
            if (specialist == null) return;

            if (newRole == SpecialistRole.Governor && Governor != null && Governor != specialist)
            {
                LogUtil.Warning("Settlement already has a governor. Demoting existing governor to Specialist.");
                ChangeRole(Governor, SpecialistRole.Specialist);
            }

            if (specialist.role == SpecialistRole.Governor && newRole != SpecialistRole.Governor)
            {
                specialist.governorFocuses.Clear();
            }

            specialist.role = newRole;
            Settlement.InvalidateStatCache();
        }

        public void RemoveDeadPawns()
        {
            int removed = allPawns.RemoveAll(s => s.pawn == null || s.pawn.Dead);
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
            bool ivoryTower = HasTrait("ivoryTower");

            foreach (SpecialistFC s in allPawns)
            {
                Pawn pawn = s.pawn;
                if (pawn == null || pawn.Dead) continue;

                // Ivory Tower: civilian specialists don't deploy
                if (ivoryTower && s.role == SpecialistRole.Specialist) continue;

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
            LogUtil.Message("Deployed " + deployedPawns.Count + " specialists to defend " + Settlement.Name);
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

                LetterDef letterDef = role == SpecialistRole.Governor
                    ? LetterDefOf.Death : LetterDefOf.NegativeEvent;
                string label = role == SpecialistRole.Governor
                    ? "FCS_LetterGovernorKilled".Translate() : "FCS_LetterSpecialistKilled".Translate();
                Find.LetterStack.ReceiveLetter(label,
                    "FCS_LetterDeathDefending".Translate(pawn.LabelShort, role.Translate(), Settlement.Name),
                    letterDef);
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
            LogUtil.Message("Recovered specialists from battle at " + Settlement.Name);
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
                if (s.pawn == null || s.pawn.Dead || s.pawn.skills == null) continue;

                float xp = FCSSettings.xpPerDay;
                if (HasTrait("specialistCorps")) xp *= 2f;
                else if (HasTrait("meritocratic")) xp *= 1.5f;
                if (s.role == SpecialistRole.Specialist && HasTrait("ivoryTower")) xp *= 3f;
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
                if (resource.def.associatedSkills == null) continue;
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

        private SpecialistsTabRenderer TabRenderer
        {
            get
            {
                if (tabRenderer == null)
                    tabRenderer = new SpecialistsTabRenderer(this);
                return tabRenderer;
            }
        }

        public void PreOpenWindow(WorldSettlementFC settlement) { TabRenderer.PreOpenWindow(settlement); }
        public void OnTabSwitch() { TabRenderer.OnTabSwitch(); }

        public void DrawOverviewTab(Rect boundingBox)
        {
            SpecialistFC toRecall = TabRenderer.DrawOverviewTab(boundingBox);
            if (toRecall != null)
                RecallPawn(toRecall);
        }

        public void PostCloseWindow() { TabRenderer.PostCloseWindow(); }
        public string OverviewTabName() { return TabRenderer.OverviewTabName(); }

        public void SetGovernorFocus(SpecialistFC gov, string focusDefName, int slot = 0)
        {
            if (gov == null || gov.role != SpecialistRole.Governor) return;
            while (gov.governorFocuses.Count <= slot)
            {
                gov.governorFocuses.Add(null);
            }
            gov.governorFocuses[slot] = focusDefName;
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

        public double CalculatePawnUpkeep(SpecialistFC s)
        {
            if (s.pawn == null || s.role == SpecialistRole.Resident) return 0;
            double skillSum = 0;
            foreach (SkillRecord sk in s.pawn.skills.skills)
            {
                skillSum += sk.Level;
            }
            double upkeep = FCSSettings.specialistBaseCost + (skillSum / FCSSettings.skillDivisor) * FCSSettings.scalingFactor;
            if (s.role == SpecialistRole.Governor)
            {
                upkeep *= HasTrait("meritocratic") ? 3.0 : 2.0;
            }
            return upkeep;
        }

        // --- IStatModifierProvider ---

        public double GetStatModifier(FCStatDef stat)
        {
            double value = stat.IdentityValue;

            // Residents contribute bonus workers
            if (stat == FCStatDefOf.workerBaseMax)
            {
                int residentCount = 0;
                foreach (SpecialistFC s in allPawns)
                {
                    if (s.role == SpecialistRole.Resident && s.pawn != null && !s.pawn.Dead)
                        residentCount++;
                }
                int perWorker = HasTrait("communalLiving") ? 3 : FCSSettings.residentsPerWorker;
                value += Math.Floor(residentCount / (double)perWorker);
            }

            // SpecialistStatEffectDefs: skill -> stat contributions
            bool profArmy = HasTrait("professionalArmy");
            bool garrison = HasTrait("garrisonDoctrine");
            List<SpecialistStatEffectDef> effects = SpecialistsCache.StatEffectsForStat(stat);
            if (effects?.Count > 0)
            {
                foreach (SpecialistStatEffectDef def in effects)
                {
                    foreach (SpecialistFC s in allPawns)
                    {
                        if (s.pawn?.skills is null || s.pawn.Dead) continue;
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
                    if (s.pawn?.skills is null || s.pawn.Dead) continue;
                    SkillRecord melee = s.pawn.skills.GetSkill(SkillDefOf.Melee);
                    SkillRecord shooting = s.pawn.skills.GetSkill(SkillDefOf.Shooting);
                    int best = Math.Max(melee?.Level ?? 0, shooting?.Level ?? 0);
                    value += best * 0.05;
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
                int residentCount = 0;
                foreach (SpecialistFC s in allPawns)
                {
                    if (s.role == SpecialistRole.Resident && s.pawn != null && !s.pawn.Dead)
                        residentCount++;
                }
                int perWorker = HasTrait("communalLiving") ? 3 : FCSSettings.residentsPerWorker;
                int bonus = (int)Math.Floor(residentCount / (double)perWorker);
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
                        if (s.pawn == null || s.pawn.Dead) continue;
                        if (s.role != def.roleFilter) continue;
                        if (s.pawn.skills == null) continue;
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
                        sb.Append("FCS_StatEffectLine".Translate(total.ToString("F1"), def.skill.skillLabel.CapitalizeFirst(), string.Join(", ", parts)));
                    }
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }

        // --- IResourceProductionModifier ---

        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            if (resource.def.associatedSkills == null) return 0;

            double bonus = 0;
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Specialist) continue;
                if (s.pawn == null || s.pawn.Dead || s.pawn.skills == null) continue;

                foreach (SkillDef skillDef in resource.def.associatedSkills)
                {
                    SpecialistSkillWeightDef weight = SpecialistsCache.SkillWeight(skillDef);
                    if (weight == null) continue;
                    SkillRecord skill = s.pawn.skills.GetSkill(skillDef);
                    if (skill != null)
                    {
                        bonus += skill.Level * weight.specialistAdditivePerLevel;
                    }
                }
            }

            if (bonus > 0 && HasTrait("specialistCorps"))
            {
                bonus *= 1.2;
            }

            return bonus * foodSatisfaction;
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            SpecialistFC gov = Governor;
            if (gov == null || gov.pawn == null || gov.pawn.Dead || gov.pawn.skills == null)
                return 1.0;

            if (resource.def.associatedSkills == null)
                return 1.0;

            SkillRecord social = gov.pawn.skills.GetSkill(SkillDefOf.Social);
            int socialLevel = social?.Level ?? 0;
            bool meritocratic = HasTrait("meritocratic");
            double socialFactor = meritocratic
                ? 0.75 + (socialLevel / 16.0)
                : 0.5 + (socialLevel / 20.0);

            double raw = 0;
            foreach (SkillDef skillDef in resource.def.associatedSkills)
            {
                SpecialistSkillWeightDef weight = SpecialistsCache.SkillWeight(skillDef);
                if (weight == null) continue;
                SkillRecord skill = gov.pawn.skills.GetSkill(skillDef);
                if (skill != null)
                {
                    raw += skill.Level * weight.governorMultiplierPerLevel;
                }
            }

            string resDefName = resource.def.defName;
            bool isFocused = gov.HasFocus(resDefName);
            double baseFocusBonus = meritocratic ? 2.0 : 1.5;
            double focusBonus = isFocused ? baseFocusBonus : 1.0;

            double multiplier = raw * socialFactor * focusBonus;
            return 1.0 + (multiplier * foodSatisfaction);
        }

        public string GetResourceAdditiveDesc(ResourceFC resource)
        {
            if (resource.def.associatedSkills == null) return null;

            double addTotal = 0;
            List<string> addParts = new List<string>();
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Specialist) continue;
                if (s.pawn == null || s.pawn.Dead || s.pawn.skills == null) continue;

                double pawnBonus = 0;
                string bestSkillName = null;
                int bestSkillLevel = 0;
                foreach (SkillDef skillDef in resource.def.associatedSkills)
                {
                    SpecialistSkillWeightDef weight = SpecialistsCache.SkillWeight(skillDef);
                    if (weight == null) continue;
                    SkillRecord skill = s.pawn.skills.GetSkill(skillDef);
                    if (skill != null)
                    {
                        pawnBonus += skill.Level * weight.specialistAdditivePerLevel;
                        if (skill.Level > bestSkillLevel)
                        {
                            bestSkillLevel = skill.Level;
                            bestSkillName = skillDef.skillLabel;
                        }
                    }
                }
                if (pawnBonus > 0)
                {
                    addTotal += pawnBonus;
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
            if (resource.def.associatedSkills == null) return null;

            SpecialistFC gov = Governor;
            if (gov == null || gov.pawn == null || gov.pawn.Dead || gov.pawn.skills == null)
                return null;

            double mult = GetResourceMultiplierModifier(resource);
            if (Math.Abs(mult - 1.0) <= 0.001)
                return null;

            bool isFocused = gov.HasFocus(resource.def.defName);
            string focusTag = isFocused ? "FCS_ResFocusTag".Translate().ToString() : "";
            SkillRecord social = gov.pawn.skills.GetSkill(SkillDefOf.Social);
            int socialLevel = social?.Level ?? 0;
            return $"{TextUtil.ColorizeMultiplierBonus(Math.Round(mult, 2))} - {"FCS_ResGovernor".Translate(gov.pawn.LabelShort, socialLevel, focusTag)}";
        }
    }
}
