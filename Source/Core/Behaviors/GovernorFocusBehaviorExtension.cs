namespace FactionColonies.Specialists;

public class GovernorFocusBehaviorExtension : BehaviorExtension<GovernorFocusBehavior>
{
    protected override void AttachTo(GovernorFocusBehavior behavior) => behavior.extension = this;
}

public abstract class GovernorFocusBehavior
{
    [Unsaved] public GovernorFocusBehaviorExtension extension;

    public virtual void OnFocusActivated(WorldSettlementFC settlement) { }
    public virtual void OnFocusDeactivated(WorldSettlementFC settlement) { }
    public virtual double ModifyStatBonus(FCStatDef stat, double baseValue) { return baseValue; }

    protected T Ext<T>() where T : GovernorFocusBehaviorExtension
    {
        return (T)extension;
    }
}
