namespace FactionColonies.Specialists;

public class SettlementSpecialist : RosterEntry<SpecialistRoleDef, SpecialistRoleBehavior>
{
    public SpecialistRoleDef role;

    public SettlementSpecialist() { }

    public SettlementSpecialist(Pawn pawn, SpecialistRoleDef role) : base(pawn)
    {
        this.role = role;
    }

    public bool IsResident => role is null;

    protected override SpecialistRoleDef AssignedDef => role;

    public override void DirtySkillScore()
    {
        base.DirtySkillScore();
        ResetBehavior();
    }

    protected override float ComputeSkillScore()
    {
        return HasUsableSkills ? role?.ComputeSkillScore(pawn) ?? 0f : 0f;
    }

    protected override void ExposeDef()
    {
        Scribe_Defs.Look(ref role, "role");
    }
}
