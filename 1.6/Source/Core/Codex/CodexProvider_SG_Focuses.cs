using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Dynamic Codex provider for the Governor Focuses reference entry. Lists every
    /// loaded GovernorFocusDef and a qualitative summary of what its multiplier
    /// affects (which resources and stats, or "all production" for baseline focuses).
    /// Def-driven, so focuses added by other submods appear automatically. No numeric
    /// coefficients are shown - those are tuned and would drift.
    /// </summary>
    public class CodexProvider_SG_Focuses : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            List<GovernorFocusDef> focuses = DefDatabase<GovernorFocusDef>.AllDefsListForReading
                .OrderBy(f => f.label ?? f.defName).ToList();
            if (focuses.Count == 0) return null;

            StringBuilder sb = new StringBuilder();
            foreach (GovernorFocusDef focus in focuses)
                sb.AppendLine(CodexSGFormat.Label(focus) + " - " + Summarize(focus));

            return "FCS_CodexFocusesHeader".Translate() + "\n\n" + sb.ToString().TrimEnd();
        }

        private static string Summarize(GovernorFocusDef focus)
        {
            List<string> parts = new List<string>();

            if (focus.providesBaselineProduction)
                parts.Add("FCS_CodexAllProduction".Translate());

            CodexSGFormat.AddResourceLabels(parts, focus.resourceBonuses);
            CodexSGFormat.AddStatLabels(parts, focus.statModifiers);

            if (parts.Count == 0)
                return "FCS_CodexRoleGeneral".Translate();
            return "FCS_CodexBoosts".Translate(string.Join(", ", parts.ToArray()));
        }
    }
}
