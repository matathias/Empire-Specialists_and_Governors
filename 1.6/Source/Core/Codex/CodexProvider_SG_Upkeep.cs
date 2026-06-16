using System;
using System.Text;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Dynamic Codex provider for the Upkeep &amp; Supply entry. Shows each settlement's
    /// current staff wage bill (GetUpkeepContribution, the sum of specialist and
    /// governor silver upkeep), plus food/medicine satisfaction wherever the Routes &amp;
    /// Resources supply system is throttling bonuses (satisfaction below full).
    /// Settlements with no wage bill and full supply are omitted.
    /// </summary>
    public class CodexProvider_SG_Upkeep : ICodexDynamicProvider
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

                double upkeep = comp.GetUpkeepContribution();
                bool foodShort = comp.FoodSatisfaction < 0.999f;
                bool medShort = comp.MedicineSatisfaction < 0.999f;
                if (upkeep <= 0 && !foodShort && !medShort) continue;

                sb.AppendLine(s.Name + ":");
                sb.AppendLine("  " + "FCS_CodexUpkeepSilver".Translate(Math.Round(upkeep, 1)));
                if (foodShort)
                    sb.AppendLine("  " + "FCS_CodexUpkeepFood".Translate(Math.Round(comp.FoodSatisfaction * 100.0)));
                if (medShort)
                    sb.AppendLine("  " + "FCS_CodexUpkeepMed".Translate(Math.Round(comp.MedicineSatisfaction * 100.0)));
                sb.AppendLine();
            }

            if (sb.Length == 0) return null;
            return "FCS_CodexUpkeepHeader".Translate() + "\n\n" + sb.ToString().TrimEnd();
        }
    }
}
