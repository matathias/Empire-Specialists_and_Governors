using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Small formatting helpers shared by the def-driven Codex reference providers
    /// (Specialist Roles and Governor Focuses). Builds qualitative bonus summaries
    /// from a def's resource/stat modifier lists - names only, never coefficients.
    /// </summary>
    internal static class CodexSGFormat
    {
        public static string Label(Def def)
        {
            if (def is null) return "";
            return def.label.NullOrEmpty() ? def.defName : def.LabelCap.ToString();
        }

        public static void AddResourceLabels(List<string> parts, List<ResourceProductionBonus> bonuses)
        {
            if (bonuses is null) return;
            foreach (ResourceProductionBonus rpb in bonuses)
            {
                if (rpb.resource is null || Math.Abs(rpb.baseValue) < 0.0001f) continue;
                string label = Label(rpb.resource);
                if (!parts.Contains(label)) parts.Add(label);
            }
        }

        public static void AddStatLabels(List<string> parts, List<FCStatModifier> mods)
        {
            if (mods is null) return;
            foreach (FCStatModifier mod in mods)
            {
                if (mod.stat is null) continue;
                string label = Label(mod.stat);
                if (!parts.Contains(label)) parts.Add(label);
            }
        }
    }
}
