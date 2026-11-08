namespace FactionColonies.Specialists;

public class SettlementGovernor : RosterEntry<GovernorFocusDef, GovernorFocusBehavior>
{
    public GovernorFocusDef focus;

    public SettlementGovernor() { }

    public SettlementGovernor(Pawn pawn, GovernorFocusDef focus) : base(pawn)
    {
        this.focus = focus;
    }

    protected override GovernorFocusDef AssignedDef => focus;

    protected override float ComputeSkillScore()
    {
        return focus is not null ? focus.ComputeSkillScore(pawn) : 0f;
    }

    public void SetFocus(GovernorFocusDef newFocus, WorldSettlementFC settlement)
    {
        if (Behavior is not null && settlement is not null)
            Behavior.OnFocusDeactivated(settlement);

        focus = newFocus;
        ResetBehavior();
        DirtySkillScore();

        if (Behavior is not null && settlement is not null)
            Behavior.OnFocusActivated(settlement);
    }

    protected override void ExposeDef()
    {
        Scribe_Defs.Look(ref focus, "focus");
    }
}
