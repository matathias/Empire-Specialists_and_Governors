namespace FactionColonies.Specialists;

public class SpecialistRoleBehaviorExtension : BehaviorExtension<SpecialistRoleBehavior>
{
    protected override void AttachTo(SpecialistRoleBehavior behavior) => behavior.extension = this;
}

public abstract class SpecialistRoleBehavior
{
    [Unsaved] public SpecialistRoleBehaviorExtension extension;

    public virtual void OnAssigned(WorldSettlementFC settlement, Pawn pawn) { }
    public virtual void OnRemoved(WorldSettlementFC settlement, Pawn pawn) { }
    public virtual double ModifyStatBonus(FCStatDef stat, double baseValue) { return baseValue; }

    public virtual void ModifyDeathChances(WorldSettlementFC settlement, float skillScore,
        ref float govChance, ref float specChance, ref float residentChance) { }

    protected T Ext<T>() where T : SpecialistRoleBehaviorExtension
    {
        return (T)extension;
    }
}
