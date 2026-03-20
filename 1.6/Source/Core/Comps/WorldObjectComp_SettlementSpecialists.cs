using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

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

        // --- ISettlementWindowOverview (stub for Phase 1) ---

        public void PreOpenWindow(WorldSettlementFC settlement)
        {
        }

        public void OnTabSwitch()
        {
        }

        public void DrawOverviewTab(Rect boundingBox)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(boundingBox.x, boundingBox.y, boundingBox.width, 30f),
                "Specialists");
            Text.Font = GameFont.Small;

            float y = boundingBox.y + 40f;

            SpecialistFC gov = Governor;
            if (gov != null && gov.pawn != null)
            {
                Widgets.Label(new Rect(boundingBox.x, y, boundingBox.width, 24f),
                    "Governor: " + gov.pawn.LabelShort);
                y += 28f;
            }

            int specCount = CivilianSpecialists.Count();
            int defCount = DefenseSpecialists.Count();
            int resCount = Residents.Count();

            Widgets.Label(new Rect(boundingBox.x, y, boundingBox.width, 24f),
                "Specialists: " + specCount + "  |  Defense: " + defCount + "  |  Residents: " + resCount);
            y += 28f;

            // List all assigned pawns
            foreach (SpecialistFC s in allPawns)
            {
                if (s.pawn == null) continue;
                Rect row = new Rect(boundingBox.x, y, boundingBox.width - 80f, 24f);
                Widgets.Label(row, s.pawn.LabelShort + " (" + s.role + ")");

                Rect recallBtn = new Rect(boundingBox.x + boundingBox.width - 75f, y, 70f, 24f);
                if (Widgets.ButtonText(recallBtn, "Recall"))
                {
                    RecallPawn(s);
                    break;
                }
                y += 28f;
            }
        }

        public void PostCloseWindow()
        {
        }

        public string OverviewTabName()
        {
            return "Specialists";
        }

        // --- IStatModifierProvider (stub for Phase 2) ---

        public double GetStatModifier(FCStatDef stat)
        {
            return 0;
        }

        public string GetStatModifierDesc(FCStatDef stat)
        {
            return null;
        }

        // --- IResourceProductionModifier (stub for Phase 2) ---

        public double GetResourceAdditiveModifier(ResourceFC resource)
        {
            return 0;
        }

        public double GetResourceMultiplierModifier(ResourceFC resource)
        {
            return 1.0;
        }

        public string GetResourceModifierDesc(ResourceFC resource)
        {
            return null;
        }
    }
}
