using UnityEngine;
using Verse;

namespace FactionColonies.Specialists
{
    public class FCSSettings : ModSettings
    {
        private static bool printDebug = false;
        public static bool PrintDebug => printDebug;

        public static int residentsPerWorker = 5;
        public static float specialistBaseCost = 3f;
        public static float skillDivisor = 20f;
        public static float scalingFactor = 1f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref printDebug, "printDebug", false);
            Scribe_Values.Look(ref residentsPerWorker, "residentsPerWorker", 5);
            Scribe_Values.Look(ref specialistBaseCost, "specialistBaseCost", 3f);
            Scribe_Values.Look(ref skillDivisor, "skillDivisor", 20f);
            Scribe_Values.Look(ref scalingFactor, "scalingFactor", 1f);
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("Enable debug logging", ref printDebug);
            ls.Gap(12f);
            ls.Label("Residents per bonus worker: " + residentsPerWorker);
            residentsPerWorker = (int)ls.Slider(residentsPerWorker, 1, 20);
            ls.Gap(8f);
            ls.Label("Specialist base upkeep (silver/day): " + specialistBaseCost.ToString("F1"));
            specialistBaseCost = (float)System.Math.Round(ls.Slider(specialistBaseCost, 0f, 20f), 1);
            ls.Label("Upkeep skill divisor: " + skillDivisor.ToString("F0"));
            skillDivisor = (float)System.Math.Round(ls.Slider(skillDivisor, 1f, 50f), 0);
            ls.Label("Upkeep scaling factor: " + scalingFactor.ToString("F1"));
            scalingFactor = (float)System.Math.Round(ls.Slider(scalingFactor, 0f, 5f), 1);
            ls.End();
        }
    }

    public class SpecialistsMod : Mod
    {
        public FCSSettings settings;

        public SpecialistsMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<FCSSettings>();
        }

        public override string SettingsCategory() => "Empire - Specialists";

        public override void DoSettingsWindowContents(Rect inRect) => settings.DoWindowContents(inRect);
    }
}
