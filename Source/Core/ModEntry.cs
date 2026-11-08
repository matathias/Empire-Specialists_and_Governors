using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FactionColonies.Specialists;

public class FCSSettings : ModSettings
{
    /* Defaults, shared by the field initializers, ExposeData and ResetToDefaults */
    public const bool DefaultPrintDebug = false;
    public const int DefaultSpecialistBaseMax = 3;
    public const int DefaultSpecialistPerLevels = 3;
    public const int DefaultResidentsPerWorker = 5;
    public const float DefaultScalingFactor = 1f;
    public const float DefaultHealRatePerLevelGovernor = 0.02f;
    public const float DefaultXpPerDay = 500f;
    public const float DefaultSkillTaperFactorBand1 = 1f;
    public const float DefaultSkillTaperFactorBand2 = 1f;
    public const float DefaultSkillTaperFactorBand3 = 0.5f;
    public const float DefaultFoodPerSpecialist = 0.3f;
    public const float DefaultFoodPerResident = 0.15f;
    public const float DefaultMedicinePerSpecialist = 0.1f;
    public const float DefaultFoodPenaltyPerUnit = 0.8f;
    public const float DefaultMedicinePenaltyPerUnit = 0.3f;

    private static bool printDebug = DefaultPrintDebug;
    public static bool PrintDebug => printDebug;

    private static bool isRoutesResourcesActive = false;
    public static bool RoutesResourcesActive => isRoutesResourcesActive;

    // Specialist capacity
    public static int specialistBaseMax = DefaultSpecialistBaseMax;
    public static int specialistPerLevels = DefaultSpecialistPerLevels;

    // Workers
    public static int residentsPerWorker = DefaultResidentsPerWorker;

    // Upkeep: global multiplier on the skill-scaled portion of specialist/governor wages.
    public static float scalingFactor = DefaultScalingFactor;

    // Healing
    public static float healRatePerLevelGovernor = DefaultHealRatePerLevelGovernor;

    // XP
    public static float xpPerDay = DefaultXpPerDay;

    // Skill scaling: per-band scaling of each skill level's contribution to bonuses.
    // Defaults leave vanilla 1-20 untouched; band3 (21+) only matters for cap-removing mods.
    public static float skillTaperFactorBand1 = DefaultSkillTaperFactorBand1;   // levels 1-10
    public static float skillTaperFactorBand2 = DefaultSkillTaperFactorBand2;   // levels 11-20
    public static float skillTaperFactorBand3 = DefaultSkillTaperFactorBand3; // levels 21+

    // Supply chain integration: per-day resource needs consumed by SupplyChain.
    public static float foodPerSpecialist = DefaultFoodPerSpecialist;
    public static float foodPerResident = DefaultFoodPerResident;
    public static float medicinePerSpecialist = DefaultMedicinePerSpecialist;
    public static float foodPenaltyPerUnit = DefaultFoodPenaltyPerUnit;
    public static float medicinePenaltyPerUnit = DefaultMedicinePenaltyPerUnit;

    private static Vector2 scrollPos;
    private static float contentHeight = 0f;

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref printDebug, "printDebug", DefaultPrintDebug);
        Scribe_Values.Look(ref specialistBaseMax, "specialistBaseMax", DefaultSpecialistBaseMax);
        Scribe_Values.Look(ref specialistPerLevels, "specialistPerLevels", DefaultSpecialistPerLevels);
        Scribe_Values.Look(ref residentsPerWorker, "residentsPerWorker", DefaultResidentsPerWorker);
        Scribe_Values.Look(ref scalingFactor, "scalingFactor", DefaultScalingFactor);
        Scribe_Values.Look(ref healRatePerLevelGovernor, "healRatePerLevelGovernor", DefaultHealRatePerLevelGovernor);
        Scribe_Values.Look(ref xpPerDay, "xpPerDay", DefaultXpPerDay);
        Scribe_Values.Look(ref skillTaperFactorBand1, "skillTaperFactorBand1", DefaultSkillTaperFactorBand1);
        Scribe_Values.Look(ref skillTaperFactorBand2, "skillTaperFactorBand2", DefaultSkillTaperFactorBand2);
        Scribe_Values.Look(ref skillTaperFactorBand3, "skillTaperFactorBand3", DefaultSkillTaperFactorBand3);
        Scribe_Values.Look(ref foodPerSpecialist, "foodPerSpecialist", DefaultFoodPerSpecialist);
        Scribe_Values.Look(ref foodPerResident, "foodPerResident", DefaultFoodPerResident);
        Scribe_Values.Look(ref medicinePerSpecialist, "medicinePerSpecialist", DefaultMedicinePerSpecialist);
        Scribe_Values.Look(ref foodPenaltyPerUnit, "foodPenaltyPerUnit", DefaultFoodPenaltyPerUnit);
        Scribe_Values.Look(ref medicinePenaltyPerUnit, "medicinePenaltyPerUnit", DefaultMedicinePenaltyPerUnit);
    }

    public static void ResetToDefaults()
    {
        printDebug = DefaultPrintDebug;
        specialistBaseMax = DefaultSpecialistBaseMax;
        specialistPerLevels = DefaultSpecialistPerLevels;
        residentsPerWorker = DefaultResidentsPerWorker;
        scalingFactor = DefaultScalingFactor;
        healRatePerLevelGovernor = DefaultHealRatePerLevelGovernor;
        xpPerDay = DefaultXpPerDay;
        skillTaperFactorBand1 = DefaultSkillTaperFactorBand1;
        skillTaperFactorBand2 = DefaultSkillTaperFactorBand2;
        skillTaperFactorBand3 = DefaultSkillTaperFactorBand3;
        foodPerSpecialist = DefaultFoodPerSpecialist;
        foodPerResident = DefaultFoodPerResident;
        medicinePerSpecialist = DefaultMedicinePerSpecialist;
        foodPenaltyPerUnit = DefaultFoodPenaltyPerUnit;
        medicinePenaltyPerUnit = DefaultMedicinePenaltyPerUnit;
    }

    public void DoWindowContents(Rect inRect)
    {
        Listing_Standard ls = new();
        Rect viewRect = ScrollUtil.BeginScrollView(inRect, ref scrollPos, contentHeight);
        ls.Begin(new Rect(viewRect.x, viewRect.y, viewRect.width, float.MaxValue));
        Listing_StandardExtensions.ResetRowStripe();

        ls.Label("FCS_ModVersion".Translate(SpecialistsMod.GetModVersion()));
        ls.Gap(10f);

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
    private static readonly SpecialistLifecycleHandler _lifecycleHandler = new();

    static SpecialistsStartup()
    {
        new Harmony("Matathias.Empire.Specialists").PatchAll(Assembly.GetExecutingAssembly());
        EmpireRegistry.RegisterPersistent(_lifecycleHandler);
    }
}

public class SpecialistsMod : Mod
{
    public FCSSettings settings;

    public SpecialistsMod(ModContentPack content) : base(content)
    {
        settings = GetSettings<FCSSettings>();
        FCSSettings.CheckForRoutesAndResources();

        ModVersionUtil.LogLoaded(content, LogSG.MessageForce);
    }

    public static string GetModVersion() => ModVersionUtil.Get<SpecialistsMod>();

    public override string SettingsCategory() => "FCS_SettingsCategory".Translate();

    public override void DoSettingsWindowContents(Rect inRect) => settings.DoWindowContents(inRect);
}
