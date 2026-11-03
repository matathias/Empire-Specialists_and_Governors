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
        private Vector2 scrollPosGovRes;
        private Vector2 scrollPosFocus;

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
        private const float GovTopRowHeight = 190f;
        private const float GovPortraitW = 100f;
        private const float GovPortraitH = 160f;
        private const float GovResBarH = 6f;
        private const float GovResIconSize = 22f;
        private const float GovGridHeaderH = 30f;
        private const float GovResRowH = 26f;
        private const float GovResNameW = 80f;
        private static readonly Color GovFocusTint = new Color(0.4f, 0.35f, 0.15f, 0.3f);
        private static readonly Color GovBarBg = new Color(0.15f, 0.15f, 0.15f);
        private static readonly Color GovEmptyOverlay = new Color(0f, 0f, 0f, 0.45f);
        private static readonly Color GovGreyedOut = new Color(1f, 1f, 1f, 0.35f);
        private static readonly Color GovGreyedOutText = new Color(0.5f, 0.5f, 0.5f, 0.35f);

        // Resolved per window-open (in PreOpenWindow) so a mid-session language change is picked up.
        private string[] tabLabels;
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
            tabLabels = new[]
            {
                (string)"FCS_SubGovernor".Translate(),
                (string)"FCS_SubSpecialists".Translate(),
                (string)"FCS_SubResidents".Translate()
            };
            subTab = 0;
            scrollPosSpec = Vector2.zero;
            scrollPosRes = Vector2.zero;
            scrollPosGovRes = Vector2.zero;
            scrollPosFocus = Vector2.zero;
        }

        public void OnTabSwitch()
        {
            subTab = 0;
            scrollPosSpec = Vector2.zero;
            scrollPosRes = Vector2.zero;
            scrollPosGovRes = Vector2.zero;
            scrollPosFocus = Vector2.zero;
        }

        public void DrawOverviewTab(Rect boundingBox)
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
            UIUtil.DrawTabDecoratorHorizontalTop(chosenRect, boundingBox, tabColors[subTab]);

            // --- Content area below sub-tabs ---
            Rect contentRect = new Rect(boundingBox.x, boundingBox.y + SubTabHeight,
                boundingBox.width, boundingBox.height - SubTabHeight);

            UIUtil.DrawColoredHighlight(contentRect, tabColors[subTab]);

            if (subTab == 0)
                DrawGovernorTab(contentRect);
            else if (subTab == 1)
                DrawSpecialistsTab(contentRect);
            else
                DrawResidentsTab(contentRect);

            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.color = prevColor;
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

        private void DrawGovernorTab(Rect contentRect)
        {
            float x = contentRect.x;
            float w = contentRect.width;
            float y = contentRect.y + GovPanelPad;

            SettlementGovernor gov = comp.Governor;
            bool hasGov = gov is object && gov.IsAlive;

            if (!hasGov)
            {
                // Draw greyed-out dashboard template
                GUI.color = GovGreyedOut;
            }

            // ===== TOP ROW: Identity Card + Focus Card =====
            float panelW = (w - GovPanelGap) / 2f;

            DrawGovIdentityCard(new Rect(x, y, panelW, GovTopRowHeight), gov, hasGov);
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

        private void DrawGovIdentityCard(Rect rect, SettlementGovernor gov, bool hasGov)
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

            // Identity line (title, age, xenotype) — dynamic height
            Text.Font = GameFont.Small;
            GUI.color = hasGov ? Color.gray : GovGreyedOutText;
            string identity = hasGov ? BuildIdentityLine(gov.pawn) : "\u2014";
            float identityH = Text.CalcHeight(identity, textW);
            Widgets.Label(new Rect(textX, lineY, textW, identityH), identity);
            lineY += identityH + 2f;

            // Skill score
            GUI.color = hasGov ? Color.white : GovGreyedOut;
            Text.Font = GameFont.Small;
            string scoreLabel = "FCS_GovSkillScore".Translate(hasGov ? gov.SkillScore.ToString("F1") : "\u2014");
            Widgets.Label(new Rect(textX, lineY, textW, 22f), scoreLabel);
            lineY += 24f;

            // Upkeep
            double upkeep = hasGov ? SpecUtil.GovernorUpkeep(gov) : 0;
            string upkeepText = "FCS_UpkeepDisplay".Translate(hasGov ? upkeep.ToString("F1") : "\u2014");
            Rect upkeepRect = new Rect(textX, lineY, textW, 22f);
            Widgets.Label(upkeepRect, upkeepText);
            if (hasGov && gov.focus is object)
            {
                double mult = SpecUtil.GovernorUpkeepMultiplier;
                string upkeepTip = "FCS_TooltipUpkeepGovFlat".Translate(
                    gov.focus.baseUpkeepSilver.ToString("F1"),
                    gov.SkillScore.ToString("F1"),
                    gov.focus.skillUpkeepScaling.ToString("F2"),
                    FCSSettings.scalingFactor.ToString("F2"),
                    mult.ToString("F1"),
                    upkeep.ToString("F1"));
                TooltipHandler.TipRegion(upkeepRect, upkeepTip);
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
            lineY += 18f;

            // Death chance on defeat
            if (hasGov)
            {
                float govC, specC, resC, baseGov, baseSpec, baseRes;
                string reductionTip;
                ComputeDeathChances(out govC, out specC, out resC, out baseGov, out baseSpec, out baseRes, out reductionTip);
                Rect deathRect = new Rect(textX, lineY, textW, 16f);
                DrawDeathChanceLine(deathRect, baseGov, govC, reductionTip);
            }

            GUI.color = hasGov ? Color.white : GovGreyedOut;

            // Recall button at bottom-right of card
            if (hasGov)
            {
                float recallX = rect.xMax - GovPanelPad - RecallBtnWidth;
                float recallY = rect.yMax - GovPanelPad - BtnHeight;
                if (Widgets.ButtonText(new Rect(recallX, recallY, RecallBtnWidth, BtnHeight), "FCS_BtnRecall".Translate()))
                {
                    comp.RecallGovernor();
                }
            }

            // Tooltip on portrait: full bio
            if (hasGov)
            {
                string topSkills = GetTopSkillLabelGovernor(gov);
                TooltipHandler.TipRegion(portraitRect, gov.pawn.LabelShort + "\n" + topSkills + "\n\n" + "FCS_TooltipSkills".Translate());
            }

            Text.Anchor = TextAnchor.UpperLeft;
        }

        private void DrawGovFocusCard(Rect rect, SettlementGovernor gov, bool hasGov)
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

            // Focus selector button
            if (hasGov)
            {
                string focusLabel = gov.focus is object ? gov.focus.LabelCap : "FCS_FocusNoFocus".Translate();
                Rect focusBtnRect = new Rect(ix, iy, iw, BtnHeight);
                if (Widgets.ButtonText(focusBtnRect, "FCS_FocusSingleLabel".Translate(focusLabel)))
                {
                    ShowGovernorFocusMenu(gov);
                }
                string focusTip = "FCS_TooltipFocusCurrent".Translate(gov.focus is object ? (string)gov.focus.LabelCap : "\u2014");
                TooltipHandler.TipRegion(focusBtnRect, focusTip);
            }
            else
            {
                GUI.color = GovGreyedOutText;
                Widgets.Label(new Rect(ix, iy, iw, BtnHeight), "\u2014");
                GUI.color = GovGreyedOut;
            }
            iy += BtnHeight + 4f;

            // Reserve space for food satisfaction at bottom
            float reservedBottom = FCSSettings.RoutesResourcesActive ? 24f : 0f;
            float scrollAreaH = rect.yMax - iy - GovPanelPad - reservedBottom;

            // Scrollable description + bonus lines area
            if (hasGov && gov.focus is object && scrollAreaH > 0f)
            {
                // Compute inner content height
                float innerH = 0f;
                Text.Font = GameFont.Tiny;
                string desc = gov.focus.description ?? "";
                if (desc.Length > 0)
                {
                    innerH += Text.CalcHeight(desc, iw - 16f) + 4f;
                }

                float score = gov.SkillScore;
                int bonusLineCount = 0;
                if (gov.focus.resourceBonuses is object)
                {
                    foreach (ResourceProductionBonus rpb in gov.focus.resourceBonuses)
                    {
                        if (rpb.resource is null) continue;
                        double mult = SpecUtil.GovernorMultiplierForResource(gov, rpb.resource);
                        if (Math.Abs(mult - 1.0) > 0.001) bonusLineCount++;
                    }
                }
                if (gov.focus.providesBaselineProduction
                    && (gov.focus.resourceBonuses is null || gov.focus.resourceBonuses.Count == 0))
                {
                    double baseMult = 1.0 + gov.focus.baselineProductionValue * score;
                    if (Math.Abs(baseMult - 1.0) > 0.001) bonusLineCount++;
                }
                if (gov.focus.statModifiers is object)
                {
                    foreach (FCStatModifier mod in gov.focus.statModifiers)
                    {
                        if (mod.stat is null) continue;
                        double val = mod.value * score;
                        if (Math.Abs(val) >= 0.0001) bonusLineCount++;
                    }
                }
                innerH += bonusLineCount * 16f;

                Rect scrollOuter = new Rect(ix, iy, iw, scrollAreaH);
                Rect scrollView = ScrollUtil.BeginScrollView(scrollOuter, ref scrollPosFocus, innerH);
                float scrollContentW = scrollView.width;
                float sy = 0f;

                // Description
                if (desc.Length > 0)
                {
                    Text.Font = GameFont.Tiny;
                    Text.Anchor = TextAnchor.UpperLeft;
                    GUI.color = new Color(0.7f, 0.7f, 0.7f);
                    float descH = Text.CalcHeight(desc, scrollContentW);
                    Widgets.Label(new Rect(0f, sy, scrollContentW, descH), desc);
                    sy += descH + 4f;
                    GUI.color = Color.white;
                }

                // Bonus lines
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;

                if (gov.focus.resourceBonuses is object)
                {
                    foreach (ResourceProductionBonus rpb in gov.focus.resourceBonuses)
                    {
                        if (rpb.resource is null) continue;
                        double mult = SpecUtil.GovernorMultiplierForResource(gov, rpb.resource);
                        if (Math.Abs(mult - 1.0) > 0.001)
                        {
                            Widgets.Label(new Rect(0f, sy, scrollContentW, 16f),
                                TextUtil.ColorizeMultiplierBonus(mult) + " " + rpb.resource.LabelCap);
                            sy += 16f;
                        }
                    }
                }

                if (gov.focus.providesBaselineProduction
                    && (gov.focus.resourceBonuses is null || gov.focus.resourceBonuses.Count == 0))
                {
                    double baseMult = 1.0 + gov.focus.baselineProductionValue * score;
                    if (Math.Abs(baseMult - 1.0) > 0.001)
                    {
                        Widgets.Label(new Rect(0f, sy, scrollContentW, 16f),
                            TextUtil.ColorizeMultiplierBonus(baseMult) + " " + "FCS_PickerAllResources".Translate());
                        sy += 16f;
                    }
                }

                if (gov.focus.statModifiers is object)
                {
                    foreach (FCStatModifier mod in gov.focus.statModifiers)
                    {
                        if (mod.stat is null) continue;
                        double val = mod.value * score;
                        if (Math.Abs(val) < 0.0001) continue;

                        TaggedString bonusText;
                        if (mod.stat.aggregation == FCStatAggregation.Multiplicative)
                            bonusText = TextUtil.ColorizeMultiplierBonus(1.0 + val) + " " + mod.stat.LabelCap;
                        else
                            bonusText = TextUtil.ColorizeAdditiveBonus(val) + " " + mod.stat.LabelCap;

                        Widgets.Label(new Rect(0f, sy, scrollContentW, 16f), bonusText);
                        sy += 16f;
                    }
                }

                ScrollUtil.EndScrollView();
                iy += scrollAreaH;
            }
            else
            {
                iy += scrollAreaH;
            }

            if (FCSSettings.RoutesResourcesActive)
            {
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

        private void DrawGovResourceGrid(Rect rect, SettlementGovernor gov, bool hasGov)
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
                if (multSummary is object)
                {
                    Text.Anchor = TextAnchor.MiddleRight;
                    GUI.color = new Color(0.85f, 0.75f, 0.5f);
                    Widgets.Label(new Rect(x, y, w - 5f, GovGridHeaderH), multSummary);
                    GUI.color = Color.white;
                }
            }
            Text.Anchor = TextAnchor.UpperLeft;
            y += GovGridHeaderH + 2f;

            // Gather resource data and cache multipliers
            if (uiSettlement is null) return;

            List<ResourceFC> resources = uiSettlement.Resources.ToList();
            double[] multipliers = new double[resources.Count];
            double maxBonus = 0;
            for (int i = 0; i < resources.Count; i++)
            {
                multipliers[i] = hasGov ? SpecUtil.GovernorMultiplierForResource(gov, resources[i].def) : 1.0;
                double bonus = Math.Abs(multipliers[i] - 1.0);
                if (bonus > maxBonus) maxBonus = bonus;
            }
            if (maxBonus < 0.001) maxBonus = 1.0;

            // Two-column row layout inside a scroll view
            int totalRows = (resources.Count + 1) / 2;
            float contentH = totalRows * GovResRowH;
            float scrollAreaH = rect.yMax - y;

            Rect scrollOuter = new Rect(x, y, w, scrollAreaH);
            Rect scrollView = ScrollUtil.BeginScrollView(scrollOuter, ref scrollPosGovRes, contentH);
            float colW = scrollView.width / 2f;
            int leftCount = (resources.Count + 1) / 2;

            for (int i = 0; i < resources.Count; i++)
            {
                ResourceFC res = resources[i];
                bool isLeft = i < leftCount;
                int row = isLeft ? i : i - leftCount;
                float rowX = isLeft ? 0f : colW;
                float rowY = row * GovResRowH;

                Rect rowRect = new Rect(rowX, rowY, colW, GovResRowH);

                // Alternating row tint
                if (row % 2 == 1)
                {
                    Widgets.DrawHighlight(rowRect);
                }

                bool hasFocusBonus = hasGov && multipliers[i] > 1.001;
                float cx = rowX + 2f;
                float cw = colW - 4f;

                // Resource icon
                GUI.color = hasGov ? Color.white : GovGreyedOut;
                GUI.DrawTexture(new Rect(cx, rowY + (GovResRowH - GovResIconSize) / 2f, GovResIconSize, GovResIconSize), res.def.Icon);
                cx += GovResIconSize + 4f;

                // Resource name
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = hasGov ? new Color(0.75f, 0.75f, 0.75f) : GovGreyedOutText;
                Widgets.Label(new Rect(cx, rowY, GovResNameW, GovResRowH), res.def.LabelCap);
                cx += GovResNameW + 4f;

                // Progress bar
                double bonus = multipliers[i] - 1.0;
                float barProgress = (float)(Math.Abs(bonus) / maxBonus);
                barProgress = Mathf.Clamp01(barProgress);

                float pctW = 42f;
                float barW = rowX + cw - cx - pctW - 4f;
                if (barW < 10f) barW = 10f;
                Rect barRect = new Rect(cx, rowY + (GovResRowH - GovResBarH) / 2f, barW, GovResBarH);
                Color barColor = hasFocusBonus
                    ? new Color(0.85f, 0.75f, 0.3f)
                    : AccentUtil.Income;
                if (!hasGov) barColor = new Color(0.3f, 0.3f, 0.3f, GovGreyedOut.a);
                UIUtil.DrawProgressBarColors(barRect, barProgress, GovBarBg, barColor);
                cx += barW + 4f;

                // Bonus percentage (right-aligned)
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                string bonusText = hasGov
                    ? (bonus >= 0 ? "+" : "") + (bonus * 100).ToString("F1") + "%"
                    : "+0.0%";
                GUI.color = hasGov ? (hasFocusBonus ? new Color(0.85f, 0.75f, 0.3f) : AccentUtil.Income) : GovGreyedOutText;
                Widgets.Label(new Rect(cx, rowY, pctW, GovResRowH), bonusText);
                GUI.color = hasGov ? Color.white : GovGreyedOut;

                // Tooltip
                if (hasGov && Math.Abs(bonus) > 0.001)
                {
                    string tip = BuildGovResourceTooltip(res, gov);
                    if (tip is object) TooltipHandler.TipRegion(rowRect, tip);
                }

                Text.Anchor = TextAnchor.UpperLeft;
            }

            ScrollUtil.EndScrollView();
        }

        // --- Specialists sub-tab ---

        private void DrawSpecialistsTab(Rect contentRect)
        {
            float x = contentRect.x;
            float w = contentRect.width;
            float y = contentRect.y + 4f;

            // Header line 1: Count + Upkeep
            int specCount = comp.SpecialistCount;
            int totalCards = specCount;

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string specHeader = "FCS_SpecHeader".Translate(specCount, comp.MaxSpecialists);
            Widgets.Label(new Rect(x, y, w * 0.6f, SectionHeaderHeight), specHeader);

            double totalUpkeep = 0;
            foreach (SettlementSpecialist s in comp.Specialists)
            {
                totalUpkeep += SpecUtil.SpecialistUpkeep(s);
            }
            Text.Anchor = TextAnchor.MiddleRight;
            Widgets.Label(new Rect(x, y, w, SectionHeaderHeight), "FCS_UpkeepDisplay".Translate(totalUpkeep.ToString("F1")));
            Text.Anchor = TextAnchor.UpperLeft;
            y += SectionHeaderHeight;

            // Header line 2: Specialist death chance on defeat
            {
                float govC, specC, resC, baseGov, baseSpec, baseRes;
                string reductionTip;
                ComputeDeathChances(out govC, out specC, out resC, out baseGov, out baseSpec, out baseRes, out reductionTip);
                DrawDeathChanceLine(new Rect(x, y, w, 18f), baseSpec, specC, reductionTip);
                y += 20f;
            }

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

            Rect scrollView = ScrollUtil.BeginScrollView(scrollOuter, ref scrollPosSpec, innerH);
            float sy = 0f;
            int rowIdx = 0;
            List<SettlementSpecialist> specSnapshot = new List<SettlementSpecialist>(comp.Specialists);
            foreach (SettlementSpecialist s in specSnapshot)
            {
                if (s.pawn is null) continue;

                Rect rowRect = new Rect(0f, sy, scrollView.width, CardHeight);
                DrawSpecialistCard(rowRect, s, true, rowIdx);
                sy += CardHeight + CardGap;
                rowIdx++;
            }
            ScrollUtil.EndScrollView();
        }

        // --- Residents sub-tab ---

        private void DrawResidentsTab(Rect contentRect)
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
            y += SectionHeaderHeight;

            // Resident death chance
            {
                float govC, specC, resC, baseGov, baseSpec, baseRes;
                string reductionTip;
                ComputeDeathChances(out govC, out specC, out resC, out baseGov, out baseSpec, out baseRes, out reductionTip);
                DrawDeathChanceLine(new Rect(x, y, w, 18f), baseRes, resC, reductionTip);
                y += 20f;
            }

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

            Rect scrollView = ScrollUtil.BeginScrollView(scrollOuter, ref scrollPosRes, innerH);
            float sy = 0f;
            int rowIdx = 0;
            List<SettlementSpecialist> resSnapshot = new List<SettlementSpecialist>(comp.Residents);
            foreach (SettlementSpecialist s in resSnapshot)
            {
                if (s.pawn is null) continue;

                Rect rowRect = new Rect(0f, sy, scrollView.width, CardHeight);
                DrawSpecialistCard(rowRect, s, false, rowIdx);
                sy += CardHeight + CardGap;
                rowIdx++;
            }
            ScrollUtil.EndScrollView();
        }

        // --- Shared pawn card drawing ---

        private void DrawSpecialistCard(Rect rowRect, SettlementSpecialist s, bool showContribution, int rowIdx)
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

            // Line 1: Name + Role
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Rect nameRect = new Rect(textX, rowRect.y + 2f, textW, 22f);
            string nameText = s.pawn.LabelShort;
            if (s.role is object)
                nameText += " \u2014 " + s.role.LabelCap;
            Widgets.Label(nameRect, nameText);

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
                    if (contribTip is object)
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
            float btnY = rowRect.y + 10f;

            if (Widgets.ButtonText(new Rect(btnX, btnY, RoleBtnWidth, BtnHeight), "FCS_BtnRole".Translate()))
            {
                ShowRoleChangeMenu(s);
            }
            if (Widgets.ButtonText(new Rect(btnX + RoleBtnWidth + BtnGap, btnY, RecallBtnWidth, BtnHeight), "FCS_BtnRecall".Translate()))
            {
                comp.RecallSpecialist(s);
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
            if (ModsConfig.BiotechActive && pawn.genes?.Xenotype is object)
            {
                parts.Add(pawn.genes.XenotypeLabelCap);
            }
            return string.Join(", ", parts);
        }

        private string BuildContributionTooltip(SettlementSpecialist s)
        {
            if (!s.HasUsableSkills || s.role is null) return null;

            if (uiSettlement is null) return null;

            StringBuilder sb = new StringBuilder();
            double bestBonus = 0;
            string bestLabel = "";
            foreach (ResourceFC resource in uiSettlement.Resources)
            {
                double resBonus = SpecUtil.SpecialistAdditiveForResource(s, resource.def);
                if (resBonus > 0.001)
                {
                    if (sb.Length > 0) sb.AppendLine();
                    sb.AppendLine("FCS_TooltipContribHeader".Translate(resource.def.LabelCap));
                    sb.Append("  +" + resBonus.ToString("F2"));
                    if (resBonus > bestBonus)
                    {
                        bestBonus = resBonus;
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

        private Color GetBestResourceColor(SettlementSpecialist s)
        {
            if (s.role is null || !s.HasUsableSkills || uiSettlement is null) return Color.white;

            // Check if this is a defense-type role (has stat modifiers but no resource bonuses)
            if ((s.role.resourceBonuses is null || s.role.resourceBonuses.Count == 0)
                && s.role.statModifiers is object && s.role.statModifiers.Count > 0)
                return DefenseContribColor;

            double bestBonus = 0;
            Color bestColor = Color.white;
            foreach (ResourceFC resource in uiSettlement.Resources)
            {
                double resBonus = SpecUtil.SpecialistAdditiveForResource(s, resource.def);
                if (resBonus > bestBonus)
                {
                    bestBonus = resBonus;
                    bestColor = resource.def.color;
                }
            }
            return bestColor;
        }

        private string BuildGovResourceTooltip(ResourceFC resource, SettlementGovernor gov)
        {
            if (gov is null || !gov.HasUsableSkills || gov.focus is null) return null;

            double mult = SpecUtil.GovernorMultiplierForResource(gov, resource.def);
            if (Math.Abs(mult - 1.0) < 0.001) return null;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("FCS_GovResTooltipHeader".Translate(resource.def.LabelCap));
            sb.AppendLine("-------------------------");

            sb.AppendLine("FCS_GovResTooltipFocus".Translate(gov.focus.LabelCap));
            sb.AppendLine("FCS_GovResTooltipSkillScore".Translate(gov.SkillScore.ToString("F1")));
            sb.AppendLine("FCS_GovResTooltipFood".Translate(comp.FoodSatisfaction.ToString("F1")));
            sb.AppendLine("-------------------------");

            sb.AppendLine("FCS_GovResTooltipFinal".Translate(((mult - 1.0) * 100).ToString("F1")));
            sb.Append("FCS_GovResTooltipBarNote".Translate());

            return sb.ToString();
        }

        private string BuildGovMultiplierSummary()
        {
            SettlementGovernor gov = comp.Governor;
            if (gov is null || !gov.HasUsableSkills || gov.focus is null || uiSettlement is null)
                return null;

            double bestMult = 0;
            string bestLabel = "";
            foreach (ResourceFC resource in uiSettlement.Resources)
            {
                double mult = SpecUtil.GovernorMultiplierForResource(gov, resource.def);
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

        private void ShowGovernorFocusMenu(SettlementGovernor gov)
        {
            if (gov?.pawn is null) return;

            Find.WindowStack.Add(new Dialog_RolePicker(
                gov.pawn,
                comp,
                onSelectGovernor: focus => comp.ChangeGovernorFocus(focus),
                currentFocus: gov.focus));
        }

        private void ShowRoleChangeMenu(SettlementSpecialist specialist)
        {
            Find.WindowStack.Add(new Dialog_RolePicker(
                specialist.pawn,
                comp,
                onSelectRole: role => comp.ChangeRole(specialist, role),
                onSelectGovernor: focus => comp.PromoteToGovernor(specialist, focus),
                allowGovernor: true,
                currentRole: specialist.role,
                isCurrentlyGovernor: false));
        }

        private string GetTopSkillLabel(SettlementSpecialist s)
        {
            if (!s.HasUsableSkills) return "";
            return SpecUtil.TopRoleSkillsLabel(s.pawn, s.role);
        }

        private string GetTopSkillLabelGovernor(SettlementGovernor g)
        {
            if (!g.HasUsableSkills) return "";
            return SpecUtil.TopFocusSkillsLabel(g.pawn, g.focus);
        }

        private string GetContributionSummary(SettlementSpecialist s)
        {
            if (!s.HasUsableSkills || s.role is null) return "";

            if (uiSettlement is null) return "";

            // Collect all resource bonuses, sorted descending
            List<KeyValuePair<string, double>> bonuses = new List<KeyValuePair<string, double>>();
            foreach (ResourceFC resource in uiSettlement.Resources)
            {
                double resBonus = SpecUtil.SpecialistAdditiveForResource(s, resource.def);
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

            // If no resource bonuses but has stat modifiers, show actual stat values
            if (s.role.statModifiers is object && s.role.statModifiers.Count > 0)
            {
                float score = s.SkillScore;
                StringBuilder statSb = new StringBuilder();
                foreach (FCStatModifier mod in s.role.statModifiers)
                {
                    if (mod.stat is null) continue;
                    double val = mod.value * score;
                    if (Math.Abs(val) < 0.0001) continue;
                    if (statSb.Length > 0) statSb.Append(", ");
                    if (mod.stat.aggregation == FCStatAggregation.Multiplicative)
                        statSb.Append(TextUtil.MultiplierBonusLine(1.0 + val, mod.stat.LabelCap, separator: " "));
                    else
                        statSb.Append(TextUtil.AdditiveBonusLine(val, mod.stat.LabelCap, separator: " "));
                }
                // Append death reduction if role has the Commander's defense behavior
                AppendDeathReduction(statSb, s.role, score);
                if (statSb.Length > 0) return statSb.ToString();
            }

            // Check for death reduction even without stat modifiers
            {
                float score = s.SkillScore;
                StringBuilder defSb = new StringBuilder();
                AppendDeathReduction(defSb, s.role, score);
                if (defSb.Length > 0) return defSb.ToString();
            }

            return "";
        }

        private static void AppendDeathReduction(StringBuilder sb, SpecialistRoleDef role, float skillScore)
        {
            RoleBehaviorExt_Defense defExt = role.GetModExtension<RoleBehaviorExt_Defense>();
            if (defExt is null) return;
            double reduction = skillScore * defExt.baseReductionPerSkillPoint * 100.0;
            if (reduction < 0.01) return;
            if (sb.Length > 0) sb.Append(", ");
            sb.Append(TextUtil.AdditiveBonusLine(-reduction, "FCS_PickerDeathReduction".Translate(), separator: " ", invert: true));
        }

        private void ComputeDeathChances(out float govChance, out float specChance, out float resChance,
            out float baseGov, out float baseSpec, out float baseRes, out string reductionTip)
        {
            WorldObjectCompProperties_SettlementSpecialists props = comp.Props;
            govChance = props.governorDeathChanceDefeat;
            specChance = props.specialistDeathChanceDefeat;
            resChance = props.residentDeathChanceDefeat;
            baseGov = govChance;
            baseSpec = specChance;
            baseRes = resChance;

            StringBuilder tipSb = new StringBuilder();
            foreach (SettlementSpecialist s in comp.Specialists)
            {
                if (s.role is null || !s.HasUsableSkills) continue;
                SpecialistRoleBehavior behavior = s.Behavior;
                if (behavior is null) continue;
                float prevGov = govChance, prevSpec = specChance, prevRes = resChance;
                behavior.ModifyDeathChances(uiSettlement, s.SkillScore,
                    ref govChance, ref specChance, ref resChance);
                // The same reduction is subtracted from all three chances but each is floor-clamped
                // independently, so the largest of the three deltas is the un-clamped reduction.
                float reduction = Mathf.Max(prevGov - govChance, prevSpec - specChance, prevRes - resChance);
                if (reduction > 0.0001f)
                {
                    tipSb.Append("FCS_DeathChanceTipReduction".Translate(
                        s.pawn.LabelShort,
                        Math.Round(reduction * 100, 1).ToString("F1"),
                        s.role.LabelCap));
                }
            }
            reductionTip = tipSb.ToString();
        }

        private void DrawDeathChanceLine(Rect lineRect, float baseChance, float finalChance, string reductionTip)
        {
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            GUI.color = Color.gray;
            string label = "FCS_DeathChanceLine".Translate(Math.Round(finalChance * 100, 1).ToString("F1"));
            Widgets.Label(lineRect, label);

            string tip = "FCS_DeathChanceTipBase".Translate(Math.Round(baseChance * 100, 1).ToString("F1"));
            if (reductionTip.Length > 0)
                tip += reductionTip;
            if (Math.Abs(finalChance - baseChance) > 0.0001f)
                tip += "FCS_DeathChanceTipFinal".Translate(Math.Round(finalChance * 100, 1).ToString("F1"));
            TooltipHandler.TipRegion(lineRect, tip);

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
        }
    }
}
