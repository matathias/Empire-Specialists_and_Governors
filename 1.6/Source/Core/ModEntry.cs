using System.Linq;
using System.Reflection;
using FactionColonies;
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

        // Healing
        public static float healRatePerLevelGovernor = 0.02f;

        // XP
        public static float xpPerDay = 500f;

        // Supply chain integration: resource needs per tax period
        public static float foodPerSpecialist = 0.3f;
        public static float foodPerResident = 0.15f;
        public static float medicinePerSpecialist = 0.1f;
        public static float foodPenaltyPerUnit = 0.8f;
        public static float medicinePenaltyPerUnit = 0.3f;

        private static Vector2 scrollPos;

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
            Scribe_Values.Look(ref healRatePerLevelGovernor, "healRatePerLevelGovernor", 0.02f);
            Scribe_Values.Look(ref xpPerDay, "xpPerDay", 500f);
            Scribe_Values.Look(ref foodPerSpecialist, "foodPerSpecialist", 0.3f);
            Scribe_Values.Look(ref foodPerResident, "foodPerResident", 0.15f);
            Scribe_Values.Look(ref medicinePerSpecialist, "medicinePerSpecialist", 0.1f);
            Scribe_Values.Look(ref foodPenaltyPerUnit, "foodPenaltyPerUnit", 0.8f);
            Scribe_Values.Look(ref medicinePenaltyPerUnit, "medicinePenaltyPerUnit", 0.3f);
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            Rect viewRect = ScrollUtil.BeginScrollView(inRect, ref scrollPos, isRoutesResourcesActive ? 900f : 700f);
            ls.Begin(viewRect);

            ls.CheckboxLabeled("FCS_SettingDebugLog".Translate(), ref printDebug);
            ls.GapLine();

            // Specialist capacity
            ls.Label("FCS_Header_Capacity".Translate());
            ls.Gap(4f);
            specialistBaseMax = (int)SliderLabeled(ls, "FCS_SettingSpecBaseMax".Translate(), specialistBaseMax, 1f, 20f);
            specialistPerLevels = (int)SliderLabeled(ls, "FCS_SettingSpecPerLevels".Translate(), specialistPerLevels, 1f, 10f);
            ls.GapLine();

            // Workers
            ls.Label("FCS_Header_Workers".Translate());
            ls.Gap(4f);
            residentsPerWorker = (int)SliderLabeled(ls, "FCS_SettingResidentsPerWorker".Translate(), residentsPerWorker, 1f, 20f);
            ls.GapLine();

            // Upkeep
            ls.Label("FCS_Header_Upkeep".Translate());
            ls.Gap(4f);
            specialistBaseCost = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingBaseUpkeep".Translate(), specialistBaseCost, 0f, 20f), 1);
            skillDivisor = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingSkillDivisor".Translate(), skillDivisor, 1f, 50f), 0);
            scalingFactor = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingScalingFactor".Translate(), scalingFactor, 0f, 5f), 1);
            ls.GapLine();

            // Healing
            ls.Label("FCS_Header_Healing".Translate());
            ls.Gap(4f);
            healRatePerLevelGovernor = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingHealRateGov".Translate(), healRatePerLevelGovernor, 0f, 0.1f), 3);
            ls.GapLine();

            // XP
            ls.Label("FCS_Header_XP".Translate());
            ls.Gap(4f);
            xpPerDay = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingXpPerDay".Translate(), xpPerDay, 0f, 2000f), 0);
            ls.GapLine();

            // Supply chain needs (only show if Supply Chain submod is active)
            if (isRoutesResourcesActive)
            {
                ls.Label("FCS_Header_SupplyChain".Translate());
                ls.Gap(4f);
                foodPerSpecialist = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingFoodPerSpec".Translate(), foodPerSpecialist, 0f, 2f), 2);
                foodPerResident = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingFoodPerResident".Translate(), foodPerResident, 0f, 1f), 2);
                medicinePerSpecialist = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingMedPerSpec".Translate(), medicinePerSpecialist, 0f, 1f), 2);
                foodPenaltyPerUnit = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingFoodPenalty".Translate(), foodPenaltyPerUnit, 0f, 2f), 2);
                medicinePenaltyPerUnit = (float)System.Math.Round(SliderLabeled(ls, "FCS_SettingMedPenalty".Translate(), medicinePenaltyPerUnit, 0f, 2f), 2);
                ls.GapLine();
            }

            ls.Gap(12f);
            if (ls.ButtonText("SP_OpenPatchNotes".Translate()))
                Find.WindowStack.Add(new PatchNotesDisplayWindow("matathias.empire.specialists", "SP_PatchTitle".Translate()));

            ls.End();
            ScrollUtil.EndScrollView();
        }

        private static float SliderLabeled(Listing_Standard ls, string label, float val, float min, float max)
        {
            return ls.SliderLabeled(label + val.ToString("F2"), val, min, max);
        }

        private static float SliderLabeledPct(Listing_Standard ls, string label, float val, float min, float max)
        {
            return ls.SliderLabeled(label + (val * 100f).ToString("F0") + "%", val, min, max);
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

            string modVersion = content?.ModMetaData?.ModVersion;
            if (modVersion.NullOrEmpty())
            {
                LogSG.MessageForce("Did not load a mod version");
            }
            else
            {
                LogSG.MessageForce($"v{modVersion}");
            }
        }

        public override string SettingsCategory() => "FCS_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect) => settings.DoWindowContents(inRect);
    }
}
