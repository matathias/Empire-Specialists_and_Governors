using System;
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

        private const float RowHeight = 35f;
        private const float margin = 10f;

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
            Text.Font = GameFont.Medium;
            Rect titleRect = new Rect(inRect.x, inRect.y, inRect.width, 35f);
            Widgets.Label(titleRect, "Assign Pawns to " + comp.Settlement.Name);
            Text.Font = GameFont.Small;

            float headerY = inRect.y + 45f;
            float buttonHeight = 35f;
            float listHeight = inRect.height - 45f - buttonHeight - margin;

            Rect listOuterRect = new Rect(inRect.x, headerY, inRect.width, listHeight);
            float totalHeight = entries.Count * RowHeight;
            Rect listInnerRect = new Rect(0f, 0f, listOuterRect.width - 16f, totalHeight);

            Widgets.BeginScrollView(listOuterRect, ref scrollPos, listInnerRect);

            float curY = 0f;
            bool hasGovernor = comp.Governor != null;
            int pendingNonResident = entries.Count(e => e.selected && e.role != SpecialistRole.Resident);
            bool capReached = (comp.SpecialistCount + pendingNonResident) >= comp.MaxSpecialists;

            for (int i = 0; i < entries.Count; i++)
            {
                PawnEntry entry = entries[i];
                Rect rowRect = new Rect(0f, curY, listInnerRect.width, RowHeight);

                if (i % 2 == 1)
                {
                    Widgets.DrawLightHighlight(rowRect);
                }

                // Checkbox
                Rect checkRect = new Rect(rowRect.x + 4f, rowRect.y + 4f, 24f, 24f);
                Widgets.Checkbox(checkRect.position, ref entry.selected);

                // Pawn name
                Rect nameRect = new Rect(checkRect.xMax + 8f, rowRect.y, 160f, RowHeight);
                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(nameRect, entry.pawn.LabelShort);

                // Top skills
                Rect skillRect = new Rect(nameRect.xMax + 4f, rowRect.y, 180f, RowHeight);
                string skills = GetTopSkills(entry.pawn, 3);
                GUI.color = Color.gray;
                Widgets.Label(skillRect, skills);
                GUI.color = Color.white;

                // Role selector
                Rect roleRect = new Rect(skillRect.xMax + 4f, rowRect.y + 4f, 120f, RowHeight - 8f);
                string roleLabel = entry.role.ToString();
                if (Widgets.ButtonText(roleRect, roleLabel))
                {
                    List<FloatMenuOption> options = new List<FloatMenuOption>();
                    foreach (SpecialistRole role in Enum.GetValues(typeof(SpecialistRole)))
                    {
                        SpecialistRole localRole = role;
                        bool disabled = false;
                        string label = role.ToString();

                        if (role == SpecialistRole.Governor && hasGovernor && !IsAnyEntryGovernor())
                        {
                            label += " (occupied)";
                            disabled = true;
                        }
                        else if (role != SpecialistRole.Resident && role != entry.role && capReached)
                        {
                            label += " (cap reached)";
                            disabled = true;
                        }

                        if (disabled)
                        {
                            options.Add(new FloatMenuOption(label, null));
                        }
                        else
                        {
                            options.Add(new FloatMenuOption(label, delegate
                            {
                                // If another entry was governor, demote it
                                if (localRole == SpecialistRole.Governor)
                                {
                                    foreach (PawnEntry other in entries)
                                    {
                                        if (other != entry && other.role == SpecialistRole.Governor)
                                        {
                                            other.role = SpecialistRole.Specialist;
                                        }
                                    }
                                }
                                entry.role = localRole;
                            }));
                        }
                    }
                    Find.WindowStack.Add(new FloatMenu(options));
                }

                Text.Anchor = TextAnchor.UpperLeft;
                curY += RowHeight;
            }

            Widgets.EndScrollView();

            // Confirm button
            Rect confirmRect = new Rect(inRect.x + inRect.width - 150f, inRect.yMax - buttonHeight, 150f, buttonHeight);
            int selectedCount = entries.Count(e => e.selected);

            if (selectedCount == 0)
            {
                GUI.color = Color.gray;
            }

            if (Widgets.ButtonText(confirmRect, "Confirm (" + selectedCount + ")") && selectedCount > 0)
            {
                AssignSelected();
                Close();
            }

            GUI.color = Color.white;
        }

        private bool IsAnyEntryGovernor()
        {
            return entries.Any(e => e.selected && e.role == SpecialistRole.Governor);
        }

        private void AssignSelected()
        {
            List<PawnEntry> selected = entries.Where(e => e.selected).ToList();

            foreach (PawnEntry entry in selected)
            {
                caravan.RemovePawn(entry.pawn);
                comp.AssignPawn(entry.pawn, entry.role);
            }

            if (!caravan.Destroyed && caravan.PawnsListForReading.Count == 0)
            {
                caravan.Destroy();
            }
        }

        private string GetTopSkills(Pawn pawn, int count)
        {
            if (pawn.skills == null) return "";

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
            public SpecialistRole role;

            public PawnEntry(Pawn pawn)
            {
                this.pawn = pawn;
                this.selected = false;
                this.role = SpecialistRole.Resident;
            }
        }
    }
}
