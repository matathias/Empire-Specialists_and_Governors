namespace FactionColonies.Specialists;

/// <summary>
/// One member of a settlement's roster: a specialist, a resident or the governor.
/// Holds the pawn, the assignment tick and the cached skill score.
/// </summary>
public abstract class RosterEntry : IExposable
{
    public Pawn pawn;
    public int assignedTick;

    [Unsaved] private float cachedSkillScore = -1f;

    protected RosterEntry() { }

    protected RosterEntry(Pawn pawn)
    {
        this.pawn = pawn;
        this.assignedTick = Find.TickManager.TicksGame;
    }

    public bool IsAlive => pawn is not null && !pawn.Dead;
    public bool HasUsableSkills => pawn?.skills is not null && !pawn.Dead;

    public float SkillScore
    {
        get
        {
            if (cachedSkillScore >= 0f) return cachedSkillScore;
            cachedSkillScore = ComputeSkillScore();
            return cachedSkillScore;
        }
    }

    public virtual void DirtySkillScore() { cachedSkillScore = -1f; }

    protected abstract float ComputeSkillScore();

    /* Scribes the role or focus def between the pawn and the assignment tick */
    protected abstract void ExposeDef();

    public void ExposeData()
    {
        Scribe_References.Look(ref pawn, "pawn");
        ExposeDef();
        Scribe_Values.Look(ref assignedTick, "assignedTick");
    }
}

/// <summary>
/// A roster member assigned to a def (role or focus) whose behavior class, if any, comes from a
/// <see cref="BehaviorExtension{TBehavior}"/> on that def and is created lazily.
/// </summary>
public abstract class RosterEntry<TDef, TBehavior> : RosterEntry
    where TDef : Def
    where TBehavior : class
{
    [Unsaved] private TBehavior behavior;
    [Unsaved] private bool behaviorResolved = false;

    protected RosterEntry() { }

    protected RosterEntry(Pawn pawn) : base(pawn) { }

    /* The role or focus this member is assigned to */
    protected abstract TDef AssignedDef { get; }

    public TBehavior Behavior
    {
        get
        {
            if (!behaviorResolved)
            {
                behaviorResolved = true;
                behavior = AssignedDef?.GetModExtension<BehaviorExtension<TBehavior>>()?.CreateBehavior();
            }
            return behavior;
        }
    }

    /* Drops the cached behavior so the next access re-creates it from the current def */
    protected void ResetBehavior()
    {
        behaviorResolved = false;
        behavior = null;
    }
}
