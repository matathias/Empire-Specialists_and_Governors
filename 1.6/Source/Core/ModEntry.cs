using UnityEngine;
using Verse;

namespace FactionColonies.Specialists
{
    public class FCSSettings : ModSettings
    {
        private static bool printDebug = false;
        public static bool PrintDebug => printDebug;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref printDebug, "printDebug", false);
        }

        public void DoWindowContents(Rect inRect)
        {
            Listing_Standard ls = new Listing_Standard();
            ls.Begin(inRect);
            ls.CheckboxLabeled("Enable debug logging", ref printDebug);
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
