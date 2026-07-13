using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace FactionColonies.Specialists
{
    public class Dialog_RolePicker : Window
    {
        private readonly Pawn pawn;
        private readonly WorldObjectComp_SettlementSpecialists comp;
        private readonly Action<SpecialistRoleDef> onSelectRole;
        private readonly Action<GovernorFocusDef> onSelectGovernor;
        private readonly bool allowGovernor;
        private readonly SpecialistRoleDef currentRole;
        private readonly bool isCurrentlyGovernor;
        private readonly GovernorFocusDef currentGovernorFocus;
        private readonly bool governorFocusOnly;
        private readonly int pendingSpecialists;
        private readonly Dictionary<SpecialistRoleDef, int> pendingRoleCounts;

        private Vector2 scrollPos;
        private int selectedTab;
        private List<RoleEntry> specialistEntries;
        private List<RoleEntry> governorEntries;

        private const float TitleHeight = 35f;
        private const float TabHeight = 28f;
        private const float TabMarginRight = 2f;
        private const float SeparatorHeight = 1f;
        private const float AccentBarWidth = 4f;
        private const float RowPadding = 8f;
        private const float NameHeight = 22f;
        private const float BonusLineHeight = 16f;
        private const float DescHeight = 18f;
        private const float MinRowHeight = 60f;

        private static readonly Color ResidentAccent = new Color(0.35f, 0.50f, 0.80f);
        private static readonly Color SpecialistAccent = new Color(0.45f, 0.75f, 0.35f);
        private static readonly Color GovernorAccent = new Color(0.85f, 0.75f, 0.5f);
        private static readonly Color DisabledColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color SeparatorColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        private static readonly Color CurrentHighlight = new Color(1f, 1f, 1f, 0.08f);

        private static readonly string[] tabLabels =
        {
            "FCS_SubSpecialists".Translate(),
            "FCS_SubGovernor".Translate()
        };
        private static readonly Color[] tabColors =
        {
            SpecialistAccent,
            GovernorAccent
        };

        public override Vector2 InitialSize
        {
            get { return new Vector2(520f, 580f); }
        }

        /// <summary>
        /// Full role picker: Resident, Specialist roles, Governor focuses.
        /// </summary>
        public Dialog_RolePicker(
            Pawn pawn,
            WorldObjectComp_SettlementSpecialists comp,
            Action<SpecialistRoleDef> onSelectRole,
            Action<GovernorFocusDef> onSelectGovernor,
            bool allowGovernor,
            SpecialistRoleDef currentRole,
            bool isCurrentlyGovernor,
            GovernorFocusDef currentGovernorFocus = null,
            int pendingSpecialists = 0,
            Dictionary<SpecialistRoleDef, int> pendingRoleCounts = null)
        {
            this.pawn = pawn;
            this.comp = comp;
            this.onSelectRole = onSelectRole;
            this.onSelectGovernor = onSelectGovernor;
            this.allowGovernor = allowGovernor;
            this.currentRole = currentRole;
            this.isCurrentlyGovernor = isCurrentlyGovernor;
            this.currentGovernorFocus = currentGovernorFocus;
            this.pendingSpecialists = pendingSpecialists;
            this.pendingRoleCounts = pendingRoleCounts;
            this.governorFocusOnly = false;
            this.selectedTab = isCurrentlyGovernor ? 1 : 0;

            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;

            BuildEntries();
        }

        /// <summary>
        /// Governor-focus-only picker (for changing focus of an existing governor).
        /// </summary>
        public Dialog_RolePicker(
            Pawn pawn,
            WorldObjectComp_SettlementSpecialists comp,
            Action<GovernorFocusDef> onSelectGovernor,
            GovernorFocusDef currentFocus)
        {
            this.pawn = pawn;
            this.comp = comp;
            this.onSelectRole = null;
            this.onSelectGovernor = onSelectGovernor;
            this.allowGovernor = true;
            this.currentRole = null;
            this.isCurrentlyGovernor = true;
            this.currentGovernorFocus = currentFocus;
            this.governorFocusOnly = true;
            this.selectedTab = 0;

            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;

            BuildEntries();
        }

        private void BuildEntries()
        {
            specialistEntries = new List<RoleEntry>();
            governorEntries = new List<RoleEntry>();

            if (!governorFocusOnly)
            {
                // Resident entry
                bool isResidentCurrent = !isCurrentlyGovernor && currentRole is null;
                RoleEntry residentEntry = new RoleEntry
                {
                    type = RoleEntryType.Resident,
                    label = "FCS_RoleResident".Translate(),
                    description = "FCS_PickerResidentDesc".Translate(),
                    accent = ResidentAccent,
                    isCurrent = isResidentCurrent,
                    enabled = true,
                    sortKey = -2f
                };
                residentEntry.bonusLines = GetBonusLines(residentEntry);
                specialistEntries.Add(residentEntry);

                // Specialist roles
                foreach (SpecialistRoleDef roleDef in DefDatabase<SpecialistRoleDef>.AllDefs)
                {
                    bool isCurrent = !isCurrentlyGovernor && currentRole == roleDef;
                    string disabledReason = null;

                    // Cap checks
                    if (!isCurrent)
                    {
                        bool isAlreadySpecialist = !isCurrentlyGovernor && currentRole is object;
                        if (!isAlreadySpecialist && comp.SpecialistCount + pendingSpecialists >= comp.MaxSpecialists)
                        {
                            disabledReason = "FCS_RoleCapReached".Translate(roleDef.LabelCap);
                        }

                        if (disabledReason is null && roleDef.maxPerSettlement > 0)
                        {
                            int existing = 0;
                            foreach (SettlementSpecialist s in comp.Specialists)
                                if (s.role == roleDef) existing++;
                            if (pendingRoleCounts is object)
                            {
                                pendingRoleCounts.TryGetValue(roleDef, out int pendingForRole);
                                existing += pendingForRole;
                            }
                            if (existing >= roleDef.maxPerSettlement)
                            {
                                disabledReason = "FCS_PickerPerSettlementCap".Translate(
                                    roleDef.LabelCap, roleDef.maxPerSettlement);
                            }
                        }
                    }

                    RoleEntry specEntry = new RoleEntry
                    {
                        type = RoleEntryType.Specialist,
                        roleDef = roleDef,
                        label = roleDef.LabelCap,
                        description = roleDef.description ?? "",
                        accent = SpecialistAccent,
                        isCurrent = isCurrent,
                        enabled = disabledReason is null,
                        disabledReason = disabledReason
                    };
                    specEntry.bonusLines = GetBonusLines(specEntry);
                    specEntry.skillLine = SpecUtil.TopRoleSkillsLabel(pawn, roleDef);

                    // Sort key: Generalist always second, then by best bonus descending, no-bonus at bottom
                    if (roleDef.isGeneralist)
                    {
                        specEntry.sortKey = -1f;
                    }
                    else if (specEntry.bonusLines.Count > 0)
                    {
                        specEntry.sortKey = -specEntry.bestBonusMagnitude;
                    }
                    else
                    {
                        specEntry.sortKey = 1000f;
                    }

                    specialistEntries.Add(specEntry);
                }

                // Sort specialist entries (Resident stays first via sortKey=-2)
                specialistEntries.Sort((a, b) =>
                {
                    int cmp = a.sortKey.CompareTo(b.sortKey);
                    if (cmp != 0) return cmp;
                    return string.Compare(a.label, b.label, StringComparison.Ordinal);
                });
            }

            // Governor focus entries
            if (allowGovernor || governorFocusOnly)
            {
                foreach (GovernorFocusDef focusDef in DefDatabase<GovernorFocusDef>.AllDefs)
                {
                    bool isCurrent = isCurrentlyGovernor && currentGovernorFocus == focusDef;

                    RoleEntry govEntry = new RoleEntry
                    {
                        type = RoleEntryType.GovernorFocus,
                        focusDef = focusDef,
                        label = focusDef.LabelCap,
                        description = focusDef.description ?? "",
                        accent = GovernorAccent,
                        isCurrent = isCurrent,
                        enabled = true
                    };
                    govEntry.bonusLines = GetBonusLines(govEntry);
                    govEntry.skillLine = SpecUtil.TopFocusSkillsLabel(pawn, focusDef);
                    governorEntries.Add(govEntry);
                }
            }
        }

        private List<RoleEntry> ActiveEntries
        {
            get
            {
                if (governorFocusOnly) return governorEntries;
                return selectedTab == 0 ? specialistEntries : governorEntries;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            Color prevColor = GUI.color;

            // Title
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            string title = governorFocusOnly
                ? "FCS_PickerFocusTitle".Translate()
                : "FCS_PickerRoleTitle".Translate(pawn.LabelShort);
            Widgets.Label(new Rect(0, 0, inRect.width, TitleHeight), title);

            float listTop = TitleHeight + 4f;

            // Tab bar (skip if governorFocusOnly)
            if (!governorFocusOnly)
            {
                float tabAreaW = inRect.width - TabMarginRight;
                float tabW = tabAreaW / 2f;
                Rect chosenRect = new Rect();
                for (int i = 0; i < 2; i++)
                {
                    Rect tabRect = new Rect(tabW * i, listTop, tabW, TabHeight);
                    if (UIUtil.ButtonFlat(tabRect, tabLabels[i], highlighted: selectedTab == i))
                    {
                        if (selectedTab != i)
                        {
                            selectedTab = i;
                            scrollPos = Vector2.zero;
                        }
                    }
                    if (selectedTab == i)
                        chosenRect = tabRect;
                }
                UIUtil.DrawColoredHighlight(chosenRect, tabColors[selectedTab]);
                UIUtil.DrawTabDecoratorHorizontalTop(chosenRect, 0f, tabAreaW, tabColors[selectedTab]);
                listTop += TabHeight;
            }

            // Scroll view
            List<RoleEntry> entries = ActiveEntries;
            float listHeight = inRect.height - listTop;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);

            float contentWidth = scrollOutRect.width - (ScrollUtil.ScrollbarWidth + 1f);
            float totalHeight = 0f;
            foreach (RoleEntry entry in entries)
            {
                totalHeight += GetRowHeight(entry, contentWidth) + SeparatorHeight;
            }

            Rect scrollViewRect = ScrollUtil.BeginScrollView(scrollOutRect, ref scrollPos, totalHeight);
            contentWidth = scrollViewRect.width;

            float curY = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                RoleEntry entry = entries[i];
                float rowHeight = GetRowHeight(entry, contentWidth);
                Rect rowRect = new Rect(0, curY, contentWidth, rowHeight);

                // Visibility culling
                if (rowRect.yMax >= scrollPos.y && rowRect.y <= scrollPos.y + listHeight)
                {
                    DrawRow(rowRect, entry, i);
                }

                curY += rowHeight;

                // Separator
                if (i < entries.Count - 1)
                {
                    GUI.color = SeparatorColor;
                    Widgets.DrawLineHorizontal(AccentBarWidth + RowPadding, curY, contentWidth - AccentBarWidth - RowPadding * 2);
                    GUI.color = Color.white;
                    curY += SeparatorHeight;
                }
            }

            ScrollUtil.EndScrollView();

            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.color = prevColor;
        }

        private float GetRowHeight(RoleEntry entry, float contentWidth)
        {
            float textWidth = contentWidth - AccentBarWidth - RowPadding * 3;

            // Name line
            float height = RowPadding + NameHeight;

            // Description
            if (entry.description.Length > 0)
            {
                GameFont prevFont = Text.Font;
                Text.Font = GameFont.Tiny;
                float descH = Text.CalcHeight(entry.description, textWidth);
                Text.Font = prevFont;
                height += descH + 2f;
            }

            // Skill line
            if (!string.IsNullOrEmpty(entry.skillLine))
            {
                height += BonusLineHeight + 2f;
            }

            // Bonus lines
            int lineCount = entry.bonusLines is object ? entry.bonusLines.Count : 0;
            if (lineCount > 0)
            {
                height += lineCount * BonusLineHeight + 2f;
            }
            else
            {
                height += BonusLineHeight + 2f; // "No production bonus" line
            }

            // Disabled reason
            if (!entry.enabled && entry.disabledReason is object)
            {
                height += DescHeight;
            }

            height += RowPadding;

            return Mathf.Max(height, MinRowHeight);
        }

        private void DrawRow(Rect rect, RoleEntry entry, int index)
        {
            // Background
            if (entry.isCurrent)
            {
                Widgets.DrawBoxSolid(rect, CurrentHighlight);
            }
            else if (entry.enabled && Mouse.IsOver(rect))
            {
                Widgets.DrawHighlightSelected(rect);
            }
            else if (index % 2 == 0)
            {
                Widgets.DrawLightHighlight(rect);
            }

            // Accent bar
            Color accentColor = entry.enabled ? entry.accent : DisabledColor;
            if (entry.isCurrent)
            {
                accentColor.a = 1f;
            }
            else
            {
                accentColor.a = entry.enabled ? 0.6f : 0.3f;
            }
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, AccentBarWidth, rect.height), accentColor);

            float textX = rect.x + AccentBarWidth + RowPadding;
            float textW = rect.width - AccentBarWidth - RowPadding * 3;
            float curY = rect.y + RowPadding;

            if (!entry.enabled)
                GUI.color = DisabledColor;

            // Role name
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            string nameLabel = entry.label;
            if (entry.isCurrent) nameLabel += " *";
            Widgets.Label(new Rect(textX, curY, textW, NameHeight), nameLabel);

            // Upkeep on right side of name row
            string upkeepText = GetUpkeepText(entry);
            if (upkeepText is object)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = entry.enabled ? Color.gray : DisabledColor;
                Widgets.Label(new Rect(textX, curY, textW, NameHeight), upkeepText);
                if (!entry.enabled) GUI.color = DisabledColor;
            }
            curY += NameHeight;

            // Description
            if (entry.description.Length > 0)
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                GUI.color = entry.enabled ? Color.gray : DisabledColor;
                float descH = Text.CalcHeight(entry.description, textW);
                Widgets.Label(new Rect(textX, curY, textW, descH), entry.description);
                curY += descH + 2f;
            }

            // Skill line
            if (!string.IsNullOrEmpty(entry.skillLine))
            {
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.MiddleLeft;
                GUI.color = entry.enabled ? Color.white : DisabledColor;
                Widgets.Label(new Rect(textX, curY, textW, BonusLineHeight), entry.skillLine);
                curY += BonusLineHeight + 2f;
            }

            // Bonus lines
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            if (entry.bonusLines is object && entry.bonusLines.Count > 0)
            {
                GUI.color = entry.enabled ? Color.white : DisabledColor;
                foreach (TaggedString line in entry.bonusLines)
                {
                    Widgets.Label(new Rect(textX, curY, textW, BonusLineHeight), line);
                    curY += BonusLineHeight;
                }
                curY += 2f;
            }
            else
            {
                GUI.color = entry.enabled ? Color.gray : DisabledColor;
                Widgets.Label(new Rect(textX, curY, textW, BonusLineHeight),
                    (string)"FCS_PreviewNoBonus".Translate());
                curY += BonusLineHeight + 2f;
            }

            // Disabled reason
            if (!entry.enabled && entry.disabledReason is object)
            {
                GUI.color = new Color(0.8f, 0.2f, 0.2f);
                Text.Font = GameFont.Tiny;
                Text.Anchor = TextAnchor.UpperLeft;
                Widgets.Label(new Rect(textX, curY, textW, DescHeight), entry.disabledReason);
            }

            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // Click handling
            if (entry.enabled && Widgets.ButtonInvisible(rect))
            {
                SelectEntry(entry);
                Close();
            }
        }

        private void SelectEntry(RoleEntry entry)
        {
            switch (entry.type)
            {
                case RoleEntryType.Resident:
                    if (onSelectRole is object) onSelectRole(null);
                    break;
                case RoleEntryType.Specialist:
                    if (onSelectRole is object) onSelectRole(entry.roleDef);
                    break;
                case RoleEntryType.GovernorFocus:
                    if (onSelectGovernor is object) onSelectGovernor(entry.focusDef);
                    break;
            }
        }

        private List<TaggedString> GetBonusLines(RoleEntry entry)
        {
            List<TaggedString> lines = new List<TaggedString>();
            WorldSettlementFC settlement = comp.Settlement;

            if (entry.type == RoleEntryType.Resident)
            {
                lines.Add("FCS_PickerResidentPreview".Translate());
                return lines;
            }

            if (entry.type == RoleEntryType.Specialist && entry.roleDef is object)
            {
                SettlementSpecialist tempSpec = new SettlementSpecialist(pawn, entry.roleDef);
                float score = tempSpec.SkillScore;

                // Resource bonuses
                if (settlement is object && entry.roleDef.resourceBonuses is object)
                {
                    foreach (ResourceProductionBonus rpb in entry.roleDef.resourceBonuses)
                    {
                        if (rpb.resource is null) continue;
                        double bonus = SpecUtil.SpecialistAdditiveForResource(tempSpec, rpb.resource);
                        if (Math.Abs(bonus) > 0.001)
                        {
                            lines.Add(TextUtil.ColorizeAdditiveBonus(bonus) + " " + rpb.resource.LabelCap);
                            if (bonus > entry.bestBonusMagnitude)
                                entry.bestBonusMagnitude = (float)bonus;
                        }
                    }
                }

                // Generalist baseline (shows for all resources)
                if (entry.roleDef.providesBaselineProduction && settlement is object
                    && (entry.roleDef.resourceBonuses is null || entry.roleDef.resourceBonuses.Count == 0))
                {
                    double baseline = entry.roleDef.baselineProductionValue * score;
                    if (Math.Abs(baseline) > 0.001)
                    {
                        lines.Add(TextUtil.ColorizeAdditiveBonus(baseline) + " " + "FCS_PickerAllResources".Translate());
                        if (baseline > entry.bestBonusMagnitude)
                            entry.bestBonusMagnitude = (float)baseline;
                    }
                }

                // Stat modifiers
                if (entry.roleDef.statModifiers is object)
                {
                    foreach (FCStatModifier mod in entry.roleDef.statModifiers)
                    {
                        if (mod.stat is null) continue;
                        double val = mod.value * score;
                        if (Math.Abs(val) < 0.0001) continue;

                        if (mod.stat.aggregation == FCStatAggregation.Multiplicative)
                        {
                            lines.Add(TextUtil.ColorizeMultiplierBonus(1.0 + val) + " " + mod.stat.LabelCap);
                        }
                        else
                        {
                            lines.Add(TextUtil.ColorizeAdditiveBonus(val) + " " + mod.stat.LabelCap);
                        }

                        if (Math.Abs(val) > entry.bestBonusMagnitude)
                            entry.bestBonusMagnitude = (float)Math.Abs(val);
                    }
                }

                // Death reduction from defense behavior
                RoleBehaviorExt_Defense defExt = entry.roleDef.GetModExtension<RoleBehaviorExt_Defense>();
                if (defExt is object)
                {
                    double reduction = score * defExt.baseReductionPerSkillPoint * 100.0;
                    if (reduction >= 0.01)
                    {
                        lines.Add(TextUtil.ColorizeAdditiveBonus(-reduction, invert: true)
                            + " " + "FCS_PickerDeathReduction".Translate());
                        if (reduction > entry.bestBonusMagnitude)
                            entry.bestBonusMagnitude = (float)reduction;
                    }
                }

                return lines;
            }

            if (entry.type == RoleEntryType.GovernorFocus && entry.focusDef is object)
            {
                SettlementGovernor tempGov = new SettlementGovernor(pawn, entry.focusDef);
                float score = tempGov.SkillScore;

                // Resource multipliers
                if (settlement is object && entry.focusDef.resourceBonuses is object)
                {
                    foreach (ResourceProductionBonus rpb in entry.focusDef.resourceBonuses)
                    {
                        if (rpb.resource is null) continue;
                        double mult = SpecUtil.GovernorMultiplierForResource(tempGov, rpb.resource);
                        if (Math.Abs(mult - 1.0) > 0.001)
                        {
                            lines.Add(TextUtil.ColorizeMultiplierBonus(mult) + " " + rpb.resource.LabelCap);
                            if (Math.Abs(mult - 1.0) > entry.bestBonusMagnitude)
                                entry.bestBonusMagnitude = (float)Math.Abs(mult - 1.0);
                        }
                    }
                }

                // Baseline production (shows for all resources)
                if (entry.focusDef.providesBaselineProduction
                    && (entry.focusDef.resourceBonuses is null || entry.focusDef.resourceBonuses.Count == 0))
                {
                    double baseMult = 1.0 + entry.focusDef.baselineProductionValue * score;
                    if (Math.Abs(baseMult - 1.0) > 0.001)
                    {
                        lines.Add(TextUtil.ColorizeMultiplierBonus(baseMult) + " " + "FCS_PickerAllResources".Translate());
                        if (Math.Abs(baseMult - 1.0) > entry.bestBonusMagnitude)
                            entry.bestBonusMagnitude = (float)Math.Abs(baseMult - 1.0);
                    }
                }

                // Stat modifiers
                if (entry.focusDef.statModifiers is object)
                {
                    foreach (FCStatModifier mod in entry.focusDef.statModifiers)
                    {
                        if (mod.stat is null) continue;
                        double val = mod.value * score;
                        if (Math.Abs(val) < 0.0001) continue;

                        if (mod.stat.aggregation == FCStatAggregation.Multiplicative)
                        {
                            lines.Add(TextUtil.ColorizeMultiplierBonus(1.0 + val) + " " + mod.stat.LabelCap);
                        }
                        else
                        {
                            lines.Add(TextUtil.ColorizeAdditiveBonus(val) + " " + mod.stat.LabelCap);
                        }

                        if (Math.Abs(val) > entry.bestBonusMagnitude)
                            entry.bestBonusMagnitude = (float)Math.Abs(val);
                    }
                }

                return lines;
            }

            return lines;
        }

        private string GetUpkeepText(RoleEntry entry)
        {
            if (entry.type == RoleEntryType.Resident)
                return null;

            if (entry.type == RoleEntryType.Specialist && entry.roleDef is object)
            {
                SettlementSpecialist tempSpec = new SettlementSpecialist(pawn, entry.roleDef);
                double upkeep = SpecUtil.SpecialistUpkeep(tempSpec);
                return "FCS_PickerUpkeep".Translate(upkeep.ToString("F1"));
            }

            if (entry.type == RoleEntryType.GovernorFocus && entry.focusDef is object)
            {
                double upkeep = SpecUtil.GovernorUpkeep(new SettlementGovernor(pawn, entry.focusDef));
                return "FCS_PickerUpkeep".Translate(upkeep.ToString("F1"));
            }

            return null;
        }

        private enum RoleEntryType
        {
            Resident,
            Specialist,
            GovernorFocus
        }

        private class RoleEntry
        {
            public RoleEntryType type;
            public SpecialistRoleDef roleDef;
            public GovernorFocusDef focusDef;
            public string label;
            public string description;
            public Color accent;
            public bool isCurrent;
            public bool enabled = true;
            public string disabledReason;
            public float sortKey;
            public float bestBonusMagnitude;
            public List<TaggedString> bonusLines;
            public string skillLine;
        }
    }
}
