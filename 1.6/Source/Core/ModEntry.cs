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
        public static int specialistBaseMax = 3;
        public static int specialistPerLevels = 3;

        // Workers
        public static int residentsPerWorker = 5;

        // Upkeep: global multiplier on the skill-scaled portion of specialist/governor wages.
        public static float scalingFactor = 1f;

        // Healing
        public static float healRatePerLevelGovernor = 0.02f;

        // XP
        public static float xpPerDay = 500f;

        // Skill scaling: per-band scaling of each skill level's contribution to bonuses.
        // Defaults leave vanilla 1-20 untouched; band3 (21+) only matters for cap-removing mods.
        public static float skillTaperFactorBand1 = 1f;   // levels 1-10
        public static float skillTaperFactorBand2 = 1f;   // levels 11-20
        public static float skillTaperFactorBand3 = 0.5f; // levels 21+

        // Supply chain integration: per-day resource needs consumed by SupplyChain.
        public static float foodPerSpecialist = 0.3f;
        public static float foodPerResident = 0.15f;
        public static float medicinePerSpecialist = 0.1f;
        public static float foodPenaltyPerUnit = 0.8f;
        public static float medicinePenaltyPerUnit = 0.3f;

        private static Vector2 scrollPos;
        private static float contentHeight = 0f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref printDebug, "printDebug", false);
            Scribe_Values.Look(ref specialistBaseMax, "specialistBaseMax", 3);
            Scribe_Values.Look(ref specialistPerLevels, "specialistPerLevels", 3);
            Scribe_Values.Look(ref residentsPerWorker, "residentsPerWorker", 5);
            Scribe_Values.Look(ref scalingFactor, "scalingFactor", 1f);
            Scribe_Values.Look(ref healRatePerLevelGovernor, "healRatePerLevelGovernor", 0.02f);
            Scribe_Values.Look(ref xpPerDay, "xpPerDay", 500f);
            Scribe_Values.Look(ref skillTaperFactorBand1, "skillTaperFactorBand1", 1f);
            Scribe_Values.Look(ref skillTaperFactorBand2, "skillTaperFactorBand2", 1f);
            Scribe_Values.Look(ref skillTaperFactorBand3, "skillTaperFactorBand3", 0.5f);
            Scribe_Values.Look(ref foodPerSpecialist, "foodPerSpecialist", 0.3f);
            Scribe_Values.Look(ref foodPerResident, "foodPerResident", 0.15f);
            Scribe_Values.Look(ref medicinePerSpecialist, "medicinePerSpecialist", 0.1f);
            Scribe_Values.Look(ref foodPenaltyPerUnit, "foodPenaltyPerUnit", 0.8f);
            Scribe_Values.Look(ref medicinePenaltyPerUnit, "medicinePenaltyPerUnit", 0.3f);
        }

        public static void ResetToDefaults()
        {
            printDebug = false;
            specialistBaseMax = 3;
            specialistPerLevels = 3;
            residentsPerWorker = 5;
            scalingFactor = 1f;
            healRatePerLevelGovernor = 0.02f;
            xpPerDay = 500f;
            skillTaperFactorBand1 = 1f;
            skillTaperFactorBand2 = 1f;
            skillTaperFactorBand3 = 0.5f;
            foodPerSpecialist = 0.3f;
            foodPerResident = 0.15f;
            medicinePerSpecialist = 0.1f;
            foodPenaltyPerUnit = 0.8f;
            medicinePenaltyPerUnit = 0.3f;
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            Rect viewRect = ScrollUtil.BeginScrollView(inRect, ref scrollPos, contentHeight);
            ls.Begin(new Rect(viewRect.x, viewRect.y, viewRect.width, float.MaxValue));
            Listing_StandardExtensions.ResetRowStripe();

            ls.CheckboxLabeled("FCS_SettingDebugLog".Translate(), ref printDebug);
            ls.GapLine();

            // Specialist capacity
            ls.Label("FCS_Header_Capacity".Translate());
            ls.Gap(4f);
            specialistBaseMax = ls.SliderTextField("FCS_SettingSpecBaseMax", "FCS_SettingSpecBaseMax".Translate(), specialistBaseMax, 1, 20);
            specialistPerLevels = ls.SliderTextField("FCS_SettingSpecPerLevels", "FCS_SettingSpecPerLevels".Translate(), specialistPerLevels, 1, 10);
            ls.GapLine();

            // Workers
            ls.Label("FCS_Header_Workers".Translate());
            ls.Gap(4f);
            residentsPerWorker = ls.SliderTextField("FCS_SettingResidentsPerWorker", "FCS_SettingResidentsPerWorker".Translate(), residentsPerWorker, 1, 20);
            ls.GapLine();

            // Upkeep
            Rect upkeepHeader = ls.Label("FCS_Header_Upkeep".Translate(), -1f, new TipSignal("FCS_TooltipUpkeepSection".Translate()));
            Widgets.DrawHighlightIfMouseover(upkeepHeader);
            ls.Gap(4f);
            scalingFactor = ls.SliderTextField("FCS_SettingScalingFactor", "FCS_SettingScalingFactor".Translate(), scalingFactor, 0f, 5f, 1);
            ls.GapLine();

            // Healing
            ls.Label("FCS_Header_Healing".Translate());
            ls.Gap(4f);
            healRatePerLevelGovernor = ls.SliderTextField("FCS_SettingHealRateGov", "FCS_SettingHealRateGov".Translate(), healRatePerLevelGovernor, 0f, 0.1f, 3);
            ls.GapLine();

            // XP
            ls.Label("FCS_Header_XP".Translate());
            ls.Gap(4f);
            xpPerDay = ls.SliderTextField("FCS_SettingXpPerDay", "FCS_SettingXpPerDay".Translate(), xpPerDay, 0f, 2000f, 0, tooltip: "FCS_TooltipXpPerDay".Translate());
            ls.GapLine();

            // Skill scaling
            ls.Label("FCS_Header_SkillScaling".Translate());
            ls.Gap(4f);
            skillTaperFactorBand1 = ls.SliderTextField("FCS_SettingSkillTaperBand1", "FCS_SettingSkillTaperBand1".Translate(), skillTaperFactorBand1, 0f, 5f, 2);
            skillTaperFactorBand2 = ls.SliderTextField("FCS_SettingSkillTaperBand2", "FCS_SettingSkillTaperBand2".Translate(), skillTaperFactorBand2, 0f, 5f, 2);
            skillTaperFactorBand3 = ls.SliderTextField("FCS_SettingSkillTaperBand3", "FCS_SettingSkillTaperBand3".Translate(), skillTaperFactorBand3, 0f, 5f, 2);
            ls.GapLine();

            // Supply chain needs (only show if Supply Chain submod is active)
            if (isRoutesResourcesActive)
            {
                ls.Label("FCS_Header_SupplyChain".Translate());
                ls.Gap(4f);
                foodPerSpecialist = ls.SliderTextField("FCS_SettingFoodPerSpec", "FCS_SettingFoodPerSpec".Translate(), foodPerSpecialist, 0f, 2f, 2);
                foodPerResident = ls.SliderTextField("FCS_SettingFoodPerResident", "FCS_SettingFoodPerResident".Translate(), foodPerResident, 0f, 1f, 2);
                medicinePerSpecialist = ls.SliderTextField("FCS_SettingMedPerSpec", "FCS_SettingMedPerSpec".Translate(), medicinePerSpecialist, 0f, 1f, 2);
                foodPenaltyPerUnit = ls.SliderTextField("FCS_SettingFoodPenalty", "FCS_SettingFoodPenalty".Translate(), foodPenaltyPerUnit, 0f, 2f, 2);
                medicinePenaltyPerUnit = ls.SliderTextField("FCS_SettingMedPenalty", "FCS_SettingMedPenalty".Translate(), medicinePenaltyPerUnit, 0f, 2f, 2);
                ls.GapLine();
            }

            ls.Gap(12f);
            if (ls.ButtonText("FCS_SettingResetAll".Translate()))
                ResetToDefaults();

            ls.Gap(4f);
            if (ls.ButtonText("SP_OpenPatchNotes".Translate()))
                Find.WindowStack.Add(new PatchNotesDisplayWindow("matathias.empire.specialists", "SP_PatchTitle".Translate()));

            contentHeight = ls.CurHeight + 12f;
            ls.End();
            ScrollUtil.EndScrollView();
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
            EmpireRegistry.Register(_lifecycleHandler);
            EmpireCacheUtil.RegisterCacheInvalidator("Specialists", () =>
            {
                SpecialistsCache.InvalidateCache();
                // Re-register after InvalidateAll clears all registries
                EmpireRegistry.Register(_lifecycleHandler);
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
