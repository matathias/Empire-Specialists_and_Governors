using RimWorld;
using Verse;

namespace FactionColonies.Specialists
{
    public class SettlementSpecialist : IExposable
    {
        public Pawn pawn;
        public SpecialistRoleDef role;
        public int assignedTick;

        [Unsaved] private float cachedSkillScore = -1f;

        public SettlementSpecialist() { }

        public SettlementSpecialist(Pawn pawn, SpecialistRoleDef role)
        {
            this.pawn = pawn;
            this.role = role;
            this.assignedTick = Find.TickManager.TicksGame;
        }

        public bool IsAlive => pawn is object && !pawn.Dead;
        public bool HasUsableSkills => pawn?.skills is object && !pawn.Dead;
        public bool IsResident => role is null;

        public float SkillScore
        {
            get
            {
                if (cachedSkillScore >= 0f) return cachedSkillScore;
                cachedSkillScore = ComputeSkillScore();
                return cachedSkillScore;
            }
        }

        public void DirtySkillScore() { cachedSkillScore = -1f; }

        private float ComputeSkillScore()
        {
            if (role is null || !HasUsableSkills || role.skillWeights is null) return 0f;
            float score = 0f;
            foreach (SkillWeight sw in role.skillWeights)
            {
                if (sw.skill is null) continue;
                SkillRecord rec = pawn.skills.GetSkill(sw.skill);
                if (rec is object)
                    score += rec.Level * sw.weight;
            }
            return score;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Defs.Look(ref role, "role");
            Scribe_Values.Look(ref assignedTick, "assignedTick");
        }
    }
}
