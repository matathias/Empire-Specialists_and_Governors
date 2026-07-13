using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
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

        public bool IsAlive => pawn is object && !pawn.Dead;
        public bool HasUsableSkills => pawn?.skills is object && !pawn.Dead;

        public float SkillScore
        {
            get
            {
                if (cachedSkillScore >= 0f) return cachedSkillScore;
                cachedSkillScore = focus is object ? focus.ComputeSkillScore(pawn) : 0f;
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
            if (Behavior is object && settlement is object)
                Behavior.OnFocusDeactivated(settlement);

            focus = newFocus;
            behaviorResolved = false;
            behavior = null;
            DirtySkillScore();

            if (Behavior is object && settlement is object)
                Behavior.OnFocusActivated(settlement);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Defs.Look(ref focus, "focus");
            Scribe_Values.Look(ref assignedTick, "assignedTick");
        }
    }
}
