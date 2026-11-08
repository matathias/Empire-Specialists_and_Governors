using LudeonTK;
using System.Reflection;

namespace FactionColonies.Specialists;

/// <summary>
/// Specialists test runner: the debug-menu entry points for this mod's tests. Discovery, running and
/// reporting live in the base mod's <see cref="EmpireTestRunner"/>; this class only names the
/// suite (its assembly, the <see cref="LogSG"/> logger and its destructive-run warnings).
/// </summary>
public static class SpecTestRunner
{
    private const string DebugCategory = "Empire Refactored: Specialists & Governors";

    private static readonly EmpireTestSuite Suite = new(
        Assembly.GetExecutingAssembly(),
        "Specialists",
        "DESTRUCTIVE Specialists & Governors tests mutate live game state: they create "
            + "transient settlements and assign/recall pawns (which can build caravans). Cleanup "
            + "is best-effort and NOT guaranteed.",
        "Destructive tests use best-effort teardown (roster cleared, transient "
            + "settlements removed) but may leave residue: world pawns reassigned to the empire, "
            + "caravans created by recall paths, and any settlement not torn down. Reload your "
            + "pre-test save to restore the prior state.",
        LogSG.Message, LogSG.Error, LogSG.MessageForce);

    /*-*-*- Standard (non-destructive) tier -*-*-*/

    [DebugAction(DebugCategory, "Run All Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAll() => EmpireTestRunner.RunTests(Suite, null, destructive: false);

    [DebugAction(DebugCategory, "Run Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunByCategory() => EmpireTestRunner.ShowCategoryMenu(Suite, destructive: false);

    /*-*-*- Destructive tier (save-first, mutates live state) -*-*-*/

    [DebugAction(DebugCategory, "Run Destructive Tests", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunAllDestructive() =>
        EmpireTestRunner.ConfirmDestructive(Suite, () => EmpireTestRunner.RunTests(Suite, null, destructive: true));

    [DebugAction(DebugCategory, "Run Destructive Tests by Category", allowedGameStates = AllowedGameStates.Invalid)]
    public static void RunDestructiveByCategory() =>
        EmpireTestRunner.ConfirmDestructive(Suite, () => EmpireTestRunner.ShowCategoryMenu(Suite, destructive: true));
}
