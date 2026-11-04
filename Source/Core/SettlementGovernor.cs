namespace FactionColonies.Specialists;

public class SettlementGovernor : IExposable
{
    public Pawn pawn;
    public GovernorFocusDef focus;
    public int assignedTick;

    [Unsaved] private GovernorFocusBehavior behavior;
    [Unsaved] private bool behaviorResolved = false;
    [Unsaved] private float cachedSkillScore = -1f;

    public SettlementGovernor() { }

    public SettlementGovernor(Pawn pawn, GovernorFocusDef focus)
    {
        this.pawn = pawn;
        this.focus = focus;
        this.assignedTick = Find.TickManager.TicksGame;
    }

    public bool IsAlive => pawn is not null && !pawn.Dead;
    public bool HasUsableSkills => pawn?.skills is not null && !pawn.Dead;

    public float SkillScore
    {
        get
        {
            if (cachedSkillScore >= 0f) return cachedSkillScore;
            cachedSkillScore = focus is not null ? focus.ComputeSkillScore(pawn) : 0f;
            return cachedSkillScore;
        }
    }

    public void DirtySkillScore() { cachedSkillScore = -1f; }

    public GovernorFocusBehavior Behavior
    {
        get
        {
            if (!behaviorResolved)
            {
                behaviorResolved = true;
                GovernorFocusBehaviorExtension ext =
                    focus?.GetModExtension<GovernorFocusBehaviorExtension>();
                behavior = ext?.CreateBehavior();
            }
            return behavior;
        }
    }

    public void SetFocus(GovernorFocusDef newFocus, WorldSettlementFC settlement)
    {
        if (Behavior is not null && settlement is not null)
            Behavior.OnFocusDeactivated(settlement);

        focus = newFocus;
        behaviorResolved = false;
        behavior = null;
        DirtySkillScore();

        if (Behavior is not null && settlement is not null)
            Behavior.OnFocusActivated(settlement);
    }

    public void ExposeData()
    {
        Scribe_References.Look(ref pawn, "pawn");
        Scribe_Defs.Look(ref focus, "focus");
        Scribe_Values.Look(ref assignedTick, "assignedTick");
    }
}
