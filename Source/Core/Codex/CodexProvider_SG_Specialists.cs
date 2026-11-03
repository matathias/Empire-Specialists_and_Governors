using System;
using System.Text;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Dynamic Codex provider for the Specialists entry. Lists, per settlement, how
    /// many specialists are filled against the settlement's cap, then each specialist
    /// by name, role, and current skill score. Settlements with no specialists are omitted.
    /// </summary>
    public class CodexProvider_SG_Specialists : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null || faction.settlements is null) return null;

            StringBuilder sb = new StringBuilder();
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_SettlementSpecialists comp =
                    s.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null || comp.SpecialistCount == 0) continue;

                sb.AppendLine(s.Name + " - "
                    + "FCS_CodexSpecCount".Translate(comp.SpecialistCount, comp.MaxSpecialists));

                foreach (SettlementSpecialist spec in comp.Specialists)
                {
                    if (!spec.IsAlive || spec.role is null) continue;
                    sb.AppendLine("  " + spec.pawn.LabelShortCap + " - " + spec.role.LabelCap
                        + " (" + "FCS_CodexScore".Translate(Math.Round(spec.SkillScore, 1)) + ")");
                }
                sb.AppendLine();
            }

            if (sb.Length == 0) return null;
            return "FCS_CodexSpecHeader".Translate() + "\n\n" + sb.ToString().TrimEnd();
        }
    }
}
