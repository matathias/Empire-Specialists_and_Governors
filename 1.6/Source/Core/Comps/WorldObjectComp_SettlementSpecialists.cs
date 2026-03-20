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

        public int TotalCount
        {
            get { return allPawns.Count; }
        }

        public WorldSettlementFC Settlement
        {
            get { return (WorldSettlementFC)parent; }
        }

        // --- Core roster operations ---

        public void AssignPawn(Pawn pawn, SpecialistRole role)
        {
            if (pawn == null) return;

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
                specialist.governorFocusDefName = null;
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
        private List<Pawn> deployedPawns = new List<Pawn>();

        public void DeployToBattle(Map map, List<Pawn> defenders, Lord defenseLord)
        {
            if (pawnsDeployedToBattle) return;
            deployedPawns.Clear();

            foreach (SpecialistFC s in allPawns)
            {
                Pawn pawn = s.pawn;
                if (pawn == null || pawn.Dead) continue;

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
                RemoveSpecialist(s);

                LetterDef letterDef = role == SpecialistRole.Governor
                    ? LetterDefOf.Death : LetterDefOf.NegativeEvent;
                string label = role == SpecialistRole.Governor
                    ? "Governor killed" : "Specialist killed";
                Find.LetterStack.ReceiveLetter(label,
                    pawn.LabelShort + " (" + role + ") died defending " + Settlement.Name + ".",
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
                defaultLabel = "Assign to Settlement",
                defaultDesc = "Assign pawns from this caravan to work at " + Settlement.Name + ".",
                icon = TexCommand.Install,
                action = delegate
                {
                    Find.WindowStack.Add(new Dialog_AssignSpecialists(caravan, this));
                }
            };
        }

        // --- ISettlementWindowOverview ---

        private WorldSettlementFC uiSettlement;
        private Vector2 scrollPos;

        private const float RowHeight = 28f;
        private const float SectionHeaderHeight = 26f;
        private const float RoleBtnWidth = 55f;
        private const float RecallBtnWidth = 50f;
        private const float BtnGap = 4f;

        public void PreOpenWindow(WorldSettlementFC settlement)
        {
            uiSettlement = settlement;
            scrollPos = Vector2.zero;
        }

        public void OnTabSwitch()
        {
            scrollPos = Vector2.zero;
        }

        public void DrawOverviewTab(Rect boundingBox)
        {
            float x = boundingBox.x;
            float w = boundingBox.width;
            float curY = boundingBox.y;

            // --- Governor section (fixed, not scrolled) ---
            curY = DrawGovernorSection(x, curY, w);
            curY += 4f;

            // Separator line
            Widgets.DrawLineHorizontal(x, curY, w);
            curY += 4f;

            // --- Scrollable list for specialists + residents ---
            float scrollAreaHeight = boundingBox.yMax - curY;
            Rect scrollOuterRect = new Rect(x, curY, w, scrollAreaHeight);

            // Calculate total inner height
            int specCount = CivilianSpecialists.Count();
            int defCount = DefenseSpecialists.Count();
            int resCount = Residents.Count();
            int nonResidentCount = specCount + defCount;
            float innerHeight = SectionHeaderHeight + (nonResidentCount * RowHeight)
                + 8f + SectionHeaderHeight + (resCount * RowHeight) + 8f;

            float scrollBarWidth = innerHeight > scrollAreaHeight ? 16f : 0f;
            Rect scrollInnerRect = new Rect(0f, 0f, w - scrollBarWidth, innerHeight);

            Widgets.BeginScrollView(scrollOuterRect, ref scrollPos, scrollInnerRect);
            float sy = 0f;

            // --- Specialist / Defense section header ---
            Text.Font = GameFont.Small;
            string specHeader = "Specialists (" + specCount + ")  |  Defense (" + defCount + ")";
            Widgets.Label(new Rect(0f, sy, scrollInnerRect.width * 0.6f, SectionHeaderHeight), specHeader);

            double totalUpkeep = CalculateTotalUpkeep();
            Text.Anchor = TextAnchor.UpperRight;
            Widgets.Label(new Rect(0f, sy, scrollInnerRect.width, SectionHeaderHeight),
                "Upkeep: " + totalUpkeep.ToString("F1") + "s/day");
            Text.Anchor = TextAnchor.UpperLeft;
            sy += SectionHeaderHeight;

            // --- Specialist and Defense rows ---
            SpecialistFC toRecall = null;
            int rowIdx = 0;
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Specialist && s.role != SpecialistRole.Defense) continue;
                if (s.pawn == null) continue;

                Rect rowRect = new Rect(0f, sy, scrollInnerRect.width, RowHeight);
                if (rowIdx % 2 == 1) Widgets.DrawLightHighlight(rowRect);

                float rx = 0f;

                // Name
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(rx, sy, 130f, RowHeight), s.pawn.LabelShort);
                rx += 134f;

                // Top skill
                GUI.color = Color.gray;
                string topSkill = GetTopSkillLabel(s);
                Widgets.Label(new Rect(rx, sy, 100f, RowHeight), topSkill);
                rx += 104f;
                GUI.color = Color.white;

                // Contribution
                string contrib = GetContributionSummary(s);
                Widgets.Label(new Rect(rx, sy, 110f, RowHeight), contrib);
                rx = scrollInnerRect.width - RoleBtnWidth - BtnGap - RecallBtnWidth;

                // Role button
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonText(new Rect(rx, sy + 2f, RoleBtnWidth, RowHeight - 4f), "Role"))
                {
                    ShowRoleChangeMenu(s);
                }
                rx += RoleBtnWidth + BtnGap;

                // Recall button
                if (Widgets.ButtonText(new Rect(rx, sy + 2f, RecallBtnWidth, RowHeight - 4f), "Recall"))
                {
                    toRecall = s;
                }

                Text.Anchor = TextAnchor.UpperLeft;
                sy += RowHeight;
                rowIdx++;
            }

            sy += 8f;

            // --- Resident section header ---
            int workerBonus = (int)Math.Floor(resCount / (double)FCSSettings.residentsPerWorker);
            string resHeader = "Residents (" + resCount + ")";
            if (workerBonus > 0) resHeader += "  ->  +" + workerBonus + " workers";
            Widgets.Label(new Rect(0f, sy, scrollInnerRect.width, SectionHeaderHeight), resHeader);
            sy += SectionHeaderHeight;

            // --- Resident rows ---
            rowIdx = 0;
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Resident) continue;
                if (s.pawn == null) continue;

                Rect rowRect = new Rect(0f, sy, scrollInnerRect.width, RowHeight);
                if (rowIdx % 2 == 1) Widgets.DrawLightHighlight(rowRect);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(new Rect(0f, sy, scrollInnerRect.width - RoleBtnWidth - BtnGap - RecallBtnWidth - 8f, RowHeight),
                    s.pawn.LabelShort);

                float rx = scrollInnerRect.width - RoleBtnWidth - BtnGap - RecallBtnWidth;
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonText(new Rect(rx, sy + 2f, RoleBtnWidth, RowHeight - 4f), "Role"))
                {
                    ShowRoleChangeMenu(s);
                }
                rx += RoleBtnWidth + BtnGap;

                if (Widgets.ButtonText(new Rect(rx, sy + 2f, RecallBtnWidth, RowHeight - 4f), "Recall"))
                {
                    toRecall = s;
                }

                sy += RowHeight;
                rowIdx++;
            }

            Widgets.EndScrollView();

            // Process recall outside the iteration
            if (toRecall != null)
            {
                RecallPawn(toRecall);
            }
        }

        private float DrawGovernorSection(float x, float startY, float w)
        {
            float y = startY;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(x, y, w, SectionHeaderHeight), "Governor");
            Text.Font = GameFont.Small;
            y += SectionHeaderHeight;

            SpecialistFC gov = Governor;
            if (gov == null || gov.pawn == null)
            {
                GUI.color = Color.gray;
                Widgets.Label(new Rect(x, y, w, RowHeight), "No governor assigned");
                GUI.color = Color.white;
                y += RowHeight;
                return y;
            }

            // Row 1: Name + skills + focus button
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, y, 140f, RowHeight), gov.pawn.LabelShort);

            GUI.color = Color.gray;
            string skills = GetTopSkillLabel(gov);
            Widgets.Label(new Rect(x + 144f, y, 120f, RowHeight), skills);
            GUI.color = Color.white;

            // Focus button
            string focusLabel = "No focus";
            if (gov.governorFocusDefName != null)
            {
                ResourceTypeDef focusDef = DefDatabase<ResourceTypeDef>.GetNamedSilentFail(gov.governorFocusDefName);
                if (focusDef != null) focusLabel = focusDef.LabelCap;
            }

            float focusBtnX = x + 268f;
            float focusBtnW = w - 268f;
            Text.Anchor = TextAnchor.UpperLeft;
            if (Widgets.ButtonText(new Rect(focusBtnX, y + 2f, Math.Min(focusBtnW, 150f), RowHeight - 4f),
                "Focus: " + focusLabel))
            {
                ShowGovernorFocusMenu(gov);
            }
            y += RowHeight;

            // Row 2: Upkeep + role/recall buttons
            double upkeep = CalculatePawnUpkeep(gov);
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(x, y, 200f, RowHeight), "Upkeep: " + upkeep.ToString("F1") + "s/day");

            Text.Anchor = TextAnchor.UpperLeft;
            float btnX = x + w - RoleBtnWidth - BtnGap - RecallBtnWidth;
            if (Widgets.ButtonText(new Rect(btnX, y + 2f, RoleBtnWidth, RowHeight - 4f), "Role"))
            {
                ShowRoleChangeMenu(gov);
            }
            btnX += RoleBtnWidth + BtnGap;
            if (Widgets.ButtonText(new Rect(btnX, y + 2f, RecallBtnWidth, RowHeight - 4f), "Recall"))
            {
                RecallPawn(gov);
            }

            Text.Anchor = TextAnchor.UpperLeft;
            y += RowHeight;
            return y;
        }

        private void ShowGovernorFocusMenu(SpecialistFC gov)
        {
            if (gov == null || gov.pawn == null) return;

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (ResourceFC resource in Settlement.Resources)
            {
                if (resource.def.associatedSkills == null || resource.def.associatedSkills.Count == 0) continue;
                ResourceTypeDef resDef = resource.def;
                string label = resDef.LabelCap;

                // Show pawn's best relevant skill level
                int bestLevel = 0;
                string bestSkillLabel = "";
                foreach (SkillDef sk in resDef.associatedSkills)
                {
                    SkillRecord rec = gov.pawn.skills.GetSkill(sk);
                    if (rec != null && rec.Level > bestLevel)
                    {
                        bestLevel = rec.Level;
                        bestSkillLabel = sk.skillLabel;
                    }
                }
                if (bestLevel > 0)
                {
                    label += " (" + bestSkillLabel + " " + bestLevel + ")";
                }

                bool isCurrent = gov.governorFocusDefName == resDef.defName;
                if (isCurrent) label += " *";

                string defName = resDef.defName;
                options.Add(new FloatMenuOption(label, delegate
                {
                    SetGovernorFocus(gov, defName);
                }));
            }

            if (options.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(options));
            }
        }

        private void ShowRoleChangeMenu(SpecialistFC specialist)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (SpecialistRole role in Enum.GetValues(typeof(SpecialistRole)))
            {
                if (role == specialist.role) continue;
                SpecialistRole localRole = role;
                string label = role.ToString();
                if (role == SpecialistRole.Governor && Governor != null && Governor != specialist)
                {
                    label += " (replaces " + Governor.pawn.LabelShort + ")";
                }
                options.Add(new FloatMenuOption(label, delegate
                {
                    ChangeRole(specialist, localRole);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public void SetGovernorFocus(SpecialistFC gov, string focusDefName)
        {
            if (gov == null || gov.role != SpecialistRole.Governor) return;
            gov.governorFocusDefName = focusDefName;
            Settlement.InvalidateStatCache();
        }

        private string GetTopSkillLabel(SpecialistFC s)
        {
            if (s.pawn == null || s.pawn.skills == null) return "";
            SkillRecord best = null;
            SkillRecord second = null;
            foreach (SkillRecord sk in s.pawn.skills.skills)
            {
                if (sk.TotallyDisabled) continue;
                if (best == null || sk.Level > best.Level)
                {
                    second = best;
                    best = sk;
                }
                else if (second == null || sk.Level > second.Level)
                {
                    second = sk;
                }
            }
            if (best == null) return "";
            string result = best.def.skillLabel.CapitalizeFirst() + " " + best.Level;
            if (second != null)
            {
                result += ", " + second.def.skillLabel.CapitalizeFirst() + " " + second.Level;
            }
            return result;
        }

        private string GetContributionSummary(SpecialistFC s)
        {
            if (s.pawn == null || s.pawn.skills == null) return "";

            if (s.role == SpecialistRole.Defense)
            {
                SkillRecord melee = s.pawn.skills.GetSkill(SkillDefOf.Melee);
                SkillRecord shooting = s.pawn.skills.GetSkill(SkillDefOf.Shooting);
                int meleeLevel = melee != null ? melee.Level : 0;
                int shootingLevel = shooting != null ? shooting.Level : 0;
                double bonus = Math.Max(meleeLevel, shootingLevel) * 0.05;
                return "+" + bonus.ToString("F2") + " Mil.Lvl";
            }

            if (s.role == SpecialistRole.Specialist && uiSettlement != null)
            {
                double bestBonus = 0;
                string bestLabel = "";
                foreach (ResourceFC resource in uiSettlement.Resources)
                {
                    if (resource.def.associatedSkills == null) continue;
                    double resBonus = 0;
                    foreach (SkillDef skillDef in resource.def.associatedSkills)
                    {
                        SpecialistSkillWeightDef weight = SpecialistsCache.SkillWeight(skillDef);
                        if (weight == null) continue;
                        SkillRecord skill = s.pawn.skills.GetSkill(skillDef);
                        if (skill != null)
                        {
                            resBonus += skill.Level * weight.specialistAdditivePerLevel;
                        }
                    }
                    if (resBonus > bestBonus)
                    {
                        bestBonus = resBonus;
                        bestLabel = resource.def.LabelCap;
                    }
                }
                if (bestBonus > 0)
                {
                    return "+" + bestBonus.ToString("F2") + " " + bestLabel;
                }
            }

            return "";
        }

        public void PostCloseWindow()
        {
            uiSettlement = null;
        }

        public string OverviewTabName()
        {
            return "Specialists";
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
            if (s.role == SpecialistRole.Governor) upkeep *= 2.0;
            return upkeep;
        }

        // --- IStatModifierProvider ---

        public double GetStatModifier(FCStatDef stat)
        {
            double value = 0;

            // Residents contribute bonus workers
            if (stat == FCStatDefOf.workerBaseMax)
            {
                int residentCount = 0;
                foreach (SpecialistFC s in allPawns)
                {
                    if (s.role == SpecialistRole.Resident && s.pawn != null && !s.pawn.Dead)
                        residentCount++;
                }
                value += Math.Floor(residentCount / (double)FCSSettings.residentsPerWorker);
            }

            // SpecialistStatEffectDefs: skill -> stat contributions
            List<SpecialistStatEffectDef> effects = SpecialistsCache.StatEffectsForStat(stat);
            if (effects != null)
            {
                foreach (SpecialistStatEffectDef def in effects)
                {
                    foreach (SpecialistFC s in allPawns)
                    {
                        if (s.pawn == null || s.pawn.Dead) continue;
                        if (s.role != def.roleFilter) continue;
                        if (s.pawn.skills == null) continue;
                        SkillRecord skill = s.pawn.skills.GetSkill(def.skill);
                        if (skill != null)
                        {
                            value += skill.Level * def.specialistValuePerLevel;
                        }
                    }
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
                int bonus = (int)Math.Floor(residentCount / (double)FCSSettings.residentsPerWorker);
                if (bonus > 0)
                {
                    sb.Append("+" + bonus + " workers (" + residentCount + " residents)");
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
                        if (sb.Length > 0) sb.Append("\n");
                        sb.Append("+" + total.ToString("F1") + " (" + def.skill.skillLabel + ": " + string.Join(", ", parts) + ")");
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

            return bonus;
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            SpecialistFC gov = Governor;
            if (gov == null || gov.pawn == null || gov.pawn.Dead || gov.pawn.skills == null)
                return 1.0;

            if (resource.def.associatedSkills == null)
                return 1.0;

            SkillRecord social = gov.pawn.skills.GetSkill(SkillDefOf.Social);
            double socialFactor = 0.5 + ((social != null ? social.Level : 0) / 20.0);

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

            bool isFocused = gov.governorFocusDefName == resource.def.defName;
            double focusBonus = isFocused ? 1.5 : 1.0;

            return 1.0 + (raw * socialFactor * focusBonus);
        }

        public string GetResourceModifierDesc(ResourceFC resource)
        {
            StringBuilder sb = new StringBuilder();

            // Specialist additive contributions
            double addTotal = 0;
            List<string> addParts = new List<string>();
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Specialist) continue;
                if (s.pawn == null || s.pawn.Dead || s.pawn.skills == null) continue;
                if (resource.def.associatedSkills == null) continue;

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
                sb.Append("Specialists: +" + addTotal.ToString("F2") + " (" + string.Join(", ", addParts) + ")");
            }

            // Governor multiplier contribution
            SpecialistFC gov = Governor;
            if (gov != null && gov.pawn != null && !gov.pawn.Dead && gov.pawn.skills != null
                && resource.def.associatedSkills != null)
            {
                double mult = GetResourceMultiplierModifier(resource);
                if (Math.Abs(mult - 1.0) > 0.001)
                {
                    if (sb.Length > 0) sb.Append("\n");
                    bool isFocused = gov.governorFocusDefName == resource.def.defName;
                    string focusTag = isFocused ? ", Focus" : "";
                    SkillRecord social = gov.pawn.skills.GetSkill(SkillDefOf.Social);
                    int socialLevel = social != null ? social.Level : 0;
                    sb.Append("Governor: x" + mult.ToString("F2") + " (" + gov.pawn.LabelShort
                        + " Social " + socialLevel + focusTag + ")");
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }
    }
}
