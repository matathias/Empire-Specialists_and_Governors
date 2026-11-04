namespace FactionColonies.Specialists;

public static class SpecialistRoster
{
    private static Dictionary<int, SpecialistRoleDef> assignments = [];
    private static HashSet<int> governors = [];

    public static void Assign(Pawn pawn, SpecialistRoleDef role)
    {
        if (pawn is not null)
            assignments[pawn.thingIDNumber] = role;
    }

    public static void AssignGovernor(Pawn pawn)
    {
        if (pawn is not null)
            governors.Add(pawn.thingIDNumber);
    }

    public static void Recall(Pawn pawn)
    {
        if (pawn is null) return;
        assignments.Remove(pawn.thingIDNumber);
        governors.Remove(pawn.thingIDNumber);
    }

    public static SpecialistRoleDef GetRole(Pawn pawn)
    {
        if (pawn is not null && assignments.TryGetValue(pawn.thingIDNumber, out SpecialistRoleDef role))
            return role;
        return null;
    }

    public static bool IsGovernor(Pawn pawn)
    {
        return pawn is not null && governors.Contains(pawn.thingIDNumber);
    }

    public static bool IsAssigned(Pawn pawn)
    {
        if (pawn is null) return false;
        return assignments.ContainsKey(pawn.thingIDNumber) || governors.Contains(pawn.thingIDNumber);
    }

    public static void Rebuild()
    {
        assignments.Clear();
        governors.Clear();
        List<WorldSettlementFC> settlements = FindFC.Settlements;
        if (settlements is null) return;
        foreach (WorldSettlementFC settlement in settlements)
        {
            WorldObjectComp_SettlementSpecialists comp =
                settlement.GetComponent<WorldObjectComp_SettlementSpecialists>();
            if (comp is null) continue;

            foreach (SettlementSpecialist s in comp.Specialists)
            {
                if (s.IsAlive)
                    assignments[s.pawn.thingIDNumber] = s.role;
            }
            foreach (SettlementSpecialist r in comp.Residents)
            {
                if (r.IsAlive)
                    assignments[r.pawn.thingIDNumber] = null;
            }
            if (comp.HasGovernor)
            {
                governors.Add(comp.Governor.pawn.thingIDNumber);
            }
        }
    }
}

public abstract class RecordWorker_SpecialistBase : RecordWorker
{
}

public class RecordWorker_FCS_TimeAssigned : RecordWorker_SpecialistBase
{
    public override bool ShouldMeasureTimeNow(Pawn pawn)
    {
        return SpecialistRoster.IsAssigned(pawn);
    }
}

public class RecordWorker_FCS_TimeAsResident : RecordWorker_SpecialistBase
{
    public override bool ShouldMeasureTimeNow(Pawn pawn)
    {
        return SpecialistRoster.GetRole(pawn) is null && SpecialistRoster.IsAssigned(pawn) && !SpecialistRoster.IsGovernor(pawn);
    }
}

public class RecordWorker_FCS_TimeAsSpecialist : RecordWorker_SpecialistBase
{
    public override bool ShouldMeasureTimeNow(Pawn pawn)
    {
        SpecialistRoleDef role = SpecialistRoster.GetRole(pawn);
        return role is not null && !SpecialistRoster.IsGovernor(pawn);
    }
}

public class RecordWorker_FCS_TimeAsGovernor : RecordWorker_SpecialistBase
{
    public override bool ShouldMeasureTimeNow(Pawn pawn)
    {
        return SpecialistRoster.IsGovernor(pawn);
    }
}
