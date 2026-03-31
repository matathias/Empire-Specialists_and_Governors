using Verse;

namespace FactionColonies.Specialists
{
    public enum SpecialistRole
    {
        Resident,
        Specialist,
        Defense,
        Governor
    }

    public static class SpecialistRoleExtensions
    {
        public static string Translate(this SpecialistRole role)
        {
            switch (role)
            {
                case SpecialistRole.Resident:   return "FCS_RoleResident".Translate();
                case SpecialistRole.Specialist:  return "FCS_RoleSpecialist".Translate();
                case SpecialistRole.Defense:     return "FCS_RoleDefense".Translate();
                case SpecialistRole.Governor:    return "FCS_RoleGovernor".Translate();
                default:                         return role.ToString();
            }
        }
    }
}
