using System;
using System.Collections.Generic;
using System.Linq;
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

        private Vector2 scrollPos;
        private List<RoleEntry> entries;

        private const float TitleHeight = 35f;
        private const float SeparatorHeight = 1f;
        private const float AccentBarWidth = 4f;
        private const float RowPadding = 8f;
        private const float NameHeight = 22f;
        private const float DescHeight = 18f;
        private const float PreviewHeight = 18f;
        private const float MinRowHeight = 60f;

        private static readonly Color ResidentAccent = new Color(0.35f, 0.50f, 0.80f);
        private static readonly Color SpecialistAccent = new Color(0.45f, 0.75f, 0.35f);
        private static readonly Color GovernorAccent = new Color(0.85f, 0.75f, 0.5f);
        private static readonly Color DisabledColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color SeparatorColor = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        private static readonly Color CurrentHighlight = new Color(1f, 1f, 1f, 0.08f);

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
            GovernorFocusDef currentGovernorFocus = null)
        {
            this.pawn = pawn;
            this.comp = comp;
            this.onSelectRole = onSelectRole;
            this.onSelectGovernor = onSelectGovernor;
            this.allowGovernor = allowGovernor;
            this.currentRole = currentRole;
            this.isCurrentlyGovernor = isCurrentlyGovernor;
            this.currentGovernorFocus = currentGovernorFocus;
            this.governorFocusOnly = false;

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

            draggable = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
            forcePause = false;

            BuildEntries();
        }

        private void BuildEntries()
        {
            entries = new List<RoleEntry>();

            if (!governorFocusOnly)
            {
                // Resident entry
                bool isResidentCurrent = !isCurrentlyGovernor && currentRole is null;
                entries.Add(new RoleEntry
                {
                    type = RoleEntryType.Resident,
                    label = "FCS_RoleResident".Translate(),
                    description = "FCS_PickerResidentDesc".Translate(),
                    accent = ResidentAccent,
                    isCurrent = isResidentCurrent,
                    enabled = true
                });

                // Specialist roles
                foreach (SpecialistRoleDef roleDef in DefDatabase<SpecialistRoleDef>.AllDefs
                    .OrderBy(d => d.LabelCap.ToString()))
                {
                    bool isCurrent = !isCurrentlyGovernor && currentRole == roleDef;
                    string disabledReason = null;

                    // Cap checks
                    if (!isCurrent)
                    {
                        bool isAlreadySpecialist = !isCurrentlyGovernor && currentRole is object;
                        if (!isAlreadySpecialist && comp.SpecialistCount >= comp.MaxSpecialists)
                        {
                            disabledReason = "FCS_RoleCapReached".Translate(roleDef.LabelCap);
                        }

                        if (disabledReason is null && roleDef.maxPerSettlement > 0)
                        {
                            int existing = 0;
                            foreach (SettlementSpecialist s in comp.Specialists)
                                if (s.role == roleDef) existing++;
                            if (existing >= roleDef.maxPerSettlement)
                            {
                                disabledReason = "FCS_PickerPerSettlementCap".Translate(
                                    roleDef.LabelCap, roleDef.maxPerSettlement);
                            }
                        }
                    }

                    entries.Add(new RoleEntry
                    {
                        type = RoleEntryType.Specialist,
                        roleDef = roleDef,
                        label = roleDef.LabelCap,
                        description = roleDef.description ?? "",
                        accent = SpecialistAccent,
                        isCurrent = isCurrent,
                        enabled = disabledReason is null,
                        disabledReason = disabledReason
                    });
                }

                // Governor section separator
                if (allowGovernor)
                {
                    entries.Add(new RoleEntry
                    {
                        type = RoleEntryType.SectionHeader,
                        label = "FCS_RoleGovernor".Translate(),
                        accent = GovernorAccent
                    });
                }
            }

            // Governor focus entries
            if (allowGovernor)
            {
                foreach (GovernorFocusDef focusDef in DefDatabase<GovernorFocusDef>.AllDefs)
                {
                    bool isCurrent = isCurrentlyGovernor && currentGovernorFocus == focusDef;

                    entries.Add(new RoleEntry
                    {
                        type = RoleEntryType.GovernorFocus,
                        focusDef = focusDef,
                        label = focusDef.LabelCap,
                        description = focusDef.description ?? "",
                        accent = GovernorAccent,
                        isCurrent = isCurrent,
                        enabled = true
                    });
                }
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

            // Scroll view
            float listTop = TitleHeight + 4f;
            float listHeight = inRect.height - listTop;
            Rect scrollOutRect = new Rect(0, listTop, inRect.width, listHeight);

            float contentWidth = scrollOutRect.width - 16f;
            float totalHeight = 0f;
            foreach (RoleEntry entry in entries)
            {
                totalHeight += GetRowHeight(entry, contentWidth) + SeparatorHeight;
            }

            Rect scrollViewRect = new Rect(0f, 0f, contentWidth, Mathf.Max(totalHeight, listHeight));
            Widgets.BeginScrollView(scrollOutRect, ref scrollPos, scrollViewRect);

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
                if (i < entries.Count - 1 && entry.type != RoleEntryType.SectionHeader)
                {
                    GUI.color = SeparatorColor;
                    Widgets.DrawLineHorizontal(AccentBarWidth + RowPadding, curY, contentWidth - AccentBarWidth - RowPadding * 2);
                    GUI.color = Color.white;
                    curY += SeparatorHeight;
                }
            }

            Widgets.EndScrollView();

            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.color = prevColor;
        }

        private float GetRowHeight(RoleEntry entry, float contentWidth)
        {
            if (entry.type == RoleEntryType.SectionHeader)
                return 28f;

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

            // Preview line
            height += PreviewHeight + 2f;

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
            if (entry.type == RoleEntryType.SectionHeader)
            {
                DrawSectionHeader(rect, entry);
                return;
            }

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

            // Preview
            GUI.color = entry.enabled ? Color.white : DisabledColor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            string preview;
            Color previewColor;
            GetPreview(entry, out preview, out previewColor);
            GUI.color = entry.enabled ? previewColor : DisabledColor;
            Widgets.Label(new Rect(textX, curY, textW, PreviewHeight), preview);
            curY += PreviewHeight + 2f;

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

        private void DrawSectionHeader(Rect rect, RoleEntry entry)
        {
            GUI.color = entry.accent;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.DrawBoxSolid(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), entry.accent);
            Widgets.Label(new Rect(rect.x + RowPadding, rect.y, rect.width - RowPadding * 2, rect.height), entry.label);
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;
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

        private void GetPreview(RoleEntry entry, out string text, out Color color)
        {
            WorldSettlementFC settlement = comp.Settlement;

            if (entry.type == RoleEntryType.Resident)
            {
                text = "FCS_PickerResidentPreview".Translate();
                color = ResidentAccent;
                return;
            }

            if (entry.type == RoleEntryType.Specialist && entry.roleDef is object)
            {
                SettlementSpecialist tempSpec = new SettlementSpecialist(pawn, entry.roleDef);
                double bestBonus = 0;
                string bestLabel = "";
                Color bestColor = Color.white;

                if (settlement is object)
                {
                    foreach (ResourceFC resource in settlement.Resources)
                    {
                        double bonus = SpecUtil.SpecialistAdditiveForResource(tempSpec, resource.def);
                        if (bonus > bestBonus)
                        {
                            bestBonus = bonus;
                            bestLabel = resource.def.LabelCap;
                            bestColor = resource.def.color;
                        }
                    }
                }

                if (bestBonus > 0)
                {
                    text = "FCS_ContribProduction".Translate(bestBonus.ToString("F2"), bestLabel);
                    color = bestColor;
                    return;
                }

                // Check stat modifiers
                if (entry.roleDef.statModifiers is object && entry.roleDef.statModifiers.Count > 0)
                {
                    float score = tempSpec.SkillScore;
                    FCStatModifier best = entry.roleDef.statModifiers[0];
                    double bestVal = best.value * score;
                    text = "FCS_PickerStatPreview".Translate(
                        best.stat.LabelCap, (bestVal * 100).ToString("F1"));
                    color = SpecialistAccent;
                    return;
                }

                text = "FCS_PreviewNoBonus".Translate();
                color = Color.gray;
                return;
            }

            if (entry.type == RoleEntryType.GovernorFocus && entry.focusDef is object)
            {
                SettlementGovernor tempGov = new SettlementGovernor(pawn, entry.focusDef);
                double bestMult = 0;
                string bestLabel = "";

                if (settlement is object)
                {
                    foreach (ResourceFC resource in settlement.Resources)
                    {
                        double mult = SpecUtil.GovernorMultiplierForResource(tempGov, resource.def);
                        if (mult > bestMult)
                        {
                            bestMult = mult;
                            bestLabel = resource.def.LabelCap;
                        }
                    }
                }

                if (bestMult > 1.001)
                {
                    text = "FCS_PreviewGovMult".Translate(bestMult.ToString("F2"), bestLabel);
                    color = GovernorAccent;
                    return;
                }

                // Check stat modifiers
                if (entry.focusDef.statModifiers is object && entry.focusDef.statModifiers.Count > 0)
                {
                    float score = tempGov.SkillScore;
                    FCStatModifier best = entry.focusDef.statModifiers[0];
                    double bestVal = best.value * score;
                    text = "FCS_PickerStatPreview".Translate(
                        best.stat.LabelCap, (bestVal * 100).ToString("F1"));
                    color = GovernorAccent;
                    return;
                }

                text = "FCS_PreviewNoBonus".Translate();
                color = Color.gray;
                return;
            }

            text = "FCS_PreviewNoBonus".Translate();
            color = Color.gray;
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

            if (entry.type == RoleEntryType.GovernorFocus)
            {
                double upkeep = SpecUtil.GovernorUpkeep();
                return "FCS_PickerUpkeep".Translate(upkeep.ToString("F1"));
            }

            return null;
        }

        private enum RoleEntryType
        {
            Resident,
            Specialist,
            GovernorFocus,
            SectionHeader
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
        }
    }
}
