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

        private bool HasTrait(string defName)
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

            // Professional Army: defense specialists cannot be reassigned
            if (specialist.role == SpecialistRole.Defense && newRole != SpecialistRole.Defense
                && HasTrait("professionalArmy"))
            {
                LogUtil.Warning("Professional Army: defense specialists cannot be reassigned.");
                return;
            }

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

        private WorldSettlementFC uiSettlement;
        private int subTab;
        private Vector2 scrollPosSpec;
        private Vector2 scrollPosRes;

        // --- UI constants ---
        private const float SubTabHeight = 24f;
        private const float CardHeight = 64f;
        private const float GovCardHeight = 90f;
        private const float CardPadding = 4f;
        private const float PortraitSize = 54f;
        private const float GovPortraitSize = 80f;
        private const float RoleBtnWidth = 55f;
        private const float RecallBtnWidth = 50f;
        private const float BtnHeight = 24f;
        private const float BtnGap = 4f;
        private const float CardGap = 2f;
        private const float SectionHeaderHeight = 26f;

        public void PreOpenWindow(WorldSettlementFC settlement)
        {
            uiSettlement = settlement;
            subTab = 0;
            scrollPosSpec = Vector2.zero;
            scrollPosRes = Vector2.zero;
        }

        public void OnTabSwitch()
        {
            subTab = 0;
            scrollPosSpec = Vector2.zero;
            scrollPosRes = Vector2.zero;
        }

        public void DrawOverviewTab(Rect boundingBox)
        {
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            bool prevWrap = Text.WordWrap;
            Color prevColor = GUI.color;

            // --- Sub-tab bar ---
            float tabW = boundingBox.width / 3f;
            string[] tabLabels =
            {
                "FCS_SubGovernor".Translate(),
                "FCS_SubSpecialists".Translate(),
                "FCS_SubResidents".Translate()
            };

            Rect chosenRect = new Rect();
            for (int i = 0; i < 3; i++)
            {
                Rect tabRect = new Rect(boundingBox.x + tabW * i, boundingBox.y, tabW, SubTabHeight);
                if (UIUtil.ButtonFlat(tabRect, tabLabels[i], highlighted: subTab == i))
                    subTab = i;
                if (subTab == i)
                    chosenRect = tabRect;
            }
            UIUtil.DrawTabDecoratorHorizontalTop(chosenRect, boundingBox, Color.gray);

            // --- Content area below sub-tabs ---
            Rect contentRect = new Rect(boundingBox.x, boundingBox.y + SubTabHeight,
                boundingBox.width, boundingBox.height - SubTabHeight);

            SpecialistFC toRecall = null;

            if (subTab == 0)
                DrawGovernorTab(contentRect, ref toRecall);
            else if (subTab == 1)
                DrawSpecialistsTab(contentRect, ref toRecall);
            else
                DrawResidentsTab(contentRect, ref toRecall);

            // Process recall outside iteration
            if (toRecall != null)
            {
                RecallPawn(toRecall);
            }

            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            Text.WordWrap = prevWrap;
            GUI.color = prevColor;
        }

        // --- Governor sub-tab ---

        private void DrawGovernorTab(Rect contentRect, ref SpecialistFC toRecall)
        {
            float x = contentRect.x;
            float w = contentRect.width;
            float y = contentRect.y + 8f;

            SpecialistFC gov = Governor;
            if (gov == null || gov.pawn == null)
            {
                Text.Font = GameFont.Small;
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Widgets.Label(contentRect, "FCS_NoGovernor".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // --- Governor card ---
            Rect cardRect = new Rect(x, y, w, GovCardHeight);
            Widgets.DrawLightHighlight(cardRect);

            // Portrait
            float portraitY = y + (GovCardHeight - GovPortraitSize) / 2f;
            Rect portraitRect = new Rect(x + CardPadding, portraitY, GovPortraitSize, GovPortraitSize);
            UIUtil.DrawPawnPortrait(portraitRect, gov.pawn, cameraZoom: 1.1f);

            // Text area to the right of portrait
            float textX = portraitRect.xMax + 8f;
            float btnAreaW = RoleBtnWidth + BtnGap + RecallBtnWidth + CardPadding;
            float textW = w - (textX - x) - btnAreaW;

            // Name (medium font)
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(textX, y + 4f, textW, 28f), gov.pawn.LabelShort);

            // Skills (small, gray, no wrap)
            Text.Font = GameFont.Small;
            Text.WordWrap = false;
            GUI.color = Color.gray;
            string skills = GetTopSkillLabel(gov);
            Widgets.Label(new Rect(textX, y + 32f, textW, 22f), skills);
            GUI.color = Color.white;
            Text.WordWrap = true;

            // Upkeep
            Text.Font = GameFont.Small;
            double upkeep = CalculatePawnUpkeep(gov);
            Widgets.Label(new Rect(textX, y + 54f, textW, 22f), "FCS_UpkeepDisplay".Translate(upkeep.ToString("F1")));
            Text.Anchor = TextAnchor.UpperLeft;

            // Role + Recall buttons (vertically centered in card)
            float btnX = x + w - btnAreaW;
            float btnY = y + (GovCardHeight - BtnHeight * 2 - BtnGap) / 2f;

            if (Widgets.ButtonText(new Rect(btnX, btnY, RoleBtnWidth, BtnHeight), "FCS_BtnRole".Translate()))
            {
                ShowRoleChangeMenu(gov);
            }
            if (Widgets.ButtonText(new Rect(btnX + RoleBtnWidth + BtnGap, btnY, RecallBtnWidth, BtnHeight), "FCS_BtnRecall".Translate()))
            {
                toRecall = gov;
            }

            y += GovCardHeight + 12f;

            // --- Focus controls ---
            Text.Font = GameFont.Small;
            bool isPatrician = HasTrait("patrician");
            int maxFocuses = isPatrician ? 2 : 1;

            if (maxFocuses > 1)
            {
                float perBtn = Math.Min((w - BtnGap) / 2f, 250f);
                for (int fi = 0; fi < maxFocuses; fi++)
                {
                    string fLabel = "FCS_FocusNone".Translate();
                    if (fi < gov.governorFocuses.Count && gov.governorFocuses[fi] != null)
                    {
                        ResourceTypeDef fd = DefDatabase<ResourceTypeDef>.GetNamedSilentFail(gov.governorFocuses[fi]);
                        if (fd != null) fLabel = fd.LabelCap;
                    }
                    float fbX = x + fi * (perBtn + BtnGap);
                    int localSlot = fi;
                    if (Widgets.ButtonText(new Rect(fbX, y, perBtn, BtnHeight),
                        "FCS_FocusSlotLabel".Translate(fi + 1, fLabel)))
                    {
                        ShowGovernorFocusMenu(gov, localSlot);
                    }
                }
            }
            else
            {
                string focusLabel = "FCS_FocusNoFocus".Translate();
                if (gov.governorFocuses.Count > 0 && gov.governorFocuses[0] != null)
                {
                    ResourceTypeDef fd = DefDatabase<ResourceTypeDef>.GetNamedSilentFail(gov.governorFocuses[0]);
                    if (fd != null) focusLabel = fd.LabelCap;
                }
                if (Widgets.ButtonText(new Rect(x, y, Math.Min(w, 250f), BtnHeight),
                    "FCS_FocusSingleLabel".Translate(focusLabel)))
                {
                    ShowGovernorFocusMenu(gov, 0);
                }
            }
        }

        // --- Specialists / Defense sub-tab ---

        private void DrawSpecialistsTab(Rect contentRect, ref SpecialistFC toRecall)
        {
            float x = contentRect.x;
            float w = contentRect.width;
            float y = contentRect.y + 4f;

            // Header
            int specCount = CivilianSpecialists.Count();
            int defCount = DefenseSpecialists.Count();
            int totalCards = specCount + defCount;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string specHeader = "FCS_SpecDefHeader".Translate(specCount, defCount);
            Widgets.Label(new Rect(x, y, w * 0.6f, SectionHeaderHeight), specHeader);

            double totalUpkeep = CalculateTotalUpkeep();
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(x, y, w, SectionHeaderHeight),
                "FCS_UpkeepDisplay".Translate(totalUpkeep.ToString("F1")));
            Text.Anchor = TextAnchor.UpperLeft;
            y += SectionHeaderHeight + 2f;

            if (totalCards == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Rect emptyRect = new Rect(x, y, w, contentRect.yMax - y);
                Widgets.Label(emptyRect, "FCS_NoSpecialists".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Scrollable card list
            float scrollH = contentRect.yMax - y;
            Rect scrollOuter = new Rect(x, y, w, scrollH);
            float innerH = totalCards * (CardHeight + CardGap);
            float scrollBarW = innerH > scrollH ? 16f : 0f;
            Rect scrollInner = new Rect(0f, 0f, w - scrollBarW, innerH);

            Widgets.BeginScrollView(scrollOuter, ref scrollPosSpec, scrollInner);
            float sy = 0f;
            int rowIdx = 0;
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Specialist && s.role != SpecialistRole.Defense) continue;
                if (s.pawn == null) continue;

                Rect rowRect = new Rect(0f, sy, scrollInner.width, CardHeight);
                DrawPawnCard(rowRect, s, true, rowIdx, ref toRecall);
                sy += CardHeight + CardGap;
                rowIdx++;
            }
            Widgets.EndScrollView();
        }

        // --- Residents sub-tab ---

        private void DrawResidentsTab(Rect contentRect, ref SpecialistFC toRecall)
        {
            float x = contentRect.x;
            float w = contentRect.width;
            float y = contentRect.y + 4f;

            // Header
            int resCount = Residents.Count();
            int workerBonus = (int)Math.Floor(resCount / (double)FCSSettings.residentsPerWorker);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string resHeader = "FCS_ResidentHeader".Translate(resCount);
            if (workerBonus > 0) resHeader += "FCS_WorkerBonusSuffix".Translate(workerBonus);
            Widgets.Label(new Rect(x, y, w, SectionHeaderHeight), resHeader);
            Text.Anchor = TextAnchor.UpperLeft;
            y += SectionHeaderHeight + 2f;

            if (resCount == 0)
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                GUI.color = Color.gray;
                Rect emptyRect = new Rect(x, y, w, contentRect.yMax - y);
                Widgets.Label(emptyRect, "FCS_NoResidents".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                return;
            }

            // Scrollable card list
            float scrollH = contentRect.yMax - y;
            Rect scrollOuter = new Rect(x, y, w, scrollH);
            float innerH = resCount * (CardHeight + CardGap);
            float scrollBarW = innerH > scrollH ? 16f : 0f;
            Rect scrollInner = new Rect(0f, 0f, w - scrollBarW, innerH);

            Widgets.BeginScrollView(scrollOuter, ref scrollPosRes, scrollInner);
            float sy = 0f;
            int rowIdx = 0;
            foreach (SpecialistFC s in allPawns)
            {
                if (s.role != SpecialistRole.Resident) continue;
                if (s.pawn == null) continue;

                Rect rowRect = new Rect(0f, sy, scrollInner.width, CardHeight);
                DrawPawnCard(rowRect, s, false, rowIdx, ref toRecall);
                sy += CardHeight + CardGap;
                rowIdx++;
            }
            Widgets.EndScrollView();
        }

        // --- Shared pawn card drawing ---

        private void DrawPawnCard(Rect rowRect, SpecialistFC s, bool showContribution, int rowIdx, ref SpecialistFC toRecall)
        {
            // Alternating background
            if (rowIdx % 2 == 0) Widgets.DrawLightHighlight(rowRect);

            // Portrait
            float portraitY = rowRect.y + (CardHeight - PortraitSize) / 2f;
            Rect portraitRect = new Rect(rowRect.x + CardPadding, portraitY, PortraitSize, PortraitSize);
            UIUtil.DrawPawnPortrait(portraitRect, s.pawn);

            // Text area
            float textX = portraitRect.xMax + 8f;
            float btnAreaW = RoleBtnWidth + BtnGap + RecallBtnWidth + CardPadding;
            float textW = rowRect.width - (textX - rowRect.x) - btnAreaW;

            // Name
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(textX, rowRect.y + 4f, textW, 22f), s.pawn.LabelShort);

            // Skills (gray, no wrap)
            Text.Font = GameFont.Tiny;
            Text.WordWrap = false;
            GUI.color = Color.gray;
            string topSkill = GetTopSkillLabel(s);
            Widgets.Label(new Rect(textX, rowRect.y + 24f, textW, 18f), topSkill);
            GUI.color = Color.white;

            // Contribution (if applicable)
            if (showContribution)
            {
                string contrib = GetContributionSummary(s);
                if (contrib.Length > 0)
                {
                    Widgets.Label(new Rect(textX, rowRect.y + 42f, textW, 18f), contrib);
                }
            }
            Text.WordWrap = true;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Role + Recall buttons (vertically centered)
            float btnX = rowRect.xMax - btnAreaW;
            float btnY = rowRect.y + (CardHeight - BtnHeight * 2 - BtnGap) / 2f;

            if (Widgets.ButtonText(new Rect(btnX, btnY, RoleBtnWidth, BtnHeight), "FCS_BtnRole".Translate()))
            {
                ShowRoleChangeMenu(s);
            }
            if (Widgets.ButtonText(new Rect(btnX + RoleBtnWidth + BtnGap, btnY, RecallBtnWidth, BtnHeight), "FCS_BtnRecall".Translate()))
            {
                toRecall = s;
            }
        }

        private void ShowGovernorFocusMenu(SpecialistFC gov, int slot = 0)
        {
            if (gov == null || gov.pawn == null) return;

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (ResourceFC resource in Settlement.Resources)
            {
                if (resource.def.associatedSkills == null || resource.def.associatedSkills.Count == 0) continue;
                ResourceTypeDef resDef = resource.def;
                string label = resDef.LabelCap;

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
                    label = "FCS_FocusSkillLevel".Translate(label, bestSkillLabel, bestLevel);
                }

                string currentFocus = slot < gov.governorFocuses.Count ? gov.governorFocuses[slot] : null;
                if (currentFocus == resDef.defName) label += " *";

                string defName = resDef.defName;
                int localSlot = slot;
                options.Add(new FloatMenuOption(label, delegate
                {
                    while (gov.governorFocuses.Count <= localSlot)
                    {
                        gov.governorFocuses.Add(null);
                    }
                    gov.governorFocuses[localSlot] = defName;
                    Settlement.InvalidateStatCache();
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
                string label = role.Translate();
                if (role == SpecialistRole.Governor && Governor != null && Governor != specialist)
                {
                    label = "FCS_RoleReplaces".Translate(label, Governor.pawn.LabelShort);
                }
                options.Add(new FloatMenuOption(label, delegate
                {
                    ChangeRole(specialist, localRole);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

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
                int meleeLevel = melee?.Level ?? 0;
                int shootingLevel = shooting?.Level ?? 0;
                double bonus = Math.Max(meleeLevel, shootingLevel) * 0.05;
                return "FCS_ContribMilLevel".Translate(bonus.ToString("F2"));
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
                    return "FCS_ContribProduction".Translate(bestBonus.ToString("F2"), bestLabel);
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
            return "FCS_TabName".Translate();
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
                        if (sb.Length > 0) sb.Append("\n");
                        sb.Append("FCS_StatEffectLine".Translate(total.ToString("F1"), def.skill.skillLabel, string.Join(", ", parts)));
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
                sb.Append("FCS_ResSpecialists".Translate(addTotal.ToString("F2"), string.Join(", ", addParts)));
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
                    bool isFocused = gov.HasFocus(resource.def.defName);
                    string focusTag = isFocused ? "FCS_ResFocusTag".Translate().ToString() : "";
                    SkillRecord social = gov.pawn.skills.GetSkill(SkillDefOf.Social);
                    int socialLevel = social?.Level ?? 0;
                    sb.Append("FCS_ResGovernor".Translate(mult.ToString("F2"), gov.pawn.LabelShort, socialLevel, focusTag));
                }
            }

            return sb.Length > 0 ? sb.ToString() : null;
        }
    }
}
