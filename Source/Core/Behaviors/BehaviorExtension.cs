namespace FactionColonies.Specialists;

/// <summary>
/// Shared base for the def mod extensions that attach a behavior class to a role or focus def.
/// Validates <see cref="behaviorClass"/> and instantiates it, handing the new behavior a
/// back-reference to this extension through <see cref="AttachTo"/>.
/// </summary>
public abstract class BehaviorExtension<TBehavior> : DefModExtension where TBehavior : class
{
    public Type behaviorClass;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string err in base.ConfigErrors())
            yield return err;
        if (behaviorClass is null)
            yield return $"{GetType().Name}: behaviorClass is null";
        else if (!typeof(TBehavior).IsAssignableFrom(behaviorClass))
            yield return $"{GetType().Name}: behaviorClass must extend {typeof(TBehavior).Name}";
    }

    public TBehavior CreateBehavior()
    {
        try
        {
            TBehavior b = (TBehavior)Activator.CreateInstance(behaviorClass);
            AttachTo(b);
            return b;
        }
        catch (Exception e)
        {
            LogSG.Error($"Failed to create {typeof(TBehavior).Name} of type {behaviorClass}: {e}");
            return null;
        }
    }

    /* Wires the behavior's back-reference to this extension */
    protected abstract void AttachTo(TBehavior behavior);
}
