using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionColonies.Specialists
{
    internal class SpecialistsTabRenderer
    {
        private readonly WorldObjectComp_SettlementSpecialists comp;

        // UI state
        private WorldSettlementFC uiSettlement;
        private int subTab;
        private Vector2 scrollPosSpec;
        private Vector2 scrollPosRes;

        // --- UI constants ---
        private const float SubTabHeight = 24f;
        private const float CardHeight = 90f;
        private const float CardPadding = 4f;
        private const float PortraitSize = 80f;
        private const float RoleBtnWidth = 55f;
        private const float RecallBtnWidth = 50f;
        private const float BtnHeight = 24f;
        private const float BtnGap = 4f;
        private const float CardGap = 2f;
        private const float SectionHeaderHeight = 26f;
        private const float InfoCardBtnSize = 24f;
        private const float AccentBarWidth = 3f;
        private static readonly Color DefenseContribColor = new Color(0.9f, 0.4f, 0.4f);
        private static readonly Color PawnBG = new Color(0.12f, 0.12f, 0.12f);

        // Governor dashboard layout constants
        private const float GovPanelPad = 6f;
        private const float GovPanelGap = 6f;
        private const float GovTopRowHeight = 145f;
        private const float GovPortraitW = 100f;
        private const float GovPortraitH = 133f;
        private const float GovResCardW = 90f;
        private const float GovResCardH = 95f;
        private const float GovResCardGap = 4f;
        private const float GovResBarH = 10f;
        private const float GovResIconSize = 24f;
        private const float GovGridHeaderH = 22f;
        private static readonly Color GovFocusTint = new Color(0.4f, 0.35f, 0.15f, 0.3f);
        private static readonly Color GovBarBg = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color GovEmptyOverlay = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color GovGreyedOut = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color GovGreyedOutText = new Color(0.5f, 0.5f, 0.5f, 0.35f);
        
        private static readonly string[] tabLabels =
        {
            "FCS_SubGovernor".Translate(),
            "FCS_SubSpecialists".Translate(),
            "FCS_SubResidents".Translate()
        };
        private static readonly Color[] tabColors =
        {
            new Color(0.85f, 0.75f, 0.5f),
            new Color(0.45f, 0.75f, 0.35f),
            new Color(0.35f, 0.50f, 0.80f)
        };

        public SpecialistsTabRenderer(WorldObjectComp_SettlementSpecialists comp)
        {
            this.comp = comp;
        }

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

        public SpecialistFC DrawOverviewTab(Rect boundingBox)
        {
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;

            // --- Sub-tab bar ---
            float tabW = boundingBox.width / 3f;

            Rect chosenRect = new Rect();
            for (int i = 0; i < 3; i++)
            {
                Rect tabRect = new Rect(boundingBox.x + tabW * i, boundingBox.y, tabW, SubTabHeight);
                if (UIUtil.ButtonFlat(tabRect, tabLabels[i], highlighted: subTab == i))
                    subTab = i;
                if (subTab == i)
                    chosenRect = tabRect;
            }

            UIUtil.DrawColoredHighlight(chosenRect, tabColors[subTab]);
            UIUtil.DrawTabDecoratorHorizontalTop(chosenRect, boundingBox, tabColors[subTab]);//Color.gray);

            // --- Content area below sub-tabs ---
            Rect contentRect = new Rect(boundingBox.x, boundingBox.y + SubTabHeight,
                boundingBox.width, boundingBox.height - SubTabHeight);
            
            UIUtil.DrawColoredHighlight(contentRect, tabColors[subTab]);

            SpecialistFC toRecall = null;

            if (subTab == 0)
                DrawGovernorTab(contentRect, ref toRecall);
            else if (subTab == 1)
                DrawSpecialistsTab(contentRect, ref toRecall);
            else
                DrawResidentsTab(contentRect, ref toRecall);

            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.color = prevColor;

            return toRecall;
        }

        public void PostCloseWindow()
        {
            uiSettlement = null;
        }

        public string OverviewTabName()
        {
            return "FCS_TabName".Translate();
        }

        // --- Governor sub-tab (dashboard layout) ---

        private void DrawGovernorTab(Rect contentRect, ref SpecialistFC toRecall)
        {
            float x = contentRect.x;
            float w = contentRect.width;
            float y = contentRect.y + GovPanelPad;

            SpecialistFC gov = comp.Governor;
            bool hasGov = gov?.IsAlive ?? false;

            if (!hasGov)
            {
                // Draw greyed-out dashboard template
                GUI.color = GovGreyedOut;
            }

            // ===== TOP ROW: Identity Card + Focus Card =====
            float panelW = (w - GovPanelGap) / 2f;

            DrawGovIdentityCard(new Rect(x, y, panelW, GovTopRowHeight), gov, hasGov, ref toRecall);
            DrawGovFocusCard(new Rect(x + panelW + GovPanelGap, y, panelW, GovTopRowHeight), gov, hasGov);

            y += GovTopRowHeight + GovPanelGap;

            // ===== BOTTOM: Resource Contributions Grid =====
            float gridH = contentRect.yMax - y;
            DrawGovResourceGrid(new Rect(x, y, w, gridH), gov, hasGov);

            // Empty state overlay
            if (!hasGov)
            {
                GUI.color = Color.white;
                Widgets.DrawBoxSolid(contentRect, GovEmptyOverlay);

                Text.Font = GameFont.Medium;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(contentRect.x, contentRect.center.y - 20f, contentRect.width, 30f),
                    "FCS_NoGovernor".Translate());

                Text.Font = GameFont.Small;
                GUI.color = Color.gray;
                Widgets.Label(new Rect(contentRect.x, contentRect.center.y + 10f, contentRect.width, 22f),
                    "FCS_NoGovernorHint".Translate());
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        private void DrawGovIdentityCard(Rect rect, SpecialistFC gov, bool hasGov, ref SpecialistFC toRecall)
        {
            Widgets.DrawMenuSection(rect);

            float px = rect.x + GovPanelPad;
            float py = rect.y + (rect.height - GovPortraitH) / 2f;
            Rect portraitRect = new Rect(px, py, GovPortraitW, GovPortraitH);

            Widgets.DrawBoxSolid(portraitRect, PawnBG);
            if (hasGov)
            {
                UIUtil.DrawPawnPortrait(portraitRect, gov.pawn, cameraZoom: 1.1f);
            }

            float textX = portraitRect.xMax + 8f;
            float textW = rect.xMax - textX - GovPanelPad;
            float lineY = rect.y + GovPanelPad;

            // Name
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            string name = hasGov ? gov.pawn.LabelShort : "\u2014";
            Widgets.DrawHighlight(new Rect(textX, lineY, textW, 28f));
            Widgets.Label(new Rect(textX + 3f, lineY, textW - InfoCardBtnSize - 7f, 28f), name);

            if (hasGov)
            {
                Widgets.InfoCardButton(rect.xMax - GovPanelPad - InfoCardBtnSize, lineY, gov.pawn);
            }
            lineY += 28f;

            // Identity line (title, age, xenotype)
            Text.Font = GameFont.Small;
            GUI.color = hasGov ? Color.gray : GovGreyedOutText;
            string identity = hasGov ? BuildIdentityLine(gov.pawn) : "\u2014";
            Widgets.Label(new Rect(textX, lineY, textW, 22f), identity);
            lineY += 24f;

            // Social skill
            SkillRecord social = hasGov ? gov.pawn.skills?.GetSkill(SkillDefOf.Social) : null;
            int socialLevel = social?.Level ?? 0;
            GUI.color = hasGov ? Color.white : GovGreyedOut;
            Text.Font = GameFont.Small;
            string socialLabel = "FCS_GovSocial".Translate(hasGov ? socialLevel.ToString() : "\u2014");
            Widgets.Label(new Rect(textX, lineY, textW, 22f), socialLabel);
            lineY += 24f;

            // Upkeep
            double upkeep = hasGov ? comp.CalculatePawnUpkeep(gov) : 0;
            string upkeepText = "FCS_UpkeepDisplay".Translate(hasGov ? upkeep.ToString("F1") : "\u2014");
            Rect upkeepRect = new Rect(textX, lineY, textW, 22f);
            Widgets.Label(upkeepRect, upkeepText);
            if (hasGov)
            {
                string upkeepTip = BuildUpkeepTooltip(gov);
                if (upkeepTip != null) TooltipHandler.TipRegion(upkeepRect, upkeepTip);
            }
            lineY += 24f;

            // XP rate (mirrors CompTick logic)
            Text.Font = GameFont.Tiny;
            GUI.color = hasGov ? new Color(0.7f, 0.7f, 0.7f) : GovGreyedOutText;
            float xpPerDay = SpecUtil.XPPerDay();
            string xpText = hasGov
                ? (string)"FCS_GovXpRate".Translate(((int)xpPerDay).ToString())
                : "\u2014";
            Widgets.Label(new Rect(textX, lineY, textW, 16f), xpText);

            GUI.color = hasGov ? Color.white : GovGreyedOut;

            // Recall button at bottom-right of card
            if (hasGov)
            {
                float recallX = rect.xMax - GovPanelPad - RecallBtnWidth;
                float recallY = rect.yMax - GovPanelPad - BtnHeight;
                if (Widgets.ButtonText(new Rect(recallX, recallY, RecallBtnWidth, BtnHeight), "FCS_BtnRecall".Translate()))
                {
                    toRecall = gov;
                }
            }

            // Tooltip on portrait: full bio
            if (hasGov)
            {
                string topSkills = GetTopSkillLabel(gov);
                TooltipHandler.TipRegion(portraitRect, gov.pawn.LabelShort + "\n" + topSkills + "\n\n" + "FCS_TooltipSkills".Translate());
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawGovFocusCard(Rect rect, SpecialistFC gov, bool hasGov)
        {
            Widgets.DrawMenuSection(rect);

            float ix = rect.x + GovPanelPad;
            float iy = rect.y + GovPanelPad;
            float iw = rect.width - GovPanelPad * 2f;

            // Header
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = hasGov ? new Color(0.85f, 0.75f, 0.5f) : GovGreyedOutText;
            Rect headerRect = new Rect(ix, iy, iw, 20f);
            Widgets.DrawHighlight(headerRect);
            Widgets.Label(headerRect, "FCS_GovFocusHeader".Translate());
            iy += 24f;

            GUI.color = hasGov ? Color.white : GovGreyedOut;

            bool isPatrician = SpecUtil.HasTrait(SpecPolicyDefOf.FCSpatrician);
            bool isMeritocratic = SpecUtil.HasTrait(SpecPolicyDefOf.FCSmeritocratic);
            int maxFocuses = isPatrician ? 2 : 1;
            double focusMult = SpecUtil.FocusBonusMultiplier();

            // Focus selector button(s)
            if (hasGov)
            {
                string focusTip = "FCS_TooltipFocus".Translate(Math.Round(focusMult, 2));
                if (maxFocuses > 1)
                {
                    float perBtn = (iw - BtnGap) / 2f;
                    for (int fi = 0; fi < maxFocuses; fi++)
                    {
                        string fLabel = "FCS_FocusNone".Translate();
                        if (fi < gov.governorFocuses.Count && gov.governorFocuses[fi] != null)
                        {
                            ResourceTypeDef fd = gov.governorFocuses[fi];
                            if (fd != null) fLabel = fd.LabelCap;
                        }
                        int localSlot = fi;
                        Rect focusBtnRect = new Rect(ix + fi * (perBtn + BtnGap), iy, perBtn, BtnHeight);
                        if (Widgets.ButtonText(focusBtnRect, "FCS_FocusSlotLabel".Translate(fi + 1, fLabel)))
                        {
                            ShowGovernorFocusMenu(gov, localSlot);
                        }
                        TooltipHandler.TipRegion(focusBtnRect, focusTip);
                    }
                }
                else
                {
                    string focusLabel = "FCS_FocusNoFocus".Translate();
                    if (gov.governorFocuses.Count > 0 && gov.governorFocuses[0] != null)
                    {
                        ResourceTypeDef fd = gov.governorFocuses[0];
                        if (fd != null) focusLabel = fd.LabelCap;
                    }
                    Rect focusBtnRect = new Rect(ix, iy, iw, BtnHeight);
                    if (Widgets.ButtonText(focusBtnRect, "FCS_FocusSingleLabel".Translate(focusLabel)))
                    {
                        ShowGovernorFocusMenu(gov, 0);
                    }
                    TooltipHandler.TipRegion(focusBtnRect, focusTip);
                }
            }
            else
            {
                GUI.color = GovGreyedOutText;
                Widgets.Label(new Rect(ix, iy, iw, BtnHeight), "\u2014");
                GUI.color = GovGreyedOut;
            }
            iy += BtnHeight + 8f;

            // Multiplier info
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string multLabel = "FCS_GovFocusMult".Translate(Math.Round(focusMult, 2));
            if (isMeritocratic)
                multLabel += " " + "FCS_GovMeritocraticTag".Translate();
            Widgets.Label(new Rect(ix, iy, iw, 22f), multLabel);
            iy += 24f;

            // Social factor
            SkillRecord social = hasGov ? gov.pawn.skills?.GetSkill(SkillDefOf.Social) : null;
            int socialLevel = social?.Level ?? 0;
            double socialFactor = SpecUtil.GovSocialFactor(socialLevel);
            string socialFactorText = hasGov
                ? "FCS_GovSocialFactor".Translate(Math.Round(socialFactor, 2))
                : "FCS_GovSocialFactor".Translate("\u2014");
            Rect socialFactorRect = new Rect(ix, iy, iw, 22f);
            Widgets.Label(socialFactorRect, socialFactorText);
            if (hasGov)
            {
                string formula = SpecUtil.GovSocialFactorDesc(socialLevel);
                TooltipHandler.TipRegion(socialFactorRect,
                    "FCS_TooltipSocialFactor".Translate(formula, socialLevel, Math.Round(socialFactor, 2)));
            }
            iy += 24f;

            if (FCSSettings.RoutesResourcesActive)
            {
                // Food satisfaction. Only show when Routes & Resources is active
                Color satColor;
                if (!hasGov)
                    satColor = new Color(0.5f, 0.5f, 0.5f, 0.35f);
                else if (comp.FoodSatisfaction >= 0.9f)
                    satColor = AccentUtil.StatGood;
                else if (comp.FoodSatisfaction >= 0.5f)
                    satColor = AccentUtil.StatMedBad;
                else
                    satColor = AccentUtil.StatBad;

                GUI.color = satColor;
                string satText = hasGov
                    ? "FCS_GovFoodSat".Translate(Math.Round(comp.FoodSatisfaction * 100))
                    : "FCS_GovFoodSat".Translate("\u2014");
                Widgets.Label(new Rect(ix, iy, iw, 22f), satText);
            }

            GUI.color = hasGov ? Color.white : GovGreyedOut;

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawGovResourceGrid(Rect rect, SpecialistFC gov, bool hasGov)
        {
            float x = rect.x;
            float w = rect.width;
            float y = rect.y;

            // Section header
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = hasGov ? Color.white : GovGreyedOut;
            Widgets.DrawHighlight(new Rect(x, y, w, GovGridHeaderH - 2f));
            Widgets.Label(new Rect(x + 5f, y, w * 0.6f, GovGridHeaderH), "FCS_GovResHeader".Translate());

            // Total multiplier (right-aligned)
            if (hasGov)
            {
                string multSummary = BuildGovMultiplierSummary();
                if (multSummary != null)
                {
                    Text.Anchor = TextAnchor.MiddleRight;
                    GUI.color = new Color(0.85f, 0.75f, 0.5f);
                    Widgets.Label(new Rect(x, y, w - 5f, GovGridHeaderH), multSummary);
                    GUI.color = Color.white;
                }
            }
            Text.Anchor = TextAnchor.UpperLeft;
            y += GovGridHeaderH + 2f;

            // Gather resource data and cache multipliers (avoid redundant LINQ lookups)
            if (uiSettlement == null) return;

            List<ResourceFC> resources = uiSettlement.Resources.ToList();
            double[] multipliers = new double[resources.Count];
            double maxBonus = 0;
            for (int i = 0; i < resources.Count; i++)
            {
                multipliers[i] = hasGov ? comp.GetResourceMultiplierModifier(resources[i]) : 1.0;
                double bonus = Math.Abs(multipliers[i] - 1.0);
                if (bonus > maxBonus) maxBonus = bonus;
            }
            if (maxBonus < 0.001) maxBonus = 1.0; // avoid division by zero

            // Calculate grid layout
            int cardsPerRow = Math.Max(1, (int)((w + GovResCardGap) / (GovResCardW + GovResCardGap)));
            float totalCardW = cardsPerRow * GovResCardW + (cardsPerRow - 1) * GovResCardGap;
            float gridOffsetX = x + (w - totalCardW) / 2f; // center the grid

            for (int i = 0; i < resources.Count; i++)
            {
                ResourceFC res = resources[i];
                int col = i % cardsPerRow;
                int row = i / cardsPerRow;
                float cx = gridOffsetX + col * (GovResCardW + GovResCardGap);
                float cy = y + row * (GovResCardH + GovResCardGap);

                Rect cardRect = new Rect(cx, cy, GovResCardW, GovResCardH);

                // Card background, with focus tint overlay
                bool isFocused = hasGov && gov.HasFocus(res.def);
                Widgets.DrawMenuSection(cardRect);
                if (isFocused)
                {
                    Widgets.DrawBoxSolid(cardRect, GovFocusTint);
                }

                float innerX = cx + GovPanelPad;
                float innerW = GovResCardW - GovPanelPad * 2f;
                float innerY = cy + GovPanelPad;

                // Resource icon (centered)
                float iconX = cx + (GovResCardW - GovResIconSize) / 2f;
                GUI.color = hasGov ? Color.white : GovGreyedOut;
                GUI.DrawTexture(new Rect(iconX, innerY, GovResIconSize, GovResIconSize), res.def.Icon);
                innerY += GovResIconSize + 2f;

                // Resource name (centered)
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(new Rect(cx, innerY, GovResCardW, 16f), res.def.LabelCap);
                innerY += 18f;

                // Progress bar (using cached multiplier)
                double bonus = multipliers[i] - 1.0;
                float barProgress = (float)(Math.Abs(bonus) / maxBonus);
                barProgress = Mathf.Clamp01(barProgress);

                Rect barRect = new Rect(innerX, innerY, innerW, GovResBarH);
                Color barColor = isFocused
                    ? new Color(0.85f, 0.75f, 0.3f)
                    : AccentUtil.Income;
                if (!hasGov) barColor = new Color(0.3f, 0.3f, 0.3f, GovGreyedOut.a);
                UIUtil.DrawProgressBarColors(barRect, barProgress, GovBarBg, barColor);
                innerY += GovResBarH + 2f;

                // Bonus percentage (centered)
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleCenter;
                string bonusText = hasGov
                    ? (bonus >= 0 ? "+" : "") + (bonus * 100).ToString("F1") + "%"
                    : "+0.0%";
                GUI.color = hasGov ? (isFocused ? new Color(0.85f, 0.75f, 0.3f) : AccentUtil.Income) : GovGreyedOutText;
                Widgets.Label(new Rect(cx, innerY, GovResCardW, 16f), bonusText);
                GUI.color = hasGov ? Color.white : GovGreyedOut;

                // Tooltip with formula breakdown (or explanation for zero-bonus)
                if (hasGov)
                {
                    if (Math.Abs(bonus) > 0.001)
                    {
                        string tip = BuildGovResourceTooltip(res, gov);
                        if (tip != null) TooltipHandler.TipRegion(cardRect, tip);
                    }
                    else if (res.def.associatedSkills == null || res.def.associatedSkills.Count == 0)
                    {
                        TooltipHandler.TipRegion(cardRect,
                            (string)"FCS_GovResTooltipNoSkills".Translate(res.def.LabelCap));
                    }
                }

                Text.Anchor = TextAnchor.UpperLeft;
            }
        }

        // --- Specialists / Defense sub-tab ---

        private void DrawSpecialistsTab(Rect contentRect, ref SpecialistFC toRecall)
        {
            float x = contentRect.x;
            float w = contentRect.width;
            float y = contentRect.y + 4f;

            // Header
            int specCount = comp.CivilianSpecialists.Count();
            int defCount = comp.DefenseSpecialists.Count();
            int totalCards = specCount + defCount;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string specHeader = "FCS_SpecDefHeader".Translate(specCount, defCount, specCount+defCount, comp.MaxSpecialists);
            Widgets.Label(new Rect(x, y, w * 0.6f, SectionHeaderHeight), specHeader);

            double totalUpkeep = comp.CalculateSpecialistUpkeep();
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(x, y, w, SectionHeaderHeight), "FCS_UpkeepDisplay".Translate(totalUpkeep.ToString("F1")));
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
            foreach (SpecialistFC s in comp.AllPawnsInternal)
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
            int resCount = comp.LiveResidentCount;
            int workerBonus = SpecUtil.WorkerBonusFromResidents(resCount);

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
            foreach (SpecialistFC s in comp.AllPawnsInternal)
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

            Color accentColor = Color.white;

            // Resource accent bar on left edge
            if (showContribution)
            {
                accentColor = GetBestResourceColor(s);
                accentColor.a = 0.5f;
                Widgets.DrawBoxSolid(new Rect(rowRect.x, rowRect.y, AccentBarWidth, rowRect.height), accentColor);
            }

            // Portrait
            float portraitX = rowRect.x + CardPadding + (showContribution ? AccentBarWidth : 0f);
            float portraitY = rowRect.y + (CardHeight - PortraitSize) / 2f;
            Rect portraitRect = new Rect(portraitX, portraitY, PortraitSize, PortraitSize);
            Widgets.DrawBoxSolid(portraitRect, PawnBG);
            UIUtil.DrawPawnPortrait(portraitRect, s.pawn);

            // Text area
            float textX = portraitRect.xMax + 8f;
            float btnAreaW = RoleBtnWidth + BtnGap + RecallBtnWidth + CardPadding;
            float textW = rowRect.width - (textX - rowRect.x) - btnAreaW - InfoCardBtnSize - 4f;
            float textFullW = rowRect.width - (textX - rowRect.x) - 4f;

            // Line 1: Name
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect nameRect = new Rect(textX, rowRect.y + 2f, textW, 22f);
            Widgets.Label(nameRect, s.pawn.LabelShort);

            // Line 2: Identity (title, age, xenotype)
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            string identity = BuildIdentityLine(s.pawn);
            Rect identityRect = new Rect(textX, rowRect.y + 22f, textW, 16f);
            Widgets.Label(identityRect, identity);

            // Line 3: Skills
            string topSkill = GetTopSkillLabel(s);
            Rect skillRect = new Rect(textX, rowRect.y + 38f, textW, 16f);
            Widgets.Label(skillRect, topSkill);
            GUI.color = Color.white;

            // Skill tooltip
            TooltipHandler.TipRegion(skillRect, (string)"FCS_TooltipSkills".Translate());

            // Line 4: Contribution (if applicable)
            if (showContribution)
            {
                string contrib = GetContributionSummary(s);
                if (contrib.Length > 0)
                {
                    accentColor.a = 1f;
                    Rect contribRect = new Rect(textX, rowRect.y + 54f, textFullW, 34f);
                    UIUtil.DrawColoredLabel(contribRect, contrib, accentColor);

                    // Contribution tooltip
                    string contribTip = BuildContributionTooltip(s);
                    if (contribTip != null)
                    {
                        TooltipHandler.TipRegion(contribRect, contribTip);
                    }
                }
            }
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Info card button (next to name, right side)
            float infoX = textX + textW + 2f;
            float infoY = rowRect.y + 10f;
            Widgets.InfoCardButton(infoX, infoY, s.pawn);

            // Role + Recall buttons (vertically centered)
            float btnX = rowRect.xMax - btnAreaW;
            float btnY = rowRect.y + 10f; //(CardHeight - BtnHeight * 2 - BtnGap) / 2f;

            if (Widgets.ButtonText(new Rect(btnX, btnY, RoleBtnWidth, BtnHeight), "FCS_BtnRole".Translate()))
            {
                ShowRoleChangeMenu(s);
            }
            if (Widgets.ButtonText(new Rect(btnX + RoleBtnWidth + BtnGap, btnY, RecallBtnWidth, BtnHeight), "FCS_BtnRecall".Translate()))
            {
                toRecall = s;
            }
        }

        // --- Helper methods ---

        internal static string BuildIdentityLine(Pawn pawn)
        {
            List<string> parts = new List<string>();
            string title = pawn.story?.TitleShortCap;
            if (!string.IsNullOrEmpty(title))
            {
                parts.Add(title);
            }
            parts.Add("FCS_IdentityAge".Translate(pawn.ageTracker.AgeBiologicalYears));
            if (ModsConfig.BiotechActive && pawn.genes?.Xenotype != null)
            {
                parts.Add(pawn.genes.XenotypeLabelCap);
            }
            return string.Join(", ", parts);
        }

        private string BuildContributionTooltip(SpecialistFC s)
        {
            if (!s.HasUsableSkills) return null;

            if (s.role == SpecialistRole.Defense)
            {
                SkillRecord melee = s.pawn.skills.GetSkill(SkillDefOf.Melee);
                SkillRecord shooting = s.pawn.skills.GetSkill(SkillDefOf.Shooting);
                int meleeLevel = melee?.Level ?? 0;
                int shootingLevel = shooting?.Level ?? 0;
                double bonus = SpecUtil.GetMilBonus(meleeLevel, shootingLevel);
                return "FCS_TooltipDefenseBonus".Translate(
                    meleeLevel.ToString(), shootingLevel.ToString(), bonus.ToString("F2"));
            }

            if (s.role == SpecialistRole.Specialist && uiSettlement != null)
            {
                StringBuilder sb = new StringBuilder();
                double bestBonus = 0;
                string bestLabel = "";
                foreach (ResourceFC resource in uiSettlement.Resources)
                {
                    if (resource.def.associatedSkills is null) continue;
                    double resTotal = 0;
                    StringBuilder resSb = new StringBuilder();
                    resSb.AppendLine("FCS_TooltipContribHeader".Translate(resource.def.LabelCap));
                    foreach (SkillDef skillDef in resource.def.associatedSkills)
                    {
                        SpecialistSkillWeightDef weight = SpecialistsCache.SkillWeight(skillDef);
                        if (weight is null) continue;
                        SkillRecord skill = s.pawn.skills.GetSkill(skillDef);
                        if (skill != null && SpecUtil.EffectiveLevel(skill.Level) > 0)
                        {
                            int eff = SpecUtil.EffectiveLevel(skill.Level);
                            double contrib = eff * weight.specialistAdditivePerLevel;
                            resTotal += contrib;
                            resSb.AppendLine("FCS_TooltipContribLine".Translate(
                                skillDef.skillLabel.CapitalizeFirst(),
                                skill.Level.ToString(),
                                weight.specialistAdditivePerLevel.ToString("F3"),
                                contrib.ToString("F2")));
                        }
                    }
                    if (resTotal > 0)
                    {
                        resSb.AppendLine("FCS_TooltipContribTotal".Translate(resTotal.ToString("F2")));
                        if (sb.Length > 0) sb.AppendLine();
                        sb.Append(resSb.ToString().TrimEnd());
                        if (resTotal > bestBonus)
                        {
                            bestBonus = resTotal;
                            bestLabel = resource.def.LabelCap;
                        }
                    }
                }
                if (bestBonus > 0)
                {
                    sb.AppendLine();
                    sb.Append("FCS_TooltipContribBest".Translate(bestBonus.ToString("F2"), bestLabel));
                }
                return sb.Length > 0 ? sb.ToString() : null;
            }

            return null;
        }

        private string BuildUpkeepTooltip(SpecialistFC s)
        {
            if (s.pawn is null || s.role == SpecialistRole.Resident) return null;
            double skillSum = SpecUtil.PawnSkillSum(s.pawn);
            double baseUpkeep = SpecUtil.BaseUpkeep(skillSum);
            string govMult = "";
            if (s.role == SpecialistRole.Governor)
            {
                double mult = SpecUtil.GovernorUpkeepMultiplier();
                govMult = "FCS_TooltipUpkeepGovMult".Translate(mult.ToString("F1"));
            }
            return "FCS_TooltipUpkeep".Translate(
                FCSSettings.specialistBaseCost.ToString("F1"),
                skillSum.ToString("F0"),
                FCSSettings.skillDivisor.ToString("F0"),
                FCSSettings.scalingFactor.ToString("F1"),
                baseUpkeep.ToString("F1"),
                govMult);
        }

        private Color GetBestResourceColor(SpecialistFC s)
        {
            if (s.role == SpecialistRole.Defense) return DefenseContribColor;
            if (!s.HasUsableSkills || uiSettlement is null) return Color.white;

            double bestBonus = 0;
            Color bestColor = Color.white;
            foreach (ResourceFC resource in uiSettlement.Resources)
            {
                double resBonus = SpecUtil.SpecialistAdditiveForResource(s.pawn, resource);
                if (resBonus > bestBonus)
                {
                    bestBonus = resBonus;
                    bestColor = resource.def.color;
                }
            }
            return bestColor;
        }

        private string BuildGovResourceTooltip(ResourceFC resource, SpecialistFC gov)
        {
            if (gov is null || !gov.HasUsableSkills) return null;
            if (resource.def.associatedSkills is null) return null;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FCS_GovResTooltipHeader".Translate(resource.def.LabelCap));
            sb.AppendLine("\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");

            double raw = 0;
            foreach (SkillDef skillDef in resource.def.associatedSkills)
            {
                SpecialistSkillWeightDef weight = SpecialistsCache.SkillWeight(skillDef);
                if (weight is null) continue;
                SkillRecord skill = gov.pawn.skills.GetSkill(skillDef);
                if (skill != null && SpecUtil.EffectiveLevel(skill.Level) > 0)
                {
                    int eff = SpecUtil.EffectiveLevel(skill.Level);
                    double contrib = eff * weight.governorMultiplierPerLevel;
                    raw += contrib;
                    sb.AppendLine("FCS_GovResTooltipSkill".Translate(
                        skillDef.skillLabel.CapitalizeFirst(),
                        skill.Level.ToString(),
                        weight.governorMultiplierPerLevel.ToString("F3"),
                        contrib.ToString("F3")));
                }
            }

            SkillRecord social = gov.pawn.skills.GetSkill(SkillDefOf.Social);
            double socialFactor = SpecUtil.GovSocialFactor(social?.Level ?? 0);

            bool isFocused = gov.HasFocus(resource.def);
            double focusBonus = isFocused ? SpecUtil.FocusBonusMultiplier() : 1.0;

            sb.AppendLine("FCS_GovResTooltipSocial".Translate(socialFactor.ToString("F3")));
            sb.AppendLine("FCS_GovResTooltipFocus".Translate(focusBonus.ToString("F1")));
            sb.AppendLine("FCS_GovResTooltipFood".Translate(comp.FoodSatisfaction.ToString("F1")));
            sb.AppendLine("\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500");

            double finalMult = raw * socialFactor * focusBonus * comp.FoodSatisfaction;
            sb.AppendLine("FCS_GovResTooltipFinal".Translate((finalMult * 100).ToString("F1")));
            sb.Append("FCS_GovResTooltipBarNote".Translate());

            return sb.ToString();
        }

        private string BuildGovMultiplierSummary()
        {
            SpecialistFC gov = comp.Governor;
            if (gov is null || !gov.HasUsableSkills || uiSettlement is null)
                return null;

            double bestMult = 0;
            string bestLabel = "";
            foreach (ResourceFC resource in uiSettlement.Resources)
            {
                double mult = comp.GetResourceMultiplierModifier(resource);
                if (mult > bestMult)
                {
                    bestMult = mult;
                    bestLabel = resource.def.LabelCap;
                }
            }
            if (bestMult > 1.001)
            {
                return "FCS_GovMultSummary".Translate(bestMult.ToString("F2"), bestLabel);
            }
            return null;
        }

        private void ShowGovernorFocusMenu(SpecialistFC gov, int slot = 0)
        {
            if (gov?.pawn is null) return;

            List<FloatMenuOption> options = new List<FloatMenuOption>();
            foreach (ResourceFC resource in comp.Settlement.Resources)
            {
                if (resource.def.associatedSkills == null || resource.def.associatedSkills.Count == 0) continue;
                ResourceTypeDef resDef = resource.def;
                string label = resDef.LabelCap;

                int bestLevel = 0;
                string bestSkillLabel = "";
                foreach (SkillDef sk in resDef.associatedSkills)
                {
                    SkillRecord rec = gov.pawn.skills.GetSkill(sk);
                    if (rec?.Level > bestLevel)
                    {
                        bestLevel = rec.Level;
                        bestSkillLabel = sk.skillLabel;
                    }
                }
                if (bestLevel > 0)
                {
                    label = "FCS_FocusSkillLevel".Translate(label, bestSkillLabel, bestLevel);
                }

                ResourceTypeDef currentFocus = slot < gov.governorFocuses.Count ? gov.governorFocuses[slot] : null;
                if (currentFocus == resDef) label += " *";

                int localSlot = slot;
                options.Add(new FloatMenuOption(label, delegate
                {
                    comp.SetGovernorFocus(gov, resDef, localSlot);
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
                if (role == SpecialistRole.Governor && comp.Governor != null && comp.Governor != specialist)
                {
                    label = "FCS_RoleReplaces".Translate(label, comp.Governor.pawn.LabelShort);
                }
                options.Add(new FloatMenuOption(label, delegate
                {
                    comp.ChangeRole(specialist, localRole);
                }));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private string GetTopSkillLabel(SpecialistFC s)
        {
            if (!s.HasUsableSkills) return "";
            SkillRecord best = null;
            SkillRecord second = null;
            foreach (SkillRecord sk in s.pawn.skills.skills)
            {
                if (sk.TotallyDisabled) continue;
                if (best is null || sk.Level > best.Level)
                {
                    second = best;
                    best = sk;
                }
                else if (second is null || sk.Level > second.Level)
                {
                    second = sk;
                }
            }
            if (best is null) return "";
            string result = best.def.skillLabel.CapitalizeFirst() + " " + best.Level;
            if (second != null)
            {
                result += ", " + second.def.skillLabel.CapitalizeFirst() + " " + second.Level;
            }
            return result;
        }

        private string GetContributionSummary(SpecialistFC s)
        {
            if (!s.HasUsableSkills) return "";

            if (s.role == SpecialistRole.Defense)
            {
                double bonus = SpecUtil.GetMilBonus(s);
                return "FCS_ContribMilLevel".Translate(bonus.ToString("F2"));
            }

            if (s.role == SpecialistRole.Specialist && uiSettlement != null)
            {
                // Collect all resource bonuses, sorted descending
                List<KeyValuePair<string, double>> bonuses = new List<KeyValuePair<string, double>>();
                foreach (ResourceFC resource in uiSettlement.Resources)
                {
                    double resBonus = SpecUtil.SpecialistAdditiveForResource(s.pawn, resource);
                    if (resBonus > 0.001)
                    {
                        bonuses.Add(new KeyValuePair<string, double>(resource.def.LabelCap, resBonus));
                    }
                }
                if (bonuses.Count > 0)
                {
                    bonuses.Sort((a, b) => b.Value.CompareTo(a.Value));
                    StringBuilder sb = new StringBuilder();
                    for (int i = 0; i < bonuses.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        sb.Append("+" + bonuses[i].Value.ToString("F2") + " " + bonuses[i].Key);
                    }
                    return sb.ToString();
                }
            }

            return "";
        }
    }
}
