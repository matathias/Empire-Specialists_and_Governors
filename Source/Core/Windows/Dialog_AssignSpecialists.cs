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
            if (pawn is null) return false;
            if (!pawn.RaceProps.Humanlike) return false;
            if (pawn.Downed) return false;
            if (pawn.Dead) return false;
            if (pawn.DevelopmentalStage != DevelopmentalStage.Adult) return false;
            if (pawn.IsPrisoner) return false;
            if (pawn.IsSlave) return false;
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

            bool hasGovernor = comp.HasGovernor;

            Rect listInnerRect = ScrollUtil.BeginScrollView(listOuterRect, ref scrollPos, totalHeight);

            float curY = 0f;
            for (int i = 0; i < entries.Count; i++)
            {
                PawnEntry entry = entries[i];
                Rect rowRect = new Rect(0f, curY, listInnerRect.width, CardRowHeight);

                DrawPawnCard(rowRect, entry, i, hasGovernor);

                curY += CardRowHeight + CardGap;
            }

            ScrollUtil.EndScrollView();

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

        private void DrawPawnCard(Rect rowRect, PawnEntry entry, int rowIdx, bool hasGovernor)
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
            // Reserve the checkbox column on the right so no text line draws under it.
            Rect nameRect = new Rect(textX, rowRect.y + 2f, textW, 18f);
            Widgets.Label(nameRect, TextUtil.ClampWithEllipsis(nameRect, entry.pawn.LabelShort, CheckboxSize + 8f));

            // Line 2: Identity (title, age, xenotype)
            Text.Font = GameFont.Tiny;
            Text.WordWrap = false;
            GUI.color = Color.gray;
            string identity = SpecialistsTabRenderer.BuildIdentityLine(entry.pawn);
            Rect identityRect = new Rect(textX, rowRect.y + 18f, textW, 16f);
            Widgets.Label(identityRect, TextUtil.ClampWithEllipsis(identityRect, identity, CheckboxSize + 8f));

            // Line 3: Skills
            string skills = GetTopSkills(entry.pawn, 3);
            Rect skillsRect = new Rect(textX, rowRect.y + 34f, textW, 16f);
            Widgets.Label(skillsRect, TextUtil.ClampWithEllipsis(skillsRect, skills, CheckboxSize + 8f));
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
                ShowRoleMenu(entry, hasGovernor);
            }
        }

        private void ShowRoleMenu(PawnEntry entry, bool hasGovernor)
        {
            // Count specialists queued in this same dialog (excluding the entry being edited) so
            // the picker can disable roles that pending selections would fill — otherwise the
            // player could queue past the caps and see the later pawns rejected on confirm.
            int pendingSpecialists = 0;
            Dictionary<SpecialistRoleDef, int> pendingRoleCounts = new Dictionary<SpecialistRoleDef, int>();
            foreach (PawnEntry other in entries)
            {
                if (other == entry || !other.selected || other.isGovernor || other.role is null) continue;
                pendingSpecialists++;
                pendingRoleCounts.TryGetValue(other.role, out int count);
                pendingRoleCounts[other.role] = count + 1;
            }

            Find.WindowStack.Add(new Dialog_RolePicker(
                entry.pawn,
                comp,
                onSelectRole: role =>
                {
                    entry.role = role;
                    entry.isGovernor = false;
                    entry.governorFocus = null;
                },
                onSelectGovernor: focus =>
                {
                    foreach (PawnEntry other in entries)
                    {
                        if (other != entry && other.isGovernor)
                        {
                            other.isGovernor = false;
                            other.governorFocus = null;
                            other.role = null;
                        }
                    }
                    entry.isGovernor = true;
                    entry.governorFocus = focus;
                    entry.role = null;
                },
                allowGovernor: !hasGovernor || IsAnyEntryGovernor() || entry.isGovernor,
                currentRole: entry.isGovernor ? null : entry.role,
                isCurrentlyGovernor: entry.isGovernor,
                currentGovernorFocus: entry.governorFocus,
                pendingSpecialists: pendingSpecialists,
                pendingRoleCounts: pendingRoleCounts));
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
                bool isGovAssign = entry.isGovernor && entry.governorFocus is object;
                bool ok = isGovAssign ? comp.CanAssignGovernor : comp.CanAssignSpecialist(entry.role);

                // Remove from the caravan ONLY when placement is guaranteed. On rejection the pawn
                // stays in the caravan (never stranded); the Assign* call still fires its message.
                if (ok) caravan.RemovePawn(entry.pawn);

                if (isGovAssign)
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
