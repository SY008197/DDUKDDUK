using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace DdukDduk.Shop.Editor
{
    /// <summary>Verifies the team's shared data contract and its integration with the existing shop.</summary>
    public static class ShopCommonApiVerification
    {
        private static int passed;

        [MenuItem("DDUKDDUK/Verify Common Data API")]
        public static void RunAll()
        {
            passed = 0;
            Run("the 17 shared API methods have the exact public signatures", ContractSignatures);
            Run("shared state is private and phase values retain save compatibility", PrivateState);
            Run("initial API reads match the existing shop", InitialReads);
            Run("signed wallet changes and spending notify only on mutation", WalletChanges);
            Run("insufficient funds, negative spending and wallet overflow are atomic", WalletFailures);
            Run("reputation changes preserve its nonnegative checked range", ReputationChanges);
            Run("phase changes process production, settlement and the following day", SequentialPhases);
            Run("invalid phase changes never skip or undo domain work", InvalidPhases);
            Run("recipe completion respects ordering and is idempotent", OrderedRecipeCompletion);
            Run("stand completion follows all four ten-pack steps", OrderedStandCompletion);
            Run("other shared research IDs are recorded without inventing shop unlocks", SharedResearchIds);
            Run("every string-based API rejects empty IDs without mutation", InvalidIds);
            Run("marketing is first recorded on use and activates the next day", MarketingActivation);
            Run("marketing IDs are independent and same-week repeats do not reschedule", MarketingIndependentIds);
            Run("day seven and day eight separate weekly marketing use", MarketingWeekBoundary);
            Run("marketing date overflow rejects use before assigning any records", MarketingDayOverflow);
            Run("affection changes remain per character and overflow is atomic", AffectionChanges);
            Run("all common collections survive a JSON roundtrip independently", CommonRoundtrip);
            Run("snapshot arrays and restored dictionaries do not share mutable state", CommonSnapshotIsolation);
            Run("version four migrates recipe and all stand completions", LegacyResearchMigration);
            Run("previous long wallets outside the int contract are rejected explicitly", LegacyWalletRange);
            Run("invalid common records are rejected without any partial mutation", InvalidCommonSnapshots);
            Run("pending cake research cannot be completed before the morning", PendingCakeCompletion);
            Run("pending stand research cannot be completed before the morning", PendingStandCompletion);
            Run("changed observers always see complete paid and morning cake state", AtomicCakeObservers);
            Run("changed observers always see complete paid and morning stand state", AtomicStandObservers);
            Debug.Log("SHOP_COMMON_API_VERIFICATION_PASSED: " + passed + " checks");
        }

        private static void Run(string name, Action test)
        {
            try { test(); passed++; }
            catch (Exception exception) { throw new Exception("Common API verification failed: " + name, exception); }
        }

        private static void ContractSignatures()
        {
            Signature("GetDay", typeof(int));
            Signature("GetPhase", typeof(GamePhase));
            Signature("ChangePhase", typeof(void), typeof(GamePhase));
            Signature("GetMoney", typeof(int));
            Signature("AddMoney", typeof(void), typeof(int));
            Signature("TrySpendMoney", typeof(bool), typeof(int));
            Signature("GetReputation", typeof(int));
            Signature("AddReputation", typeof(void), typeof(int));
            Signature("IsResearchCompleted", typeof(bool), typeof(string));
            Signature("CompleteResearch", typeof(void), typeof(string));
            Signature("IsMarketingUsedThisWeek", typeof(bool), typeof(string));
            Signature("IsMarketingFirstUsed", typeof(bool), typeof(string));
            Signature("UseMarketing", typeof(void), typeof(string));
            Signature("IsMarketingActive", typeof(bool), typeof(string));
            Signature("GetAffection", typeof(int), typeof(CharacterId));
            Signature("AddAffection", typeof(void), typeof(CharacterId), typeof(int));
            Signature("GetCurrentRoute", typeof(Route));
        }

        private static void Signature(string name, Type returnType, params Type[] argumentTypes)
        {
            MethodInfo method = typeof(ShopModel).GetMethod(name, BindingFlags.Instance | BindingFlags.Public,
                null, argumentTypes, null);
            True(method != null); Equal(returnType, method.ReturnType);
            False(method.IsStatic); False(method.IsGenericMethod);
        }

        private static void PrivateState()
        {
            string[] names = { "day", "phase", "money", "reputation", "completedResearchIds", "usedThisWeek",
                "firstUsed", "applyDay", "affection", "completedEventIds", "currentRoute" };
            foreach (string name in names)
            {
                FieldInfo field = typeof(ShopModel).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
                True(field != null); True(field.IsPrivate);
            }
            foreach (string oldProperty in new[] { "Day", "Money", "Reputation", "Phase" })
                True(typeof(ShopModel).GetProperty(oldProperty, BindingFlags.Instance | BindingFlags.Public) == null);
            Equal(0, (int)GamePhase.Arrangement); Equal(1, (int)GamePhase.ReadyToSell); Equal(2, (int)GamePhase.Closed);
            Equal(0, Enum.GetNames(typeof(CharacterId)).Length); Equal(0, Enum.GetNames(typeof(Route)).Length);
        }

        private static void InitialReads()
        {
            ShopModel model = new ShopModel();
            Equal(1, model.GetDay()); Equal(GamePhase.Arrangement, model.GetPhase());
            Equal(100000, model.GetMoney()); Equal(0, model.GetReputation()); Equal((Route)0, model.GetCurrentRoute());
            False(model.IsResearchCompleted("mugwort")); False(model.IsResearchCompleted("shared_test_research"));
            False(model.IsMarketingUsedThisWeek("marketing_a")); False(model.IsMarketingFirstUsed("marketing_a"));
            False(model.IsMarketingActive("marketing_a")); Equal(0, model.GetAffection((CharacterId)0));
        }

        private static void WalletChanges()
        {
            ShopModel model = new ShopModel(); int changes = 0; model.Changed += () => changes++;
            model.AddMoney(500); Equal(100500, model.GetMoney()); Equal(1, changes);
            model.AddMoney(-500); Equal(100000, model.GetMoney()); Equal(2, changes);
            model.AddMoney(0); True(model.TrySpendMoney(0)); Equal(2, changes);
            True(model.TrySpendMoney(100000)); Equal(0, model.GetMoney()); Equal(3, changes);
            model.AddMoney(-1); Equal(-1, model.GetMoney()); Equal(4, changes);
            False(model.TrySpendMoney(1)); Equal(-1, model.GetMoney()); Equal(4, changes);
        }

        private static void WalletFailures()
        {
            ShopModel model = new ShopModel(); int changes = 0; model.Changed += () => changes++;
            False(model.TrySpendMoney(100001)); Throws<ArgumentOutOfRangeException>(() => model.TrySpendMoney(-1));
            Equal(100000, model.GetMoney()); Equal(0, changes);
            model.AddMoney(int.MaxValue - model.GetMoney()); Equal(int.MaxValue, model.GetMoney());
            string before = Json(model); Throws<OverflowException>(() => model.AddMoney(1)); Equal(before, Json(model)); Equal(1, changes);
            model = new ShopModel(); model.AddMoney(-100000); model.AddMoney(int.MinValue); before = Json(model);
            Throws<OverflowException>(() => model.AddMoney(-1)); Equal(before, Json(model));
        }

        private static void ReputationChanges()
        {
            ShopModel model = new ShopModel(); int changes = 0; model.Changed += () => changes++;
            model.AddReputation(15); Equal(15, model.GetReputation()); Equal(1, changes);
            model.AddReputation(-15); Equal(0, model.GetReputation()); Equal(2, changes);
            model.AddReputation(0); Equal(2, changes);
            Throws<ArgumentOutOfRangeException>(() => model.AddReputation(-1)); Equal(0, model.GetReputation()); Equal(2, changes);
            model.AddReputation(int.MaxValue); string before = Json(model);
            Throws<OverflowException>(() => model.AddReputation(1)); Equal(before, Json(model)); Equal(3, changes);
        }

        private static void SequentialPhases()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 30); model.AddReputation(35);
            model.ChangePhase(GamePhase.ReadyToSell); Equal(30, model.TotalProduced); Equal(30000L, model.CommittedProductionCost);
            Equal(100000, model.GetMoney()); Equal(GamePhase.ReadyToSell, model.GetPhase());
            model.ChangePhase(GamePhase.Closed); Equal(GamePhase.Closed, model.GetPhase()); True(model.IsSaleSettled);
            Equal(145000, model.GetMoney()); Equal(30, model.LastSale.TotalSold);
            model.ChangePhase(GamePhase.Arrangement); Equal(2, model.GetDay()); Equal(0, model.TotalSelected);
            Equal(0, model.TotalProduced); Equal(145000, model.GetMoney()); True(model.LastSale == null);
        }

        private static void InvalidPhases()
        {
            ShopModel model = new ShopModel(); int changes = 0; model.Changed += () => changes++;
            model.ChangePhase(GamePhase.Arrangement); Equal(0, changes);
            string before = Json(model);
            Throws<ArgumentOutOfRangeException>(() => model.ChangePhase((GamePhase)99));
            Throws<InvalidOperationException>(() => model.ChangePhase(GamePhase.Closed)); Equal(before, Json(model));
            model.ChangePhase(GamePhase.ReadyToSell); before = Json(model);
            Throws<InvalidOperationException>(() => model.ChangePhase(GamePhase.Arrangement)); Equal(before, Json(model));
            model.ChangePhase(GamePhase.ReadyToSell); Equal(1, changes);
            model.ChangePhase(GamePhase.Closed); before = Json(model);
            Throws<InvalidOperationException>(() => model.ChangePhase(GamePhase.ReadyToSell)); Equal(before, Json(model));
            model.ChangePhase(GamePhase.Closed); Equal(2, changes);
            model = new ShopModel(); model.OpenDayWithCustomer(1); before = Json(model);
            Throws<InvalidOperationException>(() => model.ChangePhase(GamePhase.Arrangement)); Equal(before, Json(model));
        }

        private static void OrderedRecipeCompletion()
        {
            ShopModel model = new ShopModel(); int changes = 0; model.Changed += () => changes++;
            string before = Json(model); Throws<InvalidOperationException>(() => model.CompleteResearch("honey")); Equal(before, Json(model));
            string[] recipes = { "mugwort", "honey", "bean", "royal" };
            for (int i = 0; i < recipes.Length; i++)
            {
                model.CompleteResearch(recipes[i]); True(model.IsResearchCompleted(recipes[i])); True(model.UnlockedProducts[5 + i]);
                Equal(100000, model.GetMoney()); Equal(i + 1, changes); before = Json(model);
                model.CompleteResearch(recipes[i]); Equal(before, Json(model)); Equal(i + 1, changes);
            }
            RestoreJson(model);
        }

        private static void OrderedStandCompletion()
        {
            ShopModel model = new ShopModel(); string before = Json(model);
            Throws<InvalidOperationException>(() => model.CompleteResearch("stand_50")); Equal(before, Json(model));
            int changes = 0; model.Changed += () => changes++;
            for (int capacity = 40; capacity <= 70; capacity += 10)
            {
                string id = "stand_" + capacity; model.CompleteResearch(id); Equal(capacity, model.Capacity);
                True(model.IsResearchCompleted(id)); Equal(100000, model.GetMoney()); before = Json(model);
                model.CompleteResearch(id); Equal(before, Json(model)); Equal((capacity - 30) / 10, changes);
            }
            RestoreJson(model);
        }

        private static void SharedResearchIds()
        {
            ShopModel model = new ShopModel(); int changes = 0; model.Changed += () => changes++;
            model.CompleteResearch("shared_test_research"); True(model.IsResearchCompleted("shared_test_research"));
            False(model.IsResearchCompleted("SHARED_TEST_RESEARCH")); Equal(30, model.Capacity);
            for (int i = 5; i < ShopModel.Catalog.Count; i++) False(model.UnlockedProducts[i]);
            model.CompleteResearch("shared_test_research"); Equal(1, changes); Equal(100000, model.GetMoney());
            ShopModel restored = RestoreJson(model); True(restored.IsResearchCompleted("shared_test_research"));
        }

        private static void InvalidIds()
        {
            ShopModel model = new ShopModel(); int changes = 0; model.Changed += () => changes++;
            string before = Json(model);
            foreach (string id in new string[] { null, string.Empty, " \t\r\n" })
            {
                Throws<ArgumentException>(() => model.IsResearchCompleted(id)); Throws<ArgumentException>(() => model.CompleteResearch(id));
                Throws<ArgumentException>(() => model.IsMarketingUsedThisWeek(id)); Throws<ArgumentException>(() => model.IsMarketingFirstUsed(id));
                Throws<ArgumentException>(() => model.UseMarketing(id)); Throws<ArgumentException>(() => model.IsMarketingActive(id));
            }
            Equal(before, Json(model)); Equal(0, changes);
        }

        private static void MarketingActivation()
        {
            ShopModel model = new ShopModel(); model.UseMarketing("marketing_a");
            True(model.IsMarketingUsedThisWeek("marketing_a")); True(model.IsMarketingFirstUsed("marketing_a"));
            False(model.IsMarketingActive("marketing_a")); Equal(100000, model.GetMoney()); Equal(0, model.GetReputation());
            AdvanceDay(model); Equal(2, model.GetDay()); True(model.IsMarketingActive("marketing_a"));
            AdvanceDay(model); False(model.IsMarketingActive("marketing_a")); True(model.IsMarketingFirstUsed("marketing_a"));
            True(model.IsMarketingUsedThisWeek("marketing_a"));
        }

        private static void MarketingIndependentIds()
        {
            ShopModel model = new ShopModel(); int changes = 0; model.Changed += () => changes++;
            model.UseMarketing("marketing_a"); model.UseMarketing("marketing_b"); Equal(2, changes);
            False(model.IsMarketingFirstUsed("marketing_c")); False(model.IsMarketingUsedThisWeek("marketing_c"));
            string before = Json(model); model.UseMarketing("marketing_a"); Equal(before, Json(model)); Equal(2, changes);
            AdvanceDay(model); True(model.IsMarketingActive("marketing_a")); True(model.IsMarketingActive("marketing_b"));
            AdvanceDay(model); before = Json(model); int beforeChanges = changes;
            model.UseMarketing("marketing_a"); Equal(before, Json(model)); Equal(beforeChanges, changes);
            False(model.IsMarketingActive("marketing_a"));
        }

        private static void MarketingWeekBoundary()
        {
            ShopModel model = new ShopModel(); model.UseMarketing("marketing_a");
            while (model.GetDay() < 7) AdvanceDay(model);
            True(model.IsMarketingUsedThisWeek("marketing_a")); False(model.IsMarketingActive("marketing_a"));
            string before = Json(model); model.UseMarketing("marketing_a"); Equal(before, Json(model));
            AdvanceDay(model); Equal(8, model.GetDay()); False(model.IsMarketingUsedThisWeek("marketing_a"));
            True(model.IsMarketingFirstUsed("marketing_a")); model.UseMarketing("marketing_a");
            True(model.IsMarketingUsedThisWeek("marketing_a")); False(model.IsMarketingActive("marketing_a"));
            AdvanceDay(model); Equal(9, model.GetDay()); True(model.IsMarketingActive("marketing_a"));
            ShopModel restored = RestoreJson(model); True(restored.IsMarketingActive("marketing_a")); True(restored.IsMarketingFirstUsed("marketing_a"));
        }

        private static void MarketingDayOverflow()
        {
            ShopModel model = new ShopModel(); ShopSnapshot lastDay = model.CreateSnapshot(); lastDay.Day = int.MaxValue;
            model.RestoreSnapshot(lastDay); string before = Json(model); int changes = 0; model.Changed += () => changes++;
            Throws<OverflowException>(() => model.UseMarketing("marketing_a")); Equal(before, Json(model)); Equal(0, changes);
            False(model.IsMarketingFirstUsed("marketing_a")); False(model.IsMarketingUsedThisWeek("marketing_a"));
        }

        private static void AffectionChanges()
        {
            ShopModel model = new ShopModel(); CharacterId first = (CharacterId)0, second = (CharacterId)1;
            int changes = 0; model.Changed += () => changes++;
            model.AddAffection(first, 3); model.AddAffection(second, 7); model.AddAffection(first, -4);
            Equal(-1, model.GetAffection(first)); Equal(7, model.GetAffection(second)); Equal(3, changes);
            model.AddAffection(first, 0); Equal(3, changes); string before = Json(model);
            Throws<ArgumentOutOfRangeException>(() => model.GetAffection((CharacterId)(-1)));
            Throws<ArgumentOutOfRangeException>(() => model.AddAffection((CharacterId)(-1), 10)); Equal(before, Json(model));
            model.AddAffection(second, int.MaxValue - 7); before = Json(model);
            Throws<OverflowException>(() => model.AddAffection(second, 1)); Equal(before, Json(model));
            ShopModel restored = RestoreJson(model); Equal(-1, restored.GetAffection(first)); Equal(int.MaxValue, restored.GetAffection(second));
        }

        private static void CommonRoundtrip()
        {
            ShopModel model = SharedStateModel(); ShopSnapshot snapshot = model.CreateSnapshot();
            snapshot.CommonData.completedEventIds = new[] { "shared_test_event" }; snapshot.CommonData.currentRoute = (Route)1;
            model.RestoreSnapshot(snapshot); ShopModel restored = RestoreJson(model);
            Equal(5, restored.CreateSnapshot().Version); Equal(Json(model), Json(restored));
            True(restored.IsResearchCompleted("mugwort")); True(restored.IsResearchCompleted("shared_test_research"));
            True(restored.IsMarketingUsedThisWeek("marketing_a")); True(restored.IsMarketingFirstUsed("marketing_a"));
            False(restored.IsMarketingActive("marketing_a")); Equal(4, restored.GetAffection((CharacterId)0));
            Equal(-2, restored.GetAffection((CharacterId)1)); Equal((Route)1, restored.GetCurrentRoute());
            Equal("shared_test_event", restored.CreateSnapshot().CommonData.completedEventIds[0]);
            AdvanceDay(restored); True(restored.IsMarketingActive("marketing_a")); Equal((Route)1, restored.GetCurrentRoute());
        }

        private static void CommonSnapshotIsolation()
        {
            ShopModel model = SharedStateModel(); string before = Json(model); ShopSnapshot snapshot = model.CreateSnapshot();
            ShopModel restored = new ShopModel(); restored.RestoreSnapshot(snapshot); string restoredBefore = Json(restored);
            snapshot.CommonData.completedResearchIds[0] = "mutated"; snapshot.CommonData.firstUsed[0] = "mutated";
            snapshot.CommonData.usedThisWeek[0].marketingId = "mutated"; snapshot.CommonData.applyDay[0].day = 99;
            snapshot.CommonData.affection[0].amount = 99;
            Equal(before, Json(model)); Equal(restoredBefore, Json(restored));
            model.AddAffection((CharacterId)0, 1); Equal(4, restored.GetAffection((CharacterId)0));
        }

        private static void LegacyResearchMigration()
        {
            ShopSnapshot legacy = new ShopModel().CreateSnapshot(); legacy.Version = 4; legacy.CommonData = null;
            legacy.Day = 7; legacy.Money = 456789; legacy.Reputation = 35; legacy.Capacity = 70;
            for (int i = 5; i < ShopModel.Catalog.Count; i++) legacy.UnlockedProducts[i] = true;
            ShopModel migrated = new ShopModel(); migrated.RestoreSnapshot(JsonUtility.FromJson<ShopSnapshot>(JsonUtility.ToJson(legacy)));
            Equal(5, migrated.CreateSnapshot().Version); Equal(7, migrated.GetDay()); Equal(456789, migrated.GetMoney()); Equal(70, migrated.Capacity);
            foreach (string id in new[] { "mugwort", "honey", "bean", "royal", "stand_40", "stand_50", "stand_60", "stand_70" })
                True(migrated.IsResearchCompleted(id));
            False(migrated.IsMarketingFirstUsed("marketing_a")); Equal(0, migrated.GetAffection((CharacterId)0));
            RestoreJson(migrated);
        }

        private static void LegacyWalletRange()
        {
            ShopModel model = SharedStateModel(); string before = Json(model); int changes = 0; model.Changed += () => changes++;
            foreach (long amount in new[] { (long)int.MaxValue + 1, (long)int.MinValue - 1 })
            {
                ShopSnapshot legacy = new ShopModel().CreateSnapshot(); legacy.Version = 4; legacy.CommonData = null; legacy.Money = amount;
                string error; False(model.TryRestoreSnapshot(legacy, out error)); True(!string.IsNullOrEmpty(error));
                Equal(before, Json(model)); Equal(0, changes);
            }
        }

        private static void InvalidCommonSnapshots()
        {
            Action<ShopSnapshot>[] corrupt =
            {
                s => s.CommonData = null,
                s => s.CommonData.completedResearchIds = new[] { "mugwort", "mugwort", "shared_test_research" },
                s => s.CommonData.completedResearchIds = new[] { "mugwort", " " },
                s => s.CommonData.completedResearchIds = new[] { "shared_test_research" },
                s => s.CommonData.completedResearchIds = new[] { "mugwort", "honey", "shared_test_research" },
                s => s.CommonData.usedThisWeek = new[] { new MarketingWeekSnapshot { marketingId = "marketing_a", week = 0 }, new MarketingWeekSnapshot { marketingId = "marketing_a", week = 0 } },
                s => s.CommonData.firstUsed = new string[0],
                s => s.CommonData.applyDay = new MarketingApplyDaySnapshot[0],
                s => s.CommonData.usedThisWeek[0].week = -1,
                s => s.CommonData.usedThisWeek[0].week = 1,
                s => s.CommonData.applyDay[0].day = 1,
                s => s.CommonData.applyDay[0].day = 99,
                s => s.CommonData.affection = new[] { new AffectionSnapshot { id = (CharacterId)0, amount = 1 }, new AffectionSnapshot { id = (CharacterId)0, amount = 2 } },
                s => s.CommonData.affection[0].id = (CharacterId)(-1),
                s => s.CommonData.completedEventIds = new[] { " " },
                s => s.CommonData.completedEventIds = new[] { "event_a", "event_a" },
                s => s.CommonData.currentRoute = (Route)(-1)
            };
            ShopModel model = SharedStateModel(); string before = Json(model); int changes = 0; model.Changed += () => changes++;
            foreach (Action<ShopSnapshot> mutate in corrupt)
            {
                ShopSnapshot invalid = JsonUtility.FromJson<ShopSnapshot>(before); invalid.Money += 123; mutate(invalid);
                string error; False(model.TryRestoreSnapshot(invalid, out error)); True(!string.IsNullOrEmpty(error));
                Equal(before, Json(model)); Equal(0, changes);
            }
        }

        private static void PendingCakeCompletion()
        {
            ShopModel model = new ShopModel(); OpenEvening(model, ResearchKind.Cake); string error;
            True(model.TryScheduleCakeResearch(5, out error)); string before = Json(model);
            Throws<InvalidOperationException>(() => model.CompleteResearch("mugwort")); Equal(before, Json(model));
            False(model.IsResearchCompleted("mugwort")); False(model.UnlockedProducts[5]);
            model = RestoreJson(model); model.NextDay(); True(model.IsResearchCompleted("mugwort")); True(model.UnlockedProducts[5]);
            Equal(40000, model.GetMoney()); Equal(EveningStep.MorningNotice, model.Evening);
        }

        private static void PendingStandCompletion()
        {
            ShopModel model = new ShopModel(); model.AddMoney(300000); model.AddReputation(15); OpenEvening(model, ResearchKind.Stand);
            string error; True(model.TryScheduleStandResearch(out error)); string before = Json(model);
            Throws<InvalidOperationException>(() => model.CompleteResearch("stand_40")); Equal(before, Json(model));
            False(model.IsResearchCompleted("stand_40")); Equal(30, model.Capacity);
            model = RestoreJson(model); model.NextDay(); True(model.IsResearchCompleted("stand_40")); Equal(40, model.Capacity);
            Equal(250000, model.GetMoney()); Equal(EveningStep.MorningNotice, model.Evening);
        }

        private static void AtomicCakeObservers()
        {
            ShopModel model = new ShopModel(); OpenEvening(model, ResearchKind.Cake); int changes = 0;
            model.Changed += () =>
            {
                changes++; ShopModel observerCopy = RestoreJson(model); Equal(40000, observerCopy.GetMoney());
                if (observerCopy.PendingResearch != null)
                { Equal(1, observerCopy.GetDay()); False(observerCopy.IsResearchCompleted("mugwort")); False(observerCopy.UnlockedProducts[5]); }
                else
                { Equal(2, observerCopy.GetDay()); True(observerCopy.IsResearchCompleted("mugwort")); True(observerCopy.UnlockedProducts[5]); Equal(EveningStep.MorningNotice, observerCopy.Evening); }
            };
            string error; True(model.TryScheduleCakeResearch(5, out error)); Equal(1, changes);
            model.NextDay(); Equal(2, changes);
        }

        private static void AtomicStandObservers()
        {
            ShopModel model = new ShopModel(); model.AddMoney(300000); model.AddReputation(15); OpenEvening(model, ResearchKind.Stand); int changes = 0;
            model.Changed += () =>
            {
                changes++; ShopModel observerCopy = RestoreJson(model); Equal(250000, observerCopy.GetMoney());
                if (observerCopy.PendingResearch != null)
                { Equal(1, observerCopy.GetDay()); Equal(30, observerCopy.Capacity); False(observerCopy.IsResearchCompleted("stand_40")); }
                else
                { Equal(2, observerCopy.GetDay()); Equal(40, observerCopy.Capacity); True(observerCopy.IsResearchCompleted("stand_40")); Equal(EveningStep.MorningNotice, observerCopy.Evening); }
            };
            string error; True(model.TryScheduleStandResearch(out error)); Equal(1, changes);
            model.NextDay(); Equal(2, changes);
        }

        private static ShopModel SharedStateModel()
        {
            ShopModel model = new ShopModel(); model.CompleteResearch("mugwort"); model.CompleteResearch("shared_test_research");
            model.UseMarketing("marketing_a"); model.AddAffection((CharacterId)0, 4); model.AddAffection((CharacterId)1, -2);
            return model;
        }

        private static void AdvanceDay(ShopModel model)
        {
            model.ChangePhase(GamePhase.ReadyToSell); model.ChangePhase(GamePhase.Closed); model.ChangePhase(GamePhase.Arrangement);
        }

        private static void OpenEvening(ShopModel model, ResearchKind kind)
        {
            model.ConfirmProduction(); model.Sell(ShopModel.GetSalesBand(model.GetReputation()).Minimum);
            model.BeginEvening(); model.ChooseResearch(true); model.ChooseResearchKind(kind);
        }

        private static ShopModel RestoreJson(ShopModel model)
        {
            ShopModel restored = new ShopModel(); restored.RestoreSnapshot(JsonUtility.FromJson<ShopSnapshot>(Json(model))); return restored;
        }

        private static string Json(ShopModel model) { return JsonUtility.ToJson(model.CreateSnapshot()); }
        private static void Set(ShopModel model, int index, int count)
        { string error; if (!model.TrySetCount(index, count, out error)) throw new Exception(error); }
        private static void Equal<T>(T expected, T actual)
        { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual); }
        private static void True(bool value) { if (!value) throw new Exception("Expected true."); }
        private static void False(bool value) { if (value) throw new Exception("Expected false."); }
        private static void Throws<T>(Action action) where T : Exception
        { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    }
}
