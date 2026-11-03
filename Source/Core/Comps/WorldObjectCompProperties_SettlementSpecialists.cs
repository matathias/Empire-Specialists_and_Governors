using RimWorld;

namespace FactionColonies.Specialists
{
    public class WorldObjectCompProperties_SettlementSpecialists : WorldObjectCompProperties
    {
        public float governorDeathChanceDefeat = 0.10f;
        public float specialistDeathChanceDefeat = 0.20f;
        public float residentDeathChanceDefeat = 0.30f;
        public float governorDeathChanceVictory = 0.02f;
        public float specialistDeathChanceVictory = 0.05f;
        public float residentDeathChanceVictory = 0.10f;

        public WorldObjectCompProperties_SettlementSpecialists()
        {
            compClass = typeof(WorldObjectComp_SettlementSpecialists);
        }
    }
}
