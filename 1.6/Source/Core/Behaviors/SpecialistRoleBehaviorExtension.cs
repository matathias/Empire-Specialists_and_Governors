using System;
using System.Collections.Generic;
using Verse;

namespace FactionColonies.Specialists
{
    public class SpecialistRoleBehaviorExtension : DefModExtension
    {
        public Type behaviorClass;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string err in base.ConfigErrors())
                yield return err;
            if (behaviorClass is null)
                yield return "SpecialistRoleBehaviorExtension: behaviorClass is null";
            else if (!typeof(SpecialistRoleBehavior).IsAssignableFrom(behaviorClass))
                yield return "SpecialistRoleBehaviorExtension: behaviorClass must extend SpecialistRoleBehavior";
        }

        public SpecialistRoleBehavior CreateBehavior()
        {
            SpecialistRoleBehavior b = (SpecialistRoleBehavior)Activator.CreateInstance(behaviorClass);
            b.extension = this;
            return b;
        }
    }

    public abstract class SpecialistRoleBehavior : IExposable
    {
        [Unsaved] public SpecialistRoleBehaviorExtension extension;

        public virtual void OnAssigned(WorldSettlementFC settlement, Pawn pawn) { }
        public virtual void OnRemoved(WorldSettlementFC settlement, Pawn pawn) { }
        public virtual void OnTaxTick(WorldSettlementFC settlement) { }
        public virtual double ModifyStatBonus(FCStatDef stat, double baseValue) { return baseValue; }

        public virtual void ModifyDeathChances(WorldSettlementFC settlement, float skillScore,
            ref float govChance, ref float specChance, ref float residentChance) { }

        public virtual void ExposeData() { }

        protected T Ext<T>() where T : SpecialistRoleBehaviorExtension
        {
            return (T)extension;
        }
    }
}
