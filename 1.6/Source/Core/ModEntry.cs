using UnityEngine;
using Verse;

namespace FactionColonies.Specialists
{
    public class FCSSettings : ModSettings
    {
        private static bool printDebug = false;
        public static bool PrintDebug => printDebug;

        // Workers
        public static int residentsPerWorker = 5;

        // Upkeep
        public static float specialistBaseCost = 3f;
        public static float skillDivisor = 20f;
        public static float scalingFactor = 1f;

        // XP
        public static float xpPerDay = 500f;

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
            Scribe_Values.Look(ref residentsPerWorker, "residentsPerWorker", 5);
            Scribe_Values.Look(ref specialistBaseCost, "specialistBaseCost", 3f);
            Scribe_Values.Look(ref skillDivisor, "skillDivisor", 20f);
            Scribe_Values.Look(ref scalingFactor, "scalingFactor", 1f);
            Scribe_Values.Look(ref xpPerDay, "xpPerDay", 500f);
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
            ls.CheckboxLabeled("Enable debug logging", ref printDebug);

            // Workers
            ls.Gap(12f);
            ls.Label("Residents per bonus worker: " + residentsPerWorker);
            residentsPerWorker = (int)ls.Slider(residentsPerWorker, 1, 20);

            // Upkeep
            ls.Gap(8f);
            ls.Label("Specialist base upkeep (silver/day): " + specialistBaseCost.ToString("F1"));
            specialistBaseCost = (float)System.Math.Round(ls.Slider(specialistBaseCost, 0f, 20f), 1);
            ls.Label("Upkeep skill divisor: " + skillDivisor.ToString("F0"));
            skillDivisor = (float)System.Math.Round(ls.Slider(skillDivisor, 1f, 50f), 0);
            ls.Label("Upkeep scaling factor: " + scalingFactor.ToString("F1"));
            scalingFactor = (float)System.Math.Round(ls.Slider(scalingFactor, 0f, 5f), 1);

            // XP
            ls.Gap(8f);
            ls.Label("XP per day per skill: " + xpPerDay.ToString("F0"));
            xpPerDay = (float)System.Math.Round(ls.Slider(xpPerDay, 0f, 2000f), 0);

            // Death chances
            ls.Gap(8f);
            ls.Label("Civilian specialist death chance: " + (civilianDeathChance * 100f).ToString("F0") + "%");
            civilianDeathChance = (float)System.Math.Round(ls.Slider(civilianDeathChance, 0f, 1f), 2);
            ls.Label("Governor death chance: " + (governorDeathChance * 100f).ToString("F0") + "%");
            governorDeathChance = (float)System.Math.Round(ls.Slider(governorDeathChance, 0f, 1f), 2);
            ls.Label("Defense specialist death chance: " + (defenseDeathChance * 100f).ToString("F0") + "%");
            defenseDeathChance = (float)System.Math.Round(ls.Slider(defenseDeathChance, 0f, 1f), 2);
            ls.Label("Resident death chance: " + (residentDeathChance * 100f).ToString("F0") + "%");
            residentDeathChance = (float)System.Math.Round(ls.Slider(residentDeathChance, 0f, 1f), 2);
            ls.Label("Death reduction per defender: " + (deathReductionPerDefender * 100f).ToString("F0") + "%");
            deathReductionPerDefender = (float)System.Math.Round(ls.Slider(deathReductionPerDefender, 0f, 0.1f), 2);

            ls.End();
        }
    }

    [StaticConstructorOnStartup]
    public static class SpecialistsStartup
    {
        static SpecialistsStartup()
        {
            LifecycleRegistry.Register(new SpecialistLifecycleHandler());
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
