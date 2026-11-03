namespace FactionColonies.Specialists
{
    public class RoleBehaviorExt_Apothecary : SpecialistRoleBehaviorExtension
    {
        public float healRatePerSkillPoint = 0.02f;
    }

    /// <summary>
    /// Carrier for the healRatePerSkillPoint config.
    /// The actual heal rate calculation is done in SpecUtil.MedicalHealRateBonus
    /// by detecting the extension on the role.
    /// </summary>
    public class RoleBehavior_Apothecary : SpecialistRoleBehavior
    {
    }
}
