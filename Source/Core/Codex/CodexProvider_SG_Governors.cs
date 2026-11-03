using System;
using System.Text;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Dynamic Codex provider for the Governors entry. Lists each settlement that has
    /// a governor, naming the pawn, their focus, and their current skill score.
    /// Settlements without a governor are omitted.
    /// </summary>
    public class CodexProvider_SG_Governors : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null || faction.settlements is null) return null;

            StringBuilder sb = new StringBuilder();
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_SettlementSpecialists comp =
                    s.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null || !comp.HasGovernor) continue;

                SettlementGovernor gov = comp.Governor;
                string focusLabel = gov.focus is object
                    ? gov.focus.LabelCap.ToString()
                    : "FCS_FocusNone".Translate().ToString();

                sb.AppendLine(s.Name + ": " + gov.pawn.LabelShortCap + " - " + focusLabel
                    + " (" + "FCS_CodexScore".Translate(Math.Round(gov.SkillScore, 1)) + ")");
            }

            if (sb.Length == 0) return null;
            return "FCS_CodexGovHeader".Translate() + "\n\n" + sb.ToString().TrimEnd();
        }
    }
}
