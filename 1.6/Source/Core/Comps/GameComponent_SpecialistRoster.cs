using Verse;

namespace FactionColonies.Specialists
{
    public class GameComponent_SpecialistRoster : GameComponent
    {
        public GameComponent_SpecialistRoster(Game game)
        {
        }

        public override void FinalizeInit()
        {
            SpecialistRoster.Rebuild();
        }
    }
}
