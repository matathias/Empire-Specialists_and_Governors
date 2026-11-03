using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace FactionColonies.Specialists
{
    /// <summary>
    /// Dynamic Codex provider for the Specialist Roles reference entry. Lists every
    /// loaded SpecialistRoleDef and a qualitative summary of what it affects (which
    /// resources and stats it boosts, or "all production" for baseline/generalist
    /// roles). Def-driven, so roles added by other submods appear automatically.
    /// No numeric coefficients are shown - those are tuned and would drift.
    /// </summary>
    public class CodexProvider_SG_Roles : ICodexDynamicProvider
    {
        public string GetDynamicContent(FactionFC faction)
        {
            List<SpecialistRoleDef> roles = DefDatabase<SpecialistRoleDef>.AllDefsListForReading
                .OrderBy(r => r.label ?? r.defName).ToList();
            if (roles.Count == 0) return null;

            StringBuilder sb = new StringBuilder();
            foreach (SpecialistRoleDef role in roles)
                sb.AppendLine(CodexSGFormat.Label(role) + " - " + Summarize(role));

            return "FCS_CodexRolesHeader".Translate() + "\n\n" + sb.ToString().TrimEnd();
        }

        private static string Summarize(SpecialistRoleDef role)
        {
            List<string> parts = new List<string>();

            if (role.isGeneralist || role.providesBaselineProduction)
                parts.Add("FCS_CodexAllProduction".Translate());

            CodexSGFormat.AddResourceLabels(parts, role.resourceBonuses);
            CodexSGFormat.AddStatLabels(parts, role.statModifiers);

            if (parts.Count == 0)
                return "FCS_CodexRoleGeneral".Translate();
            return "FCS_CodexBoosts".Translate(string.Join(", ", parts.ToArray()));
        }
    }
}
