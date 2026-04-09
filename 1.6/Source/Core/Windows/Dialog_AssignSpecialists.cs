using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace FactionColonies.Specialists
{
    public class Dialog_AssignSpecialists : Window
    {
        private readonly Caravan caravan;
        private readonly WorldObjectComp_SettlementSpecialists comp;
        private readonly List<PawnEntry> entries = new List<PawnEntry>();
        private Vector2 scrollPos;

        private const float CardRowHeight = 80f;
        private const float PortraitSize = 68f;
        private const float CardPadding = 4f;
        private const float CardGap = 2f;
        private const float RoleBtnWidth = 120f;
        private const float RoleBtnHeight = 24f;
        private const float CheckboxSize = 24f;
        private const float AccentBarWidth = 3f;
        private const float margin = 10f;
        private static readonly Color SelectedAccent = new Color(0.3f, 0.7f, 0.3f, 0.5f);
        private static readonly Color DefenseColor = new Color(0.9f, 0.4f, 0.4f);
        private static readonly Color GovColor = new Color(0.85f, 0.75f, 0.5f);

        public override Vector2 InitialSize
        {
            get { return new Vector2(600f, 500f); }
        }

        public Dialog_AssignSpecialists(Caravan caravan, WorldObjectComp_SettlementSpecialists comp)
        {
            this.caravan = caravan;
            this.comp = comp;
            forcePause = true;
            absorbInputAroundWindow = true;
            doCloseX = true;
            closeOnAccept = false;
            closeOnCancel = true;

            foreach (Pawn pawn in caravan.PawnsListForReading)
            {
                if (IsEligible(pawn))
                {
                    entries.Add(new PawnEntry(pawn));
                }
            }
        }

        private bool IsEligible(Pawn pawn)
        {
            if (pawn == null) return false;
            if (!pawn.RaceProps.Humanlike) return false;
            if (pawn.Downed) return false;
            if (pawn.Dead) return false;
            if (pawn.DevelopmentalStage != DevelopmentalStage.Adult) return false;
            return true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            bool prevWrap = Text.WordWrap;
            Color prevColor = GUI.color;

            // Title
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            Widgets.Label(titleRect, "FCS_AssignTitle".Translate(comp.Settlement.Name));
            Text.Font = GameFont.Small;

            float headerY = inRect.y + 45f;
            float buttonHeight = 35f;
            float listHeight = inRect.height - 45f - buttonHeight - margin;

            Rect listOuterRect = new Rect(inRect.x, headerY, inRect.width, listHeight);
            float totalHeight = entries.Count * (CardRowHeight + CardGap);
            float scrollBarW = totalHeight > listHeight ? 16f : 0f;
            Rect listInnerRect = new Rect(0f, 0f, listOuterRect.width - scrollBarW, totalHeight);

            bool hasGovernor = comp.HasGovernor;
            int pendingNonResident = entries.Count(e => e.selected && e.role is object && !e.isGovernor);
            bool capReached = (comp.SpecialistCount + pendingNonResident) >= comp.MaxSpecialists;

            Widgets.BeginScrollView(listOuterRect, ref scrollPos, listInnerRect);

            float curY = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                PawnEntry entry = entries[i];
                Rect rowRect = new Rect(0f, curY, listInnerRect.width, CardRowHeight);

                DrawPawnCard(rowRect, entry, i, hasGovernor, capReached);

                curY += CardRowHeight + CardGap;
            }

            Widgets.EndScrollView();

            // Confirm button
            Rect confirmRect = new Rect(inRect.x + inRect.width - 150f, inRect.yMax - buttonHeight, 150f, buttonHeight);
            int selectedCount = entries.Count(e => e.selected);

            if (selectedCount == 0)
            {
                GUI.color = Color.gray;
            }

            if (Widgets.ButtonText(confirmRect, "FCS_ConfirmCount".Translate(selectedCount)) && selectedCount > 0)
            {
                AssignSelected();
                Close();
            }

            GUI.color = prevColor;
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            Text.WordWrap = prevWrap;
        }

        private void DrawPawnCard(Rect rowRect, PawnEntry entry, int rowIdx, bool hasGovernor, bool capReached)
        {
            // Alternating background
            if (rowIdx % 2 == 0) Widgets.DrawLightHighlight(rowRect);

            // Selection accent bar
            if (entry.selected)
            {
                Widgets.DrawBoxSolid(new Rect(rowRect.x, rowRect.y, AccentBarWidth, rowRect.height), SelectedAccent);
            }

            // Portrait
            float portraitX = rowRect.x + CardPadding + AccentBarWidth;
            float portraitY = rowRect.y + (CardRowHeight - PortraitSize) / 2f;
            Rect portraitRect = new Rect(portraitX, portraitY, PortraitSize, PortraitSize);
            UIUtil.DrawPawnPortrait(portraitRect, entry.pawn);

            // Text area
            float textX = portraitRect.xMax + 8f;
            float rightAreaW = RoleBtnWidth + 8f;
            float textW = rowRect.width - (textX - rowRect.x) - rightAreaW;

            // Line 1: Name
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(textX, rowRect.y + 2f, textW, 18f), entry.pawn.LabelShort);

            // Line 2: Identity (title, age, xenotype)
            Text.Font = GameFont.Tiny;
            Text.WordWrap = false;
            GUI.color = Color.gray;
            string identity = SpecialistsTabRenderer.BuildIdentityLine(entry.pawn);
            Widgets.Label(new Rect(textX, rowRect.y + 18f, textW, 16f), identity);

            // Line 3: Skills
            string skills = GetTopSkills(entry.pawn, 3);
            Widgets.Label(new Rect(textX, rowRect.y + 34f, textW, 16f), skills);
            GUI.color = Color.white;

            // Line 4: Bonus preview + checkbox
            string bonusText;
            Color bonusColor;
            PreviewContribution(entry, out bonusText, out bonusColor);

            GUI.color = bonusColor;
            Rect bonusRect = new Rect(textX, rowRect.y + 50f, textW - CheckboxSize - 8f, 16f);
            Widgets.Label(bonusRect, bonusText);
            GUI.color = Color.white;

            // Checkbox (vertically centered to match role button)
            float checkX = textX + textW - CheckboxSize - 4f;
            float checkY = rowRect.y + (CardRowHeight - CheckboxSize) / 2f;
            Widgets.Checkbox(new Vector2(checkX, checkY), ref entry.selected);

            Text.WordWrap = true;
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.UpperLeft;

            // Role button (right side, vertically centered)
            float roleBtnX = rowRect.xMax - RoleBtnWidth - 4f;
            float roleBtnY = rowRect.y + (CardRowHeight - RoleBtnHeight) / 2f;
            string roleLabel = entry.isGovernor
                ? "FCS_RoleGovernor".Translate()
                : (entry.role is object ? entry.role.LabelCap : "FCS_RoleResident".Translate());
            if (Widgets.ButtonText(new Rect(roleBtnX, roleBtnY, RoleBtnWidth, RoleBtnHeight), roleLabel))
            {
                ShowRoleMenu(entry, hasGovernor, capReached);
            }
        }

        private void ShowRoleMenu(PawnEntry entry, bool hasGovernor, bool capReached)
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();

            // Resident option (role = null, not governor)
            {
                string label = "FCS_RoleResident".Translate();
                if (!entry.isGovernor && entry.role is null)
                {
                    label += " *";
                }
                options.Add(new FloatMenuOption(label, delegate
                {
                    entry.role = null;
                    entry.isGovernor = false;
                    entry.governorFocus = null;
                }));
            }

            // Specialist role options from DefDatabase
            foreach (SpecialistRoleDef roleDef in DefDatabase<SpecialistRoleDef>.AllDefs)
            {
                SpecialistRoleDef localRole = roleDef;
                bool disabled = false;
                string label = roleDef.LabelCap;

                // Mark current selection
                if (!entry.isGovernor && entry.role == roleDef)
                {
                    label += " *";
                }

                // Cap check: if switching to a specialist role from resident/governor, check capacity
                if (entry.role != roleDef && capReached)
                {
                    // Allow if entry is already a non-resident specialist (swapping roles doesn't change count)
                    if (entry.isGovernor || entry.role is null)
                    {
                        label = "FCS_RoleCapReached".Translate(label);
                        disabled = true;
                    }
                }

                if (disabled)
                {
                    options.Add(new FloatMenuOption(label, null));
                }
                else
                {
                    options.Add(new FloatMenuOption(label, delegate
                    {
                        entry.role = localRole;
                        entry.isGovernor = false;
                        entry.governorFocus = null;
                    }));
                }
            }

            // Governor option — opens sub-menu for GovernorFocusDef selection
            {
                bool govDisabled = false;
                string govLabel = "FCS_RoleGovernor".Translate();

                if (entry.isGovernor)
                {
                    govLabel += " *";
                }

                if (hasGovernor && !IsAnyEntryGovernor())
                {
                    govLabel = "FCS_RoleOccupied".Translate(govLabel);
                    govDisabled = true;
                }

                if (govDisabled)
                {
                    options.Add(new FloatMenuOption(govLabel, null));
                }
                else
                {
                    options.Add(new FloatMenuOption(govLabel, delegate
                    {
                        ShowGovernorFocusSubMenu(entry);
                    }));
                }
            }

            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ShowGovernorFocusSubMenu(PawnEntry entry)
        {
            List<FloatMenuOption> focusOptions = new List<FloatMenuOption>();
            foreach (GovernorFocusDef focusDef in DefDatabase<GovernorFocusDef>.AllDefs)
            {
                GovernorFocusDef localFocus = focusDef;
                string label = focusDef.LabelCap;
                if (entry.isGovernor && entry.governorFocus == focusDef)
                {
                    label += " *";
                }
                focusOptions.Add(new FloatMenuOption(label, delegate
                {
                    // Demote any other entry currently set as governor
                    foreach (PawnEntry other in entries)
                    {
                        if (other != entry && other.isGovernor)
                        {
                            other.isGovernor = false;
                            other.governorFocus = null;
                            other.role = null; // demote to resident
                        }
                    }
                    entry.isGovernor = true;
                    entry.governorFocus = localFocus;
                    entry.role = null;
                }));
            }
            if (focusOptions.Count > 0)
            {
                Find.WindowStack.Add(new FloatMenu(focusOptions));
            }
        }

        private void PreviewContribution(PawnEntry entry, out string text, out Color color)
        {
            Pawn pawn = entry.pawn;

            // Residents: no bonus preview
            if (pawn.skills is null || (!entry.isGovernor && entry.role is null))
            {
                text = "FCS_PreviewNoBonus".Translate();
                color = Color.gray;
                return;
            }

            WorldSettlementFC settlement = comp.Settlement;
            if (settlement is null)
            {
                text = "FCS_PreviewNoBonus".Translate();
                color = Color.gray;
                return;
            }

            // Governor preview
            if (entry.isGovernor && entry.governorFocus is object)
            {
                GovernorFocusDef focus = entry.governorFocus;
                // Create a temporary governor to compute skill score and multiplier
                SettlementGovernor tempGov = new SettlementGovernor(pawn, focus);

                double bestMult = 0;
                string bestLabel = "";
                foreach (ResourceFC resource in settlement.Resources)
                {
                    double mult = SpecUtil.GovernorMultiplierForResource(tempGov, resource.def);
                    if (mult > bestMult)
                    {
                        bestMult = mult;
                        bestLabel = resource.def.LabelCap;
                    }
                }
                if (bestMult > 1.001)
                {
                    text = "FCS_PreviewGovMult".Translate(bestMult.ToString("F2"), bestLabel);
                    color = GovColor;
                    return;
                }

                text = "FCS_PreviewNoBonus".Translate();
                color = Color.gray;
                return;
            }

            // Specialist preview
            if (entry.role is object)
            {
                SettlementSpecialist tempSpec = new SettlementSpecialist(pawn, entry.role);

                double bestBonus = 0;
                string bestLabel = "";
                Color bestColor = Color.white;
                foreach (ResourceFC resource in settlement.Resources)
                {
                    double resBonus = SpecUtil.SpecialistAdditiveForResource(tempSpec, resource.def);
                    if (resBonus > bestBonus)
                    {
                        bestBonus = resBonus;
                        bestLabel = resource.def.LabelCap;
                        bestColor = resource.def.color;
                    }
                }
                if (bestBonus > 0)
                {
                    text = "FCS_ContribProduction".Translate(bestBonus.ToString("F2"), bestLabel);
                    color = bestColor;
                    return;
                }
            }

            text = "FCS_PreviewNoBonus".Translate();
            color = Color.gray;
        }

        private bool IsAnyEntryGovernor()
        {
            return entries.Any(e => e.selected && e.isGovernor);
        }

        private void AssignSelected()
        {
            List<PawnEntry> selected = entries.Where(e => e.selected).ToList();

            foreach (PawnEntry entry in selected)
            {
                caravan.RemovePawn(entry.pawn);

                if (entry.isGovernor && entry.governorFocus is object)
                {
                    comp.AssignGovernor(entry.pawn, entry.governorFocus);
                }
                else
                {
                    comp.AssignSpecialist(entry.pawn, entry.role);
                }
            }

            if (!caravan.Destroyed && caravan.PawnsListForReading.Count == 0)
            {
                caravan.Destroy();
            }
        }

        private string GetTopSkills(Pawn pawn, int count)
        {
            if (pawn.skills is null) return "";

            List<SkillRecord> sorted = pawn.skills.skills
                .Where(s => !s.TotallyDisabled)
                .OrderByDescending(s => s.Level)
                .Take(count)
                .ToList();

            return string.Join(", ", sorted.Select(s => s.def.skillLabel.CapitalizeFirst() + " " + s.Level));
        }

        private class PawnEntry
        {
            public Pawn pawn;
            public bool selected;
            public SpecialistRoleDef role;
            public GovernorFocusDef governorFocus;
            public bool isGovernor;

            public PawnEntry(Pawn pawn)
            {
                this.pawn = pawn;
                this.selected = false;
                this.role = null;
                this.governorFocus = null;
                this.isGovernor = false;
            }
        }
    }
}
