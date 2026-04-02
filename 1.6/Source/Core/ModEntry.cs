using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace FactionColonies.Specialists
{
    public class FCSSettings : ModSettings
    {
        private static bool printDebug = false;
        public static bool PrintDebug => printDebug;

        private static bool isRoutesResourcesActive = false;
        public static bool RoutesResourcesActive => isRoutesResourcesActive;

        // Specialist capacity
        public static int specialistBaseMax = 5;
        public static int specialistPerLevels = 3;

        // Workers
        public static int residentsPerWorker = 5;

        // Upkeep
        public static float specialistBaseCost = 3f;
        public static float skillDivisor = 20f;
        public static float scalingFactor = 1f;

        // XP
        public static float xpPerDay = 500f;

        // Supply chain integration: resource needs per tax period
        public static float foodPerSpecialist = 0.3f;
        public static float foodPerResident = 0.15f;
        public static float medicinePerSpecialist = 0.1f;
        public static float foodPenaltyPerUnit = 0.8f;
        public static float medicinePenaltyPerUnit = 0.3f;

        // Death chances on battle loss
        public static float civilianDeathChance = 0.15f;
        public static float governorDeathChance = 0.30f;
        public static float defenseDeathChance = 0.10f;
        public static float residentDeathChance = 0.15f;
        public static float deathReductionPerDefender = 0.02f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref printDebug, "printDebug", false);
            Scribe_Values.Look(ref specialistBaseMax, "specialistBaseMax", 5);
            Scribe_Values.Look(ref specialistPerLevels, "specialistPerLevels", 3);
            Scribe_Values.Look(ref residentsPerWorker, "residentsPerWorker", 5);
            Scribe_Values.Look(ref specialistBaseCost, "specialistBaseCost", 3f);
            Scribe_Values.Look(ref skillDivisor, "skillDivisor", 20f);
            Scribe_Values.Look(ref scalingFactor, "scalingFactor", 1f);
            Scribe_Values.Look(ref xpPerDay, "xpPerDay", 500f);
            Scribe_Values.Look(ref foodPerSpecialist, "foodPerSpecialist", 0.3f);
            Scribe_Values.Look(ref foodPerResident, "foodPerResident", 0.15f);
            Scribe_Values.Look(ref medicinePerSpecialist, "medicinePerSpecialist", 0.1f);
            Scribe_Values.Look(ref foodPenaltyPerUnit, "foodPenaltyPerUnit", 0.8f);
            Scribe_Values.Look(ref medicinePenaltyPerUnit, "medicinePenaltyPerUnit", 0.3f);
            Scribe_Values.Look(ref civilianDeathChance, "civilianDeathChance", 0.15f);
            Scribe_Values.Look(ref governorDeathChance, "governorDeathChance", 0.30f);
            Scribe_Values.Look(ref defenseDeathChance, "defenseDeathChance", 0.10f);
            Scribe_Values.Look(ref residentDeathChance, "residentDeathChance", 0.15f);
            Scribe_Values.Look(ref deathReductionPerDefender, "deathReductionPerDefender", 0.02f);
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("FCS_SettingDebugLog".Translate(), ref printDebug);

            // Specialist capacity
            ls.Gap(12f);
            ls.Label("FCS_SettingSpecBaseMax".Translate() + specialistBaseMax);
            specialistBaseMax = (int)ls.Slider(specialistBaseMax, 1, 20);
            ls.Label("FCS_SettingSpecPerLevels".Translate() + specialistPerLevels);
            specialistPerLevels = (int)ls.Slider(specialistPerLevels, 1, 10);

            // Workers
            ls.Gap(12f);
            ls.Label("FCS_SettingResidentsPerWorker".Translate() + residentsPerWorker);
            residentsPerWorker = (int)ls.Slider(residentsPerWorker, 1, 20);

            // Upkeep
            ls.Gap(8f);
            ls.Label("FCS_SettingBaseUpkeep".Translate() + specialistBaseCost.ToString("F1"));
            specialistBaseCost = (float)System.Math.Round(ls.Slider(specialistBaseCost, 0f, 20f), 1);
            ls.Label("FCS_SettingSkillDivisor".Translate() + skillDivisor.ToString("F0"));
            skillDivisor = (float)System.Math.Round(ls.Slider(skillDivisor, 1f, 50f), 0);
            ls.Label("FCS_SettingScalingFactor".Translate() + scalingFactor.ToString("F1"));
            scalingFactor = (float)System.Math.Round(ls.Slider(scalingFactor, 0f, 5f), 1);

            // XP
            ls.Gap(8f);
            ls.Label("FCS_SettingXpPerDay".Translate() + xpPerDay.ToString("F0"));
            xpPerDay = (float)System.Math.Round(ls.Slider(xpPerDay, 0f, 2000f), 0);

            // Death chances
            ls.Gap(8f);
            ls.Label("FCS_SettingCivilianDeathChance".Translate() + (civilianDeathChance * 100f).ToString("F0") + "%");
            civilianDeathChance = (float)System.Math.Round(ls.Slider(civilianDeathChance, 0f, 1f), 2);
            ls.Label("FCS_SettingGovernorDeathChance".Translate() + (governorDeathChance * 100f).ToString("F0") + "%");
            governorDeathChance = (float)System.Math.Round(ls.Slider(governorDeathChance, 0f, 1f), 2);
            ls.Label("FCS_SettingDefenseDeathChance".Translate() + (defenseDeathChance * 100f).ToString("F0") + "%");
            defenseDeathChance = (float)System.Math.Round(ls.Slider(defenseDeathChance, 0f, 1f), 2);
            ls.Label("FCS_SettingResidentDeathChance".Translate() + (residentDeathChance * 100f).ToString("F0") + "%");
            residentDeathChance = (float)System.Math.Round(ls.Slider(residentDeathChance, 0f, 1f), 2);
            ls.Label("FCS_SettingDeathReduction".Translate() + (deathReductionPerDefender * 100f).ToString("F0") + "%");
            deathReductionPerDefender = (float)System.Math.Round(ls.Slider(deathReductionPerDefender, 0f, 0.1f), 2);

            // Supply chain needs (only show if Supply Chain submod is active)
            if (ModsConfig.IsActive("Matathias.Empire.SupplyChain"))
            {
                ls.Gap(8f);
                ls.Label("FCS_SettingSCHeader".Translate());
                ls.Label("FCS_SettingFoodPerSpec".Translate() + foodPerSpecialist.ToString("F2"));
                foodPerSpecialist = (float)System.Math.Round(ls.Slider(foodPerSpecialist, 0f, 2f), 2);
                ls.Label("FCS_SettingFoodPerResident".Translate() + foodPerResident.ToString("F2"));
                foodPerResident = (float)System.Math.Round(ls.Slider(foodPerResident, 0f, 1f), 2);
                ls.Label("FCS_SettingMedPerSpec".Translate() + medicinePerSpecialist.ToString("F2"));
                medicinePerSpecialist = (float)System.Math.Round(ls.Slider(medicinePerSpecialist, 0f, 1f), 2);
            }

            ls.End();
        }

        public static void CheckForRoutesAndResources()
        {
            if (LoadedModManager.RunningMods.Any(mod => mod.PackageId.ToLower() == "matathias.empire.supplychain"))
            {
                isRoutesResourcesActive = true;
            }
            LogSG.MessageForce($"Routes & Resources is active: {isRoutesResourcesActive}");
        }
    }

    [StaticConstructorOnStartup]
    public static class SpecialistsStartup
    {
        private static readonly SpecialistLifecycleHandler _lifecycleHandler = new SpecialistLifecycleHandler();

        static SpecialistsStartup()
        {
            new Harmony("Matathias.Empire.Specialists").PatchAll(Assembly.GetExecutingAssembly());
            LifecycleRegistry.Register(_lifecycleHandler);
            EmpireCacheUtil.RegisterCacheInvalidator("Specialists", () =>
            {
                SpecialistsCache.InvalidateCache();
                // Re-register after InvalidateAll clears all registries
                LifecycleRegistry.Register(_lifecycleHandler);
            });
        }
    }

    public class SpecialistsMod : Mod
    {
        public FCSSettings settings;

        public SpecialistsMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<FCSSettings>();
            FCSSettings.CheckForRoutesAndResources();
        }

        public override string SettingsCategory() => "FCS_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect) => settings.DoWindowContents(inRect);
    }
}
