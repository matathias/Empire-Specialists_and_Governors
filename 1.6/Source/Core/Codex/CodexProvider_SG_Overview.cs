using System.Text;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Dynamic Codex provider for the Specialists &amp; Governors overview entry.
    /// Lists, per settlement, whether a governor is appointed, how many specialists
    /// are filled against the settlement's cap, and the live resident headcount.
    /// </summary>
    public class CodexProvider_SG_Overview : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null || faction.settlements is null) return null;

            StringBuilder sb = new StringBuilder();
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_SettlementSpecialists comp =
                    s.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null) continue;

                string gov = comp.HasGovernor
                    ? "FCS_CodexYes".Translate()
                    : "FCS_CodexNone".Translate();

                sb.AppendLine(s.Name + ":");
                sb.AppendLine("  " + "FCS_CodexOverviewGovernor".Translate(gov));
                sb.AppendLine("  " + "FCS_CodexOverviewSpecialists".Translate(comp.SpecialistCount, comp.MaxSpecialists));
                sb.AppendLine("  " + "FCS_CodexOverviewResidents".Translate(comp.LiveResidentCount));
                sb.AppendLine();
            }

            if (sb.Length == 0) return null;
            return "FCS_CodexOverviewHeader".Translate() + "\n\n" + sb.ToString().TrimEnd();
        }
    }
}
