using System.IO;
using System.Runtime.CompilerServices;
using System.Xml;

namespace FactionColonies.Specialists;

/// <summary>
/// Non-destructive coverage for the roster helpers: per-role caps with pending picks, combined
/// supply satisfaction, the top-skills label, weighted skill learning, assignability, the
/// settings defaults, and the save format of roster entries. Pawns are controlled, never-spawned
/// throwaways; settings are restored after use.
/// </summary>
public static class SpecRosterLogicTests
{
    /*-*-*- RoleAtCap with pending picks (empty roster, so only pending counts) -*-*-*/

    [EmpireTest("SG.Roster")]
    public static void RoleAtCap_PendingCountsTowardCap()
    {
        WorldObjectComp_SettlementSpecialists comp = new();
        SpecialistRoleDef role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f, maxPerSettlement: 2);
        TestAssert.IsFalse(comp.RoleAtCap(role, 0), "no holders and nothing pending: not at cap");
        TestAssert.IsFalse(comp.RoleAtCap(role, 1), "one pending of two: not at cap");
        TestAssert.IsTrue(comp.RoleAtCap(role, 2), "two pending of two: at cap");
        TestAssert.IsTrue(comp.RoleAtCap(role, 3), "over the cap: at cap");
    }

    [EmpireTest("SG.Roster")]
    public static void RoleAtCap_SingleArgument_MatchesZeroPending()
    {
        WorldObjectComp_SettlementSpecialists comp = new();
        SpecialistRoleDef role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f, maxPerSettlement: 1);
        TestAssert.AreEqual(comp.RoleAtCap(role, 0), comp.RoleAtCap(role), "RoleAtCap(role) should equal RoleAtCap(role, 0)");
    }

    [EmpireTest("SG.Roster")]
    public static void RoleAtCap_UncappedOrResident_NeverAtCap()
    {
        WorldObjectComp_SettlementSpecialists comp = new();
        SpecialistRoleDef uncapped = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f);
        TestAssert.IsFalse(comp.RoleAtCap(uncapped, 50), "a role without maxPerSettlement is never at cap");
        TestAssert.IsFalse(comp.RoleAtCap(null, 50), "residents (null role) are never at cap");
    }

    /*-*-*- Satisfaction -*-*-*/

    [EmpireTest("SG.Roster")]
    public static void Satisfaction_IsFoodTimesMedicine()
    {
        WorldObjectComp_SettlementSpecialists comp = new()
        {
            FoodSatisfaction = 0.5f,
            MedicineSatisfaction = 0.4f
        };
        TestAssert.AreEqual(0.2, comp.Satisfaction, 0.0001);
    }

    [EmpireTest("SG.Roster")]
    public static void Satisfaction_FreshComp_IsOne()
    {
        TestAssert.AreEqual(1.0, new WorldObjectComp_SettlementSpecialists().Satisfaction, 0.0001);
    }

    [EmpireTest("SG.Roster")]
    public static void Members_FreshComp_IsEmpty()
    {
        TestAssert.IsEmpty(new WorldObjectComp_SettlementSpecialists().Members, "a fresh comp has no members");
    }

    /*-*-*- Best*Resource guards -*-*-*/

    [EmpireTest("SG.Roster")]
    public static void BestResource_NullSettlement_ReturnsNullAndZero()
    {
        SettlementSpecialist s = SpecTestHelper.Specialist(null, SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f));
        TestAssert.IsNull(SpecUtil.BestSpecialistResource(s, null, out double bonus), "no settlement: no best resource");
        TestAssert.AreEqual(0.0, bonus);

        SettlementGovernor g = SpecTestHelper.Governor(null, SpecTestHelper.Focus(SkillDefOf.Plants, 1f, null, 0f));
        TestAssert.IsNull(SpecUtil.BestGovernorResource(g, null, out double mult), "no settlement: no best resource");
        TestAssert.AreEqual(0.0, mult);
    }

    /*-*-*- TopSkillsLabel -*-*-*/

    private static string Label(SkillRecord rec) => $"{rec.def.skillLabel.CapitalizeFirst()} {rec.Level}";

    // Every skill at 1, then skills[3] = 10, skills[1] = 10 (a tie, earlier in skill order) and
    // skills[5] = 7. Returns the pawn's skill records.
    private static List<SkillRecord> RankedPawn(out Pawn pawn)
    {
        pawn = SpecTestHelper.MakeControlledPawn(1);
        List<SkillRecord> skills = pawn.skills.skills;
        if (skills.Count < 6) TestAssert.Fail("Expected at least six skills");
        skills[3].Level = 10;
        skills[1].Level = 10;
        skills[5].Level = 7;
        return skills;
    }

    [EmpireTest("SG.Roster")]
    public static void TopSkillsLabel_HighestFirst_TiesInSkillOrder()
    {
        List<SkillRecord> skills = RankedPawn(out Pawn pawn);
        string expected = $"{Label(skills[1])}, {Label(skills[3])}, {Label(skills[5])}";
        TestAssert.AreEqual(expected, SpecUtil.TopSkillsLabel(pawn, 3));
    }

    [EmpireTest("SG.Roster")]
    public static void TopSkillsLabel_CountLimitsEntries()
    {
        List<SkillRecord> skills = RankedPawn(out Pawn pawn);
        TestAssert.AreEqual(Label(skills[1]), SpecUtil.TopSkillsLabel(pawn, 1));
        TestAssert.AreEqual($"{Label(skills[1])}, {Label(skills[3])}", SpecUtil.TopSkillsLabel(pawn, 2));
    }

    [EmpireTest("SG.Roster")]
    public static void TopSkillsLabel_RepeatedCalls_AreStable()
    {
        RankedPawn(out Pawn pawn);
        string first = SpecUtil.TopSkillsLabel(pawn, 3);
        TestAssert.AreEqual(first, SpecUtil.TopSkillsLabel(pawn, 3), "the shared buffer must not leak between calls");
        TestAssert.AreEqual(first, SpecUtil.TopSkillsLabel(pawn, 3));
    }

    [EmpireTest("SG.Roster")]
    public static void TopSkillsLabel_ZeroCountOrNullPawn_IsEmpty()
    {
        RankedPawn(out Pawn pawn);
        TestAssert.AreEqual("", SpecUtil.TopSkillsLabel(pawn, 0));
        TestAssert.AreEqual("", SpecUtil.TopSkillsLabel(null, 3));
    }

    [EmpireTest("SG.Roster")]
    public static void TopRoleSkillsLabel_OnlyRoleSkills()
    {
        List<SkillRecord> skills = RankedPawn(out Pawn pawn);
        // The role only weights skills[5] and skills[0]; skills[1] and skills[3] are higher but irrelevant.
        SpecialistRoleDef role = new()
        {
            defName = "TEST_TopSkillsRole",
            label = "test top skills role",
            skillWeights =
            [
                new() { skill = skills[5].def, weight = 1f },
                new() { skill = skills[0].def, weight = 1f }
            ]
        };
        TestAssert.AreEqual($"{Label(skills[5])}, {Label(skills[0])}", SpecUtil.TopRoleSkillsLabel(pawn, role));
    }

    /*-*-*- LearnWeighted -*-*-*/

    [EmpireTest("SG.Roster")]
    public static void LearnWeighted_SplitsXpByWeight()
    {
        Pawn p = SpecTestHelper.MakeControlledPawn(5);
        SkillRecord plants = p.skills.GetSkill(SkillDefOf.Plants);
        SkillRecord mining = p.skills.GetSkill(SkillDefOf.Mining);
        SkillRecord cooking = p.skills.GetSkill(SkillDefOf.Cooking);
        plants.xpSinceLastLevel = 0f;
        mining.xpSinceLastLevel = 0f;
        cooking.xpSinceLastLevel = 0f;

        List<SkillWeight> weights =
        [
            new() { skill = SkillDefOf.Plants, weight = 1f },
            new() { skill = SkillDefOf.Mining, weight = 0.5f },
            new() { skill = null, weight = 5f }
        ];
        SpecUtil.LearnWeighted(p, weights, 100f);

        TestAssert.AreEqual(5, plants.Level, "the test xp must not level the skill up");
        TestAssert.GreaterThan(plants.xpSinceLastLevel, 0.0, "a weighted skill should gain xp");
        TestAssert.AreEqual(2.0, plants.xpSinceLastLevel / mining.xpSinceLastLevel, 0.001,
            "a weight of 1 should gain twice the xp of a weight of 0.5");
        TestAssert.AreEqual(0.0, cooking.xpSinceLastLevel, 0.0001, "an unweighted skill should gain nothing");
    }

    [EmpireTest("SG.Roster")]
    public static void LearnWeighted_NullInputs_DoNotThrow()
    {
        Pawn p = SpecTestHelper.MakeControlledPawn(5);
        TestAssert.DoesNotThrow(() => SpecUtil.LearnWeighted(null, [new() { skill = SkillDefOf.Plants, weight = 1f }], 100f));
        TestAssert.DoesNotThrow(() => SpecUtil.LearnWeighted(p, null, 100f));
    }

    /*-*-*- IsAssignablePawn (quest cases are covered by the destructive tier) -*-*-*/

    [EmpireTest("SG.Roster")]
    public static void IsAssignablePawn_NullIsRejected()
    {
        TestAssert.IsFalse(SpecUtil.IsAssignablePawn(null));
    }

    [EmpireTest("SG.Roster")]
    public static void IsAssignablePawn_AdultColonist_IsAccepted()
    {
        Pawn p = SpecTestHelper.MakeControlledPawn(5);
        TestAssert.IsTrue(SpecUtil.IsAssignablePawn(p), "a healthy adult colonist with no quest ties should be assignable");
    }

    /*-*-*- Settings defaults -*-*-*/

    [UnsafeAccessor(UnsafeAccessorKind.StaticField, Name = "printDebug")]
    private static extern ref bool PrintDebugSetting(FCSSettings settings);

    [EmpireTest("SG.Roster", GameState = TestGameState.Any)]
    public static void ResetToDefaults_RestoresShippedValues()
    {
        ref bool printDebug = ref PrintDebugSetting(null);
        bool savedPrintDebug = printDebug;
        int baseMax = FCSSettings.specialistBaseMax, perLevels = FCSSettings.specialistPerLevels;
        int perWorker = FCSSettings.residentsPerWorker;
        float scaling = FCSSettings.scalingFactor, heal = FCSSettings.healRatePerLevelGovernor, xp = FCSSettings.xpPerDay;
        float band1 = FCSSettings.skillTaperFactorBand1, band2 = FCSSettings.skillTaperFactorBand2, band3 = FCSSettings.skillTaperFactorBand3;
        float foodSpec = FCSSettings.foodPerSpecialist, foodRes = FCSSettings.foodPerResident, medSpec = FCSSettings.medicinePerSpecialist;
        float foodPen = FCSSettings.foodPenaltyPerUnit, medPen = FCSSettings.medicinePenaltyPerUnit;
        try
        {
            FCSSettings.ResetToDefaults();
            TestAssert.IsFalse(FCSSettings.PrintDebug, "printDebug");
            TestAssert.AreEqual(3, FCSSettings.specialistBaseMax, "specialistBaseMax");
            TestAssert.AreEqual(3, FCSSettings.specialistPerLevels, "specialistPerLevels");
            TestAssert.AreEqual(5, FCSSettings.residentsPerWorker, "residentsPerWorker");
            TestAssert.AreEqual(1.0, FCSSettings.scalingFactor, 0.00001, "scalingFactor");
            TestAssert.AreEqual(0.02, FCSSettings.healRatePerLevelGovernor, 0.00001, "healRatePerLevelGovernor");
            TestAssert.AreEqual(500.0, FCSSettings.xpPerDay, 0.00001, "xpPerDay");
            TestAssert.AreEqual(1.0, FCSSettings.skillTaperFactorBand1, 0.00001, "skillTaperFactorBand1");
            TestAssert.AreEqual(1.0, FCSSettings.skillTaperFactorBand2, 0.00001, "skillTaperFactorBand2");
            TestAssert.AreEqual(0.5, FCSSettings.skillTaperFactorBand3, 0.00001, "skillTaperFactorBand3");
            TestAssert.AreEqual(0.3, FCSSettings.foodPerSpecialist, 0.00001, "foodPerSpecialist");
            TestAssert.AreEqual(0.15, FCSSettings.foodPerResident, 0.00001, "foodPerResident");
            TestAssert.AreEqual(0.1, FCSSettings.medicinePerSpecialist, 0.00001, "medicinePerSpecialist");
            TestAssert.AreEqual(0.8, FCSSettings.foodPenaltyPerUnit, 0.00001, "foodPenaltyPerUnit");
            TestAssert.AreEqual(0.3, FCSSettings.medicinePenaltyPerUnit, 0.00001, "medicinePenaltyPerUnit");
        }
        finally
        {
            printDebug = savedPrintDebug;
            FCSSettings.specialistBaseMax = baseMax;
            FCSSettings.specialistPerLevels = perLevels;
            FCSSettings.residentsPerWorker = perWorker;
            FCSSettings.scalingFactor = scaling;
            FCSSettings.healRatePerLevelGovernor = heal;
            FCSSettings.xpPerDay = xp;
            FCSSettings.skillTaperFactorBand1 = band1;
            FCSSettings.skillTaperFactorBand2 = band2;
            FCSSettings.skillTaperFactorBand3 = band3;
            FCSSettings.foodPerSpecialist = foodSpec;
            FCSSettings.foodPerResident = foodRes;
            FCSSettings.medicinePerSpecialist = medSpec;
            FCSSettings.foodPenaltyPerUnit = foodPen;
            FCSSettings.medicinePenaltyPerUnit = medPen;
        }
    }

    /*-*-*- Roster entry save format: element names and order must stay readable by old saves -*-*-*/

    private static List<string> SavedElementNames(IExposable entry)
    {
        string xml = Scribe.saver.DebugOutputFor(entry);
        TestAssert.IsFalse(xml.NullOrEmpty(), "Scribe produced no output");
        XmlDocument doc = new();
        doc.LoadXml(xml);
        return doc.DocumentElement.ChildNodes.OfType<XmlElement>().Select(e => e.Name).ToList();
    }

    [EmpireTest("SG.Serialization")]
    public static void SpecialistSave_WritesPawnRoleTickInOrder()
    {
        SpecialistRoleDef role = TestAssert.NotNull(SpecialistRoleDefOf.Generalist, "Generalist role not loaded");
        SettlementSpecialist s = new() { role = role, assignedTick = 42 };
        TestAssert.AreEqual("pawn,role,assignedTick", string.Join(",", SavedElementNames(s)));
    }

    [EmpireTest("SG.Serialization")]
    public static void GovernorSave_WritesPawnFocusTickInOrder()
    {
        GovernorFocusDef focus = TestAssert.NotNull(GovernorFocusDefOf.Balanced, "Balanced focus not loaded");
        SettlementGovernor g = new() { focus = focus, assignedTick = 42 };
        TestAssert.AreEqual("pawn,focus,assignedTick", string.Join(",", SavedElementNames(g)));
    }

    // Loads an entry from XML in the format older versions saved, then stops the loader without
    // resolving references (the pawn is null in the XML anyway).
    private static T LoadEntry<T>(string innerXml) where T : RosterEntry
    {
        string path = Path.Combine(GenFilePaths.TempFolderPath, $"EmpireSpecTest_{typeof(T).Name}.xml");
        File.WriteAllText(path, $"<root><saveable>{innerXml}</saveable></root>");
        T loaded = null;
        try
        {
            Scribe.loader.InitLoading(path);
            Scribe_Deep.Look(ref loaded, "saveable");
        }
        finally
        {
            Scribe.ForceStop();
            File.Delete(path);
        }
        return loaded;
    }

    [EmpireTest("SG.Serialization")]
    public static void SpecialistLoad_ReadsOldFormat()
    {
        SpecialistRoleDef role = TestAssert.NotNull(SpecialistRoleDefOf.Generalist, "Generalist role not loaded");
        SettlementSpecialist s = LoadEntry<SettlementSpecialist>(
            $"<pawn>null</pawn><role>{role.defName}</role><assignedTick>4242</assignedTick>");
        TestAssert.NotNull(s, "the entry did not load");
        TestAssert.IsTrue(s.role == role, "role should load from <role>");
        TestAssert.AreEqual(4242, s.assignedTick, "assignedTick should load from <assignedTick>");
        TestAssert.IsNull(s.pawn);
    }

    [EmpireTest("SG.Serialization")]
    public static void GovernorLoad_ReadsOldFormat()
    {
        GovernorFocusDef focus = TestAssert.NotNull(GovernorFocusDefOf.Balanced, "Balanced focus not loaded");
        SettlementGovernor g = LoadEntry<SettlementGovernor>(
            $"<pawn>null</pawn><focus>{focus.defName}</focus><assignedTick>4242</assignedTick>");
        TestAssert.NotNull(g, "the entry did not load");
        TestAssert.IsTrue(g.focus == focus, "focus should load from <focus>");
        TestAssert.AreEqual(4242, g.assignedTick, "assignedTick should load from <assignedTick>");
    }

    /*-*-*- Behavior lookup through the shared extension base -*-*-*/

    public class SpyFocusBehavior : GovernorFocusBehavior { }

    [EmpireTest("SG.Behavior", GameState = TestGameState.Any)]
    public static void GovernorBehavior_ResolvesFromFocusExtension()
    {
        GovernorFocusBehaviorExtension ext = new() { behaviorClass = typeof(SpyFocusBehavior) };
        GovernorFocusDef focus = new()
        {
            defName = "TEST_BehaviorFocus",
            label = "test behavior focus",
            modExtensions = [ext]
        };
        SettlementGovernor g = new() { focus = focus };
        GovernorFocusBehavior b = g.Behavior;
        TestAssert.IsTrue(b is SpyFocusBehavior, "the focus extension's behavior class should be created");
        TestAssert.IsTrue(b.extension == ext, "the behavior should back-reference its extension");
        TestAssert.IsTrue(ReferenceEquals(b, g.Behavior), "the behavior should be cached");
    }

    [EmpireTest("SG.Behavior", GameState = TestGameState.Any)]
    public static void GovernorFocusExt_WrongClass_NamesTheExtension()
    {
        GovernorFocusBehaviorExtension ext = new() { behaviorClass = typeof(string) };
        TestAssert.IsTrue(ext.ConfigErrors().Any(e => e.Contains("GovernorFocusBehaviorExtension")
                && e.Contains("must extend GovernorFocusBehavior")),
            "the ConfigError should name the extension and the required base type");
    }

    [EmpireTest("SG.Behavior", GameState = TestGameState.Any)]
    public static void Behavior_NoExtension_IsNull()
    {
        SettlementSpecialist s = new() { role = SpecTestHelper.Role(SkillDefOf.Plants, 1f, null, 0f) };
        TestAssert.IsNull(s.Behavior, "a role without a behavior extension has no behavior");
    }
}
