using System.Collections.Generic;
using FactionColonies.SupplyChain;
using RimWorld.Planet;
using RimWorld;
using Verse;

namespace FactionColonies.Specialists.SC
{
    public class WorldObjectCompProperties_SpecialistNeeds : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_SpecialistNeeds()
        {
            compClass = typeof(WorldObjectComp_SpecialistNeeds);
        }
    }

    /// <summary>
    /// Bridge comp connecting the Specialists submod to the Supply Chain's need resolution system.
    /// Injected via conditional XML patch (PatchOperationFindMod) only when Supply Chain is active.
    /// Implements INeedProvider: emits food and medicine needs based on specialist roster,
    /// writes satisfaction back to the main specialists comp for production scaling.
    /// </summary>
    public class WorldObjectComp_SpecialistNeeds : WorldObjectComp, INeedProvider
    {
        private WorldObjectComp_SettlementSpecialists specialistsComp;

        private WorldObjectComp_SettlementSpecialists Specialists => specialistsComp ??
                                                                     (specialistsComp = parent.GetComponent<WorldObjectComp_SettlementSpecialists>());

        private static FCStatDef happinessLostStat;
        private static FCStatDef HappinessLostStat => happinessLostStat ??
            (happinessLostStat = DefDatabase<FCStatDef>.GetNamedSilentFail("happinessLostBase"));

        // Built fresh per collection so the food/medicine penalty sliders take effect live (no restart).
        private static List<NeedPenalty> BuildPenalties(float penaltyPerUnit)
        {
            FCStatDef stat = HappinessLostStat;
            if (stat is null) return null;
            return new List<NeedPenalty>
            {
                new NeedPenalty
                {
                    stat = stat,
                    penaltyPerUnit = penaltyPerUnit,
                    label = "FCSRR_happinesspenalty".Translate()
                }
            };
        }

        public void CollectNeeds(WorldSettlementFC settlement, List<NeedEntry> needs)
        {
            var spec = Specialists;
            if (spec is null) return;

            int activeCount = spec.SpecialistCount + (spec.HasGovernor ? 1 : 0);
            int residentCount = spec.ResidentCount;

            if (activeCount + residentCount <= 0) return;

            // Food need: specialists + governor eat more, residents eat less
            double foodAmount = activeCount * FCSSettings.foodPerSpecialist
                                + residentCount * FCSSettings.foodPerResident;
            if (foodAmount > 0)
            {
                needs.Add(new NeedEntry
                {
                    needId = "specialist.food",
                    label = "FCSRR_FoodNeed".Translate(),
                    resource = ResourceTypeDefOf.RTD_Food,
                    amount = foodAmount,
                    penalties = BuildPenalties(FCSSettings.foodPenaltyPerUnit)
                });
            }

            // Medicine need: only active specialists + governor
            if (activeCount > 0)
            {
                double medAmount = activeCount * FCSSettings.medicinePerSpecialist;
                if (medAmount > 0)
                {
                    needs.Add(new NeedEntry
                    {
                        needId = "specialist.medicine",
                        label = "FCSRR_MedicineNeed".Translate(),
                        resource = ResourceTypeDefOf.RTD_Medicine,
                        amount = medAmount,
                        penalties = BuildPenalties(FCSSettings.medicinePenaltyPerUnit)
                    });
                }
            }
        }

        public void OnNeedsResolved(List<NeedResolution> resolvedNeeds)
        {
            var spec = Specialists;
            if (spec is null) return;

            foreach (NeedResolution r in resolvedNeeds)
            {
                if (r.needId == "specialist.food")
                    spec.FoodSatisfaction = r.Satisfaction;
                else if (r.needId == "specialist.medicine")
                    spec.MedicineSatisfaction = r.Satisfaction;
            }

            WorldSettlementFC ws = parent as WorldSettlementFC;
            if (ws is object)
            {
                ws.InvalidateStatCache();
            }
        }
    }
}
