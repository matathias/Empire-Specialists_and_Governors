using System.Text;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Dynamic Codex provider for the Residents entry. Shows the current
    /// residents-per-worker ratio, then each settlement's live resident count and
    /// the bonus workers those residents provide (via SpecUtil.WorkerBonusFromResidents).
    /// Settlements with no residents are omitted.
    /// </summary>
    public class CodexProvider_SG_Residents : ICodexDynamicProvider
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

                int residents = comp.LiveResidentCount;
                if (residents <= 0) continue;

                int workers = SpecUtil.WorkerBonusFromResidents(residents);
                sb.AppendLine(s.Name + ":  " + "FCS_CodexResidentsLine".Translate(residents, workers));
            }

            if (sb.Length == 0) return null;
            return "FCS_CodexResidentsHeader".Translate(FCSSettings.residentsPerWorker)
                + "\n\n" + sb.ToString().TrimEnd();
        }
    }
}
