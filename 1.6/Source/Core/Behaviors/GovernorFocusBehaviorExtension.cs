using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies.Specialists
{
    public class GovernorFocusBehaviorExtension : DefModExtension
    {
        public Type behaviorClass;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;
            if (behaviorClass is null)
                yield return "GovernorFocusBehaviorExtension: behaviorClass is null";
            else if (!typeof(GovernorFocusBehavior).IsAssignableFrom(behaviorClass))
                yield return "GovernorFocusBehaviorExtension: behaviorClass must extend GovernorFocusBehavior";
        }

        public GovernorFocusBehavior CreateBehavior()
        {
            GovernorFocusBehavior b = (GovernorFocusBehavior)Activator.CreateInstance(behaviorClass);
            b.extension = this;
            return b;
        }
    }

    public abstract class GovernorFocusBehavior : IExposable
    {
        [Unsaved] public GovernorFocusBehaviorExtension extension;

        public virtual void OnFocusActivated(WorldSettlementFC settlement) { }
        public virtual void OnFocusDeactivated(WorldSettlementFC settlement) { }
        public virtual void OnTaxTick(WorldSettlementFC settlement) { }
        public virtual double ModifyStatBonus(FCStatDef stat, double baseValue) { return baseValue; }
        public virtual void ExposeData() { }

        protected T Ext<T>() where T : GovernorFocusBehaviorExtension
        {
            return (T)extension;
        }
    }
}
