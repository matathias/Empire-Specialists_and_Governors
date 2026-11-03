using System;
using System.Text;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Dynamic Codex provider for the Battle Risk entry. Mirrors the death-chance
    /// calculation in SpecialistLifecycleHandler.OnBattleResolved: it starts from the
    /// per-tier base chances on the comp's properties, then lets each stationed
    /// specialist's behavior (e.g. the Commander role) reduce them, and reports the
    /// resulting victory/defeat chances per settlement. Settlements with no stationed
    /// colonists are omitted.
    /// </summary>
    public class CodexProvider_SG_BattleRisk : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            if (faction is null || faction.settlements is null) return null;

            StringBuilder sb = new StringBuilder();
            foreach (WorldSettlementFC s in faction.settlements)
            {
                WorldObjectComp_SettlementSpecialists comp =
                    s.GetComponent<WorldObjectComp_SettlementSpecialists>();
                if (comp is null || comp.TotalCount == 0) continue;

                WorldObjectCompProperties_SettlementSpecialists props = comp.Props;

                float govV = props.governorDeathChanceVictory;
                float specV = props.specialistDeathChanceVictory;
                float resV = props.residentDeathChanceVictory;
                float govD = props.governorDeathChanceDefeat;
                float specD = props.specialistDeathChanceDefeat;
                float resD = props.residentDeathChanceDefeat;

                // Apply each specialist behavior's death-chance reduction to both outcomes,
                // exactly as the lifecycle handler does for the actual battle outcome.
                foreach (SettlementSpecialist spec in comp.Specialists)
                {
                    if (spec.role is null || !spec.HasUsableSkills) continue;
                    SpecialistRoleBehavior behavior = spec.Behavior;
                    if (behavior is null) continue;
                    behavior.ModifyDeathChances(s, spec.SkillScore, ref govV, ref specV, ref resV);
                    behavior.ModifyDeathChances(s, spec.SkillScore, ref govD, ref specD, ref resD);
                }

                sb.AppendLine(s.Name + ":");
                sb.AppendLine("  " + "FCS_CodexRiskDefeat".Translate());
                sb.AppendLine("    " + "FCS_CodexRiskTiers".Translate(Pct(resD), Pct(specD), Pct(govD)));
                sb.AppendLine("  " + "FCS_CodexRiskVictory".Translate());
                sb.AppendLine("    " + "FCS_CodexRiskTiers".Translate(Pct(resV), Pct(specV), Pct(govV)));
                sb.AppendLine();
            }

            if (sb.Length == 0) return null;
            return "FCS_CodexRiskHeader".Translate() + "\n\n" + sb.ToString().TrimEnd();
        }

        private static double Pct(float chance)
        {
            return Math.Round(chance * 100.0);
        }
    }
}
