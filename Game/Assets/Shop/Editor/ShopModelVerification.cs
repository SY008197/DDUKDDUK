using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DdukDduk.Shop.Editor
{
    /// <summary>Run with Unity -batchmode -executeMethod DdukDduk.Shop.Editor.ShopModelVerification.RunAll.</summary>
    public static class ShopModelVerification
    {
        private static int passed;

        [MenuItem("DDUKDDUK/Verify Shop Model")]
        public static void RunAll()
        {
            passed = 0;
            Run("catalog and initial state", InitialState);
            Run("one product can fill total capacity", SingleProductCapacity);
            Run("mixed counts share capacity", MixedCapacity);
            Run("negative and unknown counts are rejected", InvalidSelection);
            Run("locked products require research", ResearchUnlock);
            Run("capacity upgrades charge now and apply on the following morning", CapacityUpgrade);
            Run("automatic distribution respects research and capacity", AutoDistribution);
            Run("zero production and sale are valid", ZeroSale);
            Run("production commit locks editing and is not a money debit", ProductionCommit);
            Run("reputation 14 uses the first sales band", () => ReputationBoundary(14, 0.80, 24));
            Run("reputation 15 uses the second sales band", () => ReputationBoundary(15, 0.85, 25));
            Run("reputation 34 uses the second sales band", () => ReputationBoundary(34, 0.95, 28));
            Run("reputation 35 sells everything", () => ReputationBoundary(35, 1.0, 30));
            Run("invalid sale rates leave a ready shop intact", InvalidRates);
            Run("rounding is applied to the total and ties use catalog order", TotalRounding);
            Run("largest remainders preserve proportional sales", LargestRemainders);
            Run("100 percent conserves each product", FullSaleConservation);
            Run("production and sale cannot be processed twice", DuplicateProcessing);
            Run("money uses revenue minus committed production cost", MoneyAccounting);
            Run("next day resets counts and preserves progression", NextDayReset);
            Run("domain events are emitted once per completed operation", Events);
            Run("snapshots roundtrip in all three phases", SnapshotRoundtrip);
            Run("Unity inline sale placeholders cannot fabricate a completed sale", SnapshotSalePresence);
            Run("snapshots and restored models do not share mutable arrays", SnapshotIsolation);
            Run("invalid snapshots are rejected atomically", SnapshotValidation);
            Run("rice cakes use the assigned taste categories", CustomerTasteCategories);
            Run("all three customer answers reward the matching recommendation", CustomerAnswers);
            Run("customer selection respects taste, research and the confirmed display", CustomerSelectionFilters);
            Run("incorrect recommendations preserve reputation and resolve only once", CustomerIncorrectRecommendation);
            Run("today's sales keep the band from before the customer reward", CustomerSalesBandAtOpening);
            Run("customer opening works from either production phase and blocks early next day", CustomerOpeningAndProgression);
            Run("unusual generated dialogue is displayed unchanged", CustomerDialogue);
            Run("customer visits roundtrip in every step and cannot award twice after restore", CustomerSnapshotRoundtrip);
            Run("version one saves migrate without a fabricated customer", CustomerSnapshotMigration);
            Run("invalid customer snapshots are rejected atomically", CustomerSnapshotValidation);
            Run("empty displays reject recommendations and let the customer leave without a reward", CustomerDeclineEmptyDisplay);
            Run("unclassified-only displays let the customer leave", CustomerDeclineUnclassifiedDisplay);
            Run("recommendations use confirmed production rather than remaining stock", CustomerConfirmedDisplay);
            Run("declined visits roundtrip and cannot fabricate recommendation rewards", CustomerDeclineSnapshotValidation);
            Run("evening navigation and back never change money or progression", EveningNavigation);
            Run("evening begins only after a completed customer visit", EveningStartGuards);
            Run("rest requires confirmation and begins the next day without a notice", EveningRest);
            Run("cake research checks funds before prerequisites and is atomic on rejection", ResearchFailureGuards);
            Run("cake recipes research sequentially and become available only next day", SequentialCakeResearch);
            Run("only one evening action can be paid and queued", SingleEveningAction);
            Run("morning notices block every production entry point until acknowledged", MorningProductionGuards);
            Run("stand research has reputation gates and all four paid ten-pack steps", StandResearchProgression);
            Run("stand research failure leaves money and capacity intact", StandResearchFailure);
            Run("queued research survives a JSON roundtrip and applies exactly once", PendingResearchRoundtrip);
            Run("all evening and morning screens survive JSON roundtrips", EveningSnapshotRoundtrip);
            Run("invalid research snapshots are rejected without modifying live state", EveningSnapshotValidation);
            Run("old version two saves retain their progression without evening data", EveningSnapshotMigration);
            Run("explicit editor unlocks remain available while gameplay upgrades cannot bypass payment", ResearchExternalCompatibility);
            Run("customer sales post money only as the result screen begins", CustomerDeferredSettlement);
            Run("failed recommendations defer negative net profit until the result", CustomerNegativeSettlement);
            Run("empty displays settle zero profit exactly once", CustomerZeroSettlement);
            Run("sale completed events fire at actual settlement rather than opening", CustomerSettlementEvents);
            Run("unsettled customer visits survive saves without missing or duplicating payment", CustomerSettlementRoundtrip);
            Run("version three customer visits keep their previously paid money after migration", CustomerSettlementMigration);
            Run("invalid settlement markers are rejected atomically", CustomerSettlementValidation);
            Debug.Log("SHOP_MODEL_VERIFICATION_PASSED: " + passed + " checks");
        }

        private static void Run(string name, Action test)
        {
            try { test(); passed++; }
            catch (Exception exception) { throw new Exception("Shop verification failed: " + name, exception); }
        }

        private static void InitialState()
        {
            ShopModel model = new ShopModel();
            Equal(9, ShopModel.Catalog.Count); Equal(1, model.GetDay()); Equal(100000L, model.GetMoney());
            Equal(30, model.Capacity); Equal(0, model.GetReputation()); Equal(GamePhase.Arrangement, model.GetPhase());
            Equal(0, model.TotalSelected); Equal(0, model.TotalProduced);
            string[] ids = { "garaetteok", "jeolpyeon", "baekseolgi", "injeolmi", "baram", "mugwort", "honey", "bean", "royal" };
            int[] prices = { 2500, 2500, 3000, 3500, 3500, 4500, 6000, 8000, 12000 };
            int[] costs = { 1000, 1000, 1200, 1400, 1400, 1800, 2400, 3200, 4800 };
            for (int i = 0; i < ids.Length; i++)
            {
                Equal(ids[i], model.Products[i].Id); Equal(prices[i], model.Products[i].SellPrice);
                Equal(costs[i], model.Products[i].Cost); Equal(i < 5, model.UnlockedProducts[i]);
                True(!string.IsNullOrEmpty(model.Products[i].Name)); True(!string.IsNullOrEmpty(model.Products[i].Description));
            }
        }

        private static void SingleProductCapacity()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 30); Equal(30, model.TotalSelected);
            string error; False(model.TrySetCount(0, 31, out error)); True(!string.IsNullOrEmpty(error));
            Equal(30, model.SelectedCounts[0]); Set(model, 0, 0); Equal(0, model.TotalSelected);
        }

        private static void MixedCapacity()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 12); Set(model, 1, 8); Set(model, 2, 10);
            Equal(30, model.TotalSelected); string error; False(model.TrySetCount(3, 1, out error));
            Set(model, 0, 11); Set(model, 3, 1); Equal(30, model.TotalSelected);
        }

        private static void InvalidSelection()
        {
            ShopModel model = new ShopModel(); string error;
            False(model.TrySetCount(0, -1, out error)); False(model.TrySetCount(-1, 1, out error));
            False(model.TrySetCount(9, 1, out error)); False(model.TrySetCount("missing", 1, out error));
            False(model.TryAdjustQuantity("garaetteok", -1, out error)); Equal(0, model.TotalSelected);
        }

        private static void ResearchUnlock()
        {
            ShopModel model = new ShopModel(); string error;
            for (int i = 5; i < 9; i++) { False(model.TrySetCount(i, 1, out error)); Set(model, i, 0); }
            model.UnlockProduct("mugwort"); Set(model, 5, 30); Equal(30, model.TotalSelected);
            model.UnlockProduct(5); Equal(100000L, model.GetMoney());
        }

        private static void CapacityUpgrade()
        {
            ShopModel model = FundedModel(400000, 15);
            OpenResearchEvening(model, ResearchKind.Stand); long before = model.GetMoney();
            model.UpgradeCapacity(); Equal(30, model.Capacity); Equal(before - 150000, model.GetMoney());
            model.NextDay(); Equal(40, model.Capacity); Equal(EveningStep.MorningNotice, model.Evening); model.AcknowledgeMorning();
            OpenResearchEvening(model, ResearchKind.Stand); model.UpgradeCapacity(); Equal(40, model.Capacity);
            model.NextDay(); model.AcknowledgeMorning(); Equal(50, model.Capacity);
            Set(model, 0, 50); Equal(50, model.TotalSelected);
        }

        private static void AutoDistribution()
        {
            ShopModel model = new ShopModel(); model.DistributeToCapacity(); Counts(model.SelectedCounts, 6, 6, 6, 6, 6, 0, 0, 0, 0);
            model.ClearSelection(); Equal(0, model.TotalSelected); model.UnlockProduct(5); model.AutoDistribute();
            Counts(model.SelectedCounts, 5, 5, 5, 5, 5, 5, 0, 0, 0);
            model.CompleteResearch("stand_40");
            model.AutoDistribute(); Counts(model.SelectedCounts, 7, 7, 7, 7, 6, 6, 0, 0, 0);
        }

        private static void ZeroSale()
        {
            ShopModel model = new ShopModel(); ProductionReceipt production = model.ConfirmProduction();
            Equal(0, production.TotalProduced); Equal(0L, production.ProductionCost);
            SaleResult sale = model.Sell(0.75); Equal(0, sale.TotalSold); Equal(0L, sale.Revenue);
            Equal(0L, sale.NetProfit); Equal(100000L, model.GetMoney()); Equal(GamePhase.Closed, model.GetPhase());
            model.NextDay(); Equal(2, model.GetDay());
        }

        private static void ProductionCommit()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 30); ProductionReceipt receipt = model.ConfirmProduction();
            Equal(30, receipt.TotalProduced); Equal(30000L, receipt.ProductionCost); Equal(100000L, model.GetMoney());
            Equal(GamePhase.ReadyToSell, model.GetPhase()); string error; False(model.TrySetCount(0, 0, out error));
            Throws<InvalidOperationException>(() => model.ClearSelection()); Throws<InvalidOperationException>(() => model.AutoDistribute());
            Equal(30, model.TotalProduced); Equal(30, model.TotalSelected);
        }

        private static void ReputationBoundary(int reputation, double rate, int expectedSold)
        {
            ShopModel model = new ShopModel(); model.AddReputation((reputation) - model.GetReputation()); Set(model, 0, 30); model.ConfirmProduction();
            SaleResult sale = model.Sell(rate); Equal(expectedSold, sale.TotalSold); Equal(reputation, sale.Reputation);
            SalesRateBand band = ShopModel.GetSalesBand(reputation);
            if (reputation < 15) { Equal(0.75, band.Minimum); Equal(0.80, band.Maximum); }
            else if (reputation < 35) { Equal(0.85, band.Minimum); Equal(0.95, band.Maximum); }
            else { Equal(1.0, band.Minimum); Equal(1.0, band.Maximum); }
        }

        private static void InvalidRates()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 30); model.ConfirmProduction();
            foreach (double rate in new[] { 0.74, 0.81, 1.0, double.NaN, double.PositiveInfinity })
                Throws<ArgumentOutOfRangeException>(() => model.Sell(rate));
            Equal(GamePhase.ReadyToSell, model.GetPhase()); Equal(100000L, model.GetMoney()); True(model.LastSale == null);
            model.AddReputation((15) - model.GetReputation()); Throws<ArgumentOutOfRangeException>(() => model.Sell(0.80));
            model.AddReputation((35) - model.GetReputation()); Throws<ArgumentOutOfRangeException>(() => model.Sell(0.95));
            Equal(30, model.Sell(1.0).TotalSold);
        }

        private static void TotalRounding()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 1); Set(model, 1, 1); Set(model, 2, 1); model.ConfirmProduction();
            SaleResult sale = model.Sell(0.75); Equal(2, sale.TotalSold); Counts(sale.SoldCounts, 1, 1, 0, 0, 0, 0, 0, 0, 0);
            model = new ShopModel(); model.AutoDistribute(); model.ConfirmProduction(); sale = model.Sell(0.75);
            Equal(22, sale.TotalSold); Counts(sale.SoldCounts, 5, 5, 4, 4, 4, 0, 0, 0, 0);
        }

        private static void LargestRemainders()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 2); Set(model, 1, 1); model.ConfirmProduction();
            SaleResult sale = model.Sell(0.80); Counts(sale.SoldCounts, 1, 1, 0, 0, 0, 0, 0, 0, 0);
            Equal(2, sale.TotalSold); Equal(1, sale.TotalUnsold);
        }

        private static void FullSaleConservation()
        {
            ShopModel model = new ShopModel(); for (int i = 5; i < 9; i++) model.UnlockProduct(i);
            model.AutoDistribute(); model.AddReputation((35) - model.GetReputation()); model.ConfirmProduction(); SaleResult sale = model.Sell(1);
            Equal(30, sale.TotalSold); Equal(0, sale.TotalUnsold);
            for (int i = 0; i < 9; i++) { Equal(sale.ProducedCounts[i], sale.SoldCounts[i]); Equal(0, sale.UnsoldCounts[i]); }
        }

        private static void DuplicateProcessing()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 30); model.ConfirmProduction();
            Throws<InvalidOperationException>(() => model.ConfirmProduction()); string error; False(model.TryConfirmProduction(out error));
            model.Sell(0.75); long money = model.GetMoney();
            Throws<InvalidOperationException>(() => model.Sell(0.75)); Throws<InvalidOperationException>(() => model.StartSelling());
            Equal(money, model.GetMoney()); Throws<InvalidOperationException>(() => model.ConfirmProduction());
        }

        private static void MoneyAccounting()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 15); Set(model, 2, 15); Equal(33000L, model.EstimatedProductionCost);
            model.ConfirmProduction(); SaleResult sale = model.Sell(0.80);
            Equal(66000L, sale.Revenue); Equal(33000L, sale.ProductionCost); Equal(33000L, sale.NetProfit);
            Equal(133000L, model.GetMoney()); Equal(0, model.GetReputation());
        }

        private static void NextDayReset()
        {
            ShopModel model = new ShopModel(); Throws<InvalidOperationException>(() => model.NextDay());
            model.CompleteResearch("stand_40");
            model.UnlockProduct(8); model.AddReputation((35) - model.GetReputation()); Set(model, 8, 40); model.ConfirmProduction();
            Throws<InvalidOperationException>(() => model.NextDay()); model.Sell(1); long money = model.GetMoney(); model.NextDay();
            Equal(2, model.GetDay()); Equal(40, model.Capacity); Equal(35, model.GetReputation()); True(model.UnlockedProducts[8]);
            Equal(money, model.GetMoney()); Equal(0, model.TotalSelected); Equal(0, model.TotalProduced); Equal(0L, model.CommittedProductionCost);
            Equal(GamePhase.Arrangement, model.GetPhase()); True(model.LastSale == null);
        }

        private static void Events()
        {
            ShopModel model = new ShopModel(); int changed = 0, committed = 0, sold = 0;
            model.Changed += () => changed++;
            model.ProductionConfirmed += receipt => { committed++; Equal(GamePhase.ReadyToSell, model.GetPhase()); Equal(30, receipt.TotalProduced); };
            model.SaleCompleted += result => { sold++; Equal(GamePhase.Closed, model.GetPhase()); True(ReferenceEquals(result, model.LastSale)); };
            Set(model, 0, 30); model.ConfirmProduction(); model.Sell(0.75); Equal(3, changed); Equal(1, committed); Equal(1, sold);
            Throws<InvalidOperationException>(() => model.Sell(0.75)); Equal(3, changed); Equal(1, sold);
        }

        private static void SnapshotRoundtrip()
        {
            ShopModel source = new ShopModel(); source.CompleteResearch("stand_40");
            source.UnlockProduct(8); source.AddReputation((15) - source.GetReputation()); Set(source, 8, 17);
            AssertRoundtrip(source); source.ConfirmProduction(); AssertRoundtrip(source); source.Sell(0.90); AssertRoundtrip(source);
            // Reputation can be changed by an outside system after a sale, while that sale records its original band.
            source.AddReputation((40) - source.GetReputation()); AssertRoundtrip(source);
        }

        private static void AssertRoundtrip(ShopModel source)
        {
            string json = JsonUtility.ToJson(source.CreateSnapshot());
            ShopSnapshot snapshot = JsonUtility.FromJson<ShopSnapshot>(json); ShopModel restored = new ShopModel();
            string error;
            if (!restored.TryRestoreSnapshot(snapshot, out error))
                throw new Exception("Roundtrip failed in " + source.GetPhase() + ": " + error + ". JSON: " + json);
            Equal(json, JsonUtility.ToJson(restored.CreateSnapshot()));
            if (restored.LastSale != null) { Equal(source.LastSale.Revenue, restored.LastSale.Revenue); Equal(source.LastSale.TotalSold, restored.LastSale.TotalSold); }
        }

        private static void SnapshotSalePresence()
        {
            ShopModel model = new ShopModel(); ShopSnapshot inactive = model.CreateSnapshot();
            False(inactive.HasLastSale); True(inactive.LastSale != null);
            model.RestoreSnapshot(inactive); True(model.LastSale == null);
            // JsonUtility may reconstruct a serialized null as a default inline object; it carries no sale.
            inactive.LastSale = new SaleSnapshot(); model.RestoreSnapshot(inactive); True(model.LastSale == null);
            string error; inactive.HasLastSale = true; False(model.TryRestoreSnapshot(inactive, out error));
            inactive.HasLastSale = false; inactive.LastSale.Day = 1; False(model.TryRestoreSnapshot(inactive, out error));
            inactive.LastSale = new SaleSnapshot { SoldCounts = new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0 } };
            False(model.TryRestoreSnapshot(inactive, out error));
            model.ConfirmProduction(); model.Sell(0.75); ShopSnapshot closed = model.CreateSnapshot(); True(closed.HasLastSale);
            closed.HasLastSale = false; False(model.TryRestoreSnapshot(closed, out error));
            Equal(GamePhase.Closed, model.GetPhase()); True(model.LastSale != null);
        }

        private static void SnapshotIsolation()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 30); ShopSnapshot snapshot = model.CreateSnapshot(); snapshot.SelectedCounts[0] = 1;
            Equal(30, model.TotalSelected); ShopModel restored = new ShopModel(); restored.RestoreSnapshot(snapshot); snapshot.SelectedCounts[0] = 2;
            Equal(1, restored.TotalSelected); model.ConfirmProduction(); model.Sell(0.75); snapshot = model.CreateSnapshot();
            snapshot.LastSale.SoldCounts[0] = 0; Equal(22, model.LastSale.TotalSold);
        }

        private static void SnapshotValidation()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 30); model.ConfirmProduction(); model.Sell(0.75);
            string before = JsonUtility.ToJson(model.CreateSnapshot());
            Action<ShopSnapshot>[] corruptions =
            {
                save => save.Version = 99, save => save.Day = 0, save => save.Reputation = -1, save => save.Capacity = 31,
                save => save.Phase = (GamePhase)99, save => save.SelectedCounts = null,
                save => save.SelectedCounts[0] = -1, save => save.SelectedCounts[0] = 31,
                save => save.UnlockedProducts[0] = false, save => save.ProducedCounts[5] = 1,
                save => save.ProductionCost++, save => save.LastSale = null,
                save => save.HasLastSale = false,
                save => save.LastSale.Rate = 1, save => save.LastSale.SoldCounts[0]++,
                save => save.LastSale.Revenue++, save => save.LastSale.ProducedCounts[0]--
            };
            string error; False(model.TryRestoreSnapshot(null, out error));
            foreach (Action<ShopSnapshot> corrupt in corruptions)
            {
                ShopSnapshot bad = model.CreateSnapshot(); corrupt(bad); False(model.TryRestoreSnapshot(bad, out error));
                True(!string.IsNullOrEmpty(error)); Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
            }
            ShopSnapshot ready = new ShopModel().CreateSnapshot(); ready.Phase = GamePhase.ReadyToSell;
            ready.LastSale = model.CreateSnapshot().LastSale; False(model.TryRestoreSnapshot(ready, out error));
            Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
        }

        private static void CustomerTasteCategories()
        {
            RiceCakeTaste[] tastes = { RiceCakeTaste.Mild, RiceCakeTaste.Mild, RiceCakeTaste.Mild, RiceCakeTaste.Nutty,
                RiceCakeTaste.Sweet, RiceCakeTaste.Mild, RiceCakeTaste.Sweet, RiceCakeTaste.Nutty, RiceCakeTaste.Unspecified };
            for (int i = 0; i < tastes.Length; i++) Equal(tastes[i], ShopModel.Catalog[i].Taste);
        }

        private static void CustomerAnswers()
        {
            int[] productIndices = { 0, 4, 3 };
            for (int answer = 1; answer <= 3; answer++)
            {
                ShopModel model = new ShopModel(); Set(model, productIndices[answer - 1], 1);
                CustomerVisit customer = model.OpenDayWithCustomer(answer);
                Equal((RiceCakeTaste)answer, customer.Answer); Equal(1, customer.Day); Equal(0, customer.ReputationBefore);
                Equal(CustomerStep.Opening, customer.Step); False(customer.IsResolved); Equal(-1, customer.RecommendedProductIndex);
                model.PresentCustomer(); model.SelectCustomerTaste((RiceCakeTaste)answer);
                string error; True(model.TryRecommendProduct(productIndices[answer - 1], out error)); True(error == null);
                Equal(1, model.GetReputation()); True(customer.WasSatisfied); True(customer.IsResolved); Equal(CustomerStep.Feedback, customer.Step);
                False(model.TryRecommendProduct(productIndices[answer - 1], out error)); Equal(1, model.GetReputation());
                model.FinishCustomerFeedback(); Equal(CustomerStep.Result, customer.Step); model.NextDay();
                Equal(2, model.GetDay()); Equal(1, model.GetReputation()); True(model.Customer == null);
            }
        }

        private static void CustomerSelectionFilters()
        {
            ShopModel model = new ShopModel(); model.UnlockProduct(8); Set(model, 0, 1); Set(model, 4, 1);
            model.OpenDayWithCustomer(1); model.PresentCustomer();
            string error; False(model.TryRecommendProduct(0, out error)); model.SelectCustomerTaste(RiceCakeTaste.Mild);
            False(model.TryRecommendProduct(-1, out error)); False(model.TryRecommendProduct(9, out error));
            False(model.TryRecommendProduct(4, out error)); False(model.TryRecommendProduct(3, out error));
            False(model.TryRecommendProduct(5, out error)); False(model.TryRecommendProduct(8, out error));
            Equal(CustomerStep.AwaitingRecommendation, model.Customer.Step); Equal(0, model.GetReputation());
            model.UnlockProduct(5); False(model.TryRecommendProduct(5, out error)); True(model.HasRecommendableProduct);
            False(model.TryDeclineRecommendation(out error)); True(model.TryRecommendProduct(0, out error)); Equal(1, model.GetReputation());
            True(model.Customer.WasSatisfied);
            model = new ShopModel(); model.UnlockProduct(5); Set(model, 5, 1); model.OpenDayWithCustomer(1);
            model.PresentCustomer(); model.SelectCustomerTaste(RiceCakeTaste.Mild); True(model.TryRecommendProduct(5, out error));
            Equal(1, model.TotalProduced); Equal(1, model.GetReputation());
        }

        private static void CustomerIncorrectRecommendation()
        {
            ShopModel model = new ShopModel(); model.AddReputation((9) - model.GetReputation()); Set(model, 4, 1); model.OpenDayWithCustomer(1); model.PresentCustomer();
            model.SelectCustomerTaste(RiceCakeTaste.Sweet); string error; True(model.TryRecommendProduct(4, out error));
            False(model.Customer.WasSatisfied); True(model.Customer.IsResolved); Equal(9, model.GetReputation());
            False(model.TryRecommendProduct(0, out error)); Equal(9, model.GetReputation());
            Throws<InvalidOperationException>(() => model.SelectCustomerTaste(RiceCakeTaste.Mild));
            model.FinishCustomerFeedback(); Throws<InvalidOperationException>(() => model.FinishCustomerFeedback());
            False(model.TryRecommendProduct(4, out error)); Equal(9, model.GetReputation());
        }

        private static void CustomerSalesBandAtOpening()
        {
            ShopModel model = new ShopModel(); model.AddReputation((14) - model.GetReputation()); Set(model, 0, 30); model.OpenDayWithCustomer(1);
            SaleResult sale = model.LastSale; True(sale.Rate >= 0.75 && sale.Rate <= 0.80); Equal(14, sale.Reputation);
            Equal(14, model.Customer.ReputationBefore); model.PresentCustomer(); model.SelectCustomerTaste(RiceCakeTaste.Mild);
            string error; True(model.TryRecommendProduct(0, out error)); Equal(15, model.GetReputation());
            True(ReferenceEquals(sale, model.LastSale)); Equal(14, model.LastSale.Reputation);
            model.FinishCustomerFeedback(); model.NextDay(); Set(model, 0, 30); model.OpenDayWithCustomer(1);
            Equal(15, model.LastSale.Reputation); True(model.LastSale.Rate >= 0.85 && model.LastSale.Rate <= 0.95);
        }

        private static void CustomerOpeningAndProgression()
        {
            ShopModel model = new ShopModel(); model.CompleteResearch("stand_40");
            model.UnlockProduct(5); Set(model, 0, 39); Set(model, 4, 1);
            model.ConfirmProduction(); model.OpenDayWithCustomer(2); Equal(GamePhase.Closed, model.GetPhase()); Equal(40, model.TotalProduced);
            Throws<InvalidOperationException>(() => model.OpenDayWithCustomer(2)); Throws<InvalidOperationException>(() => model.NextDay());
            Throws<InvalidOperationException>(() => model.SelectCustomerTaste(RiceCakeTaste.Sweet)); model.PresentCustomer();
            Throws<InvalidOperationException>(() => model.PresentCustomer()); Throws<InvalidOperationException>(() => model.NextDay());
            Throws<ArgumentOutOfRangeException>(() => model.SelectCustomerTaste(RiceCakeTaste.Unspecified));
            Throws<ArgumentOutOfRangeException>(() => model.SelectCustomerTaste((RiceCakeTaste)4));
            model.SelectCustomerTaste(RiceCakeTaste.Sweet); string error; True(model.TryRecommendProduct(4, out error));
            Throws<InvalidOperationException>(() => model.NextDay()); model.FinishCustomerFeedback(); model.NextDay();
            Equal(40, model.Capacity); True(model.UnlockedProducts[5]); True(model.Customer == null); True(model.LastSale == null);
            Equal(0, model.TotalSelected); Equal(0, model.TotalProduced); Equal(GamePhase.Arrangement, model.GetPhase());
            ShopModel invalid = new ShopModel(); Throws<ArgumentOutOfRangeException>(() => invalid.OpenDayWithCustomer(-1));
            Throws<ArgumentOutOfRangeException>(() => invalid.OpenDayWithCustomer(4)); Equal(GamePhase.Arrangement, invalid.GetPhase());
            for (int i = 0; i < 12; i++)
            {
                CustomerVisit visit = new ShopModel().OpenDayWithCustomer(); True((int)visit.Answer >= 1 && (int)visit.Answer <= 3);
            }
        }

        private static void CustomerDialogue()
        {
            ShopModel model = new ShopModel(); Throws<InvalidOperationException>(() => model.SetCustomerDialogue("대사", true));
            Set(model, 3, 1);
            model.OpenDayWithCustomer(3); const string odd = "구름을 접어서 씹고 싶어요. 123?!\n✨";
            model.SetCustomerDialogue(odd, true); Equal(odd, model.Customer.Dialogue); True(model.Customer.GeneratedWithAi);
            model.PresentCustomer(); model.SetCustomerDialogue("고소한 떡을 찾고 있어요.", false); False(model.Customer.GeneratedWithAi);
            Equal("고소한 떡을 찾고 있어요.", model.Customer.Dialogue); model.SelectCustomerTaste(RiceCakeTaste.Nutty);
            string error; True(model.TryRecommendProduct(3, out error));
            Throws<InvalidOperationException>(() => model.SetCustomerDialogue("변경", false));
        }

        private static void CustomerSnapshotRoundtrip()
        {
            ShopModel source = new ShopModel(); source.AddReputation((14) - source.GetReputation()); Set(source, 0, 30); source.OpenDayWithCustomer(1);
            source.SetCustomerDialogue("담백한 한입을 원해요.", true); AssertRoundtrip(source); source.PresentCustomer(); AssertRoundtrip(source);
            source.SelectCustomerTaste(RiceCakeTaste.Mild); AssertRoundtrip(source); string error;
            True(source.TryRecommendProduct(0, out error)); AssertRoundtrip(source); source.FinishCustomerFeedback(); AssertRoundtrip(source);
            ShopModel restored = new ShopModel(); restored.RestoreSnapshot(JsonUtility.FromJson<ShopSnapshot>(JsonUtility.ToJson(source.CreateSnapshot())));
            Equal(CustomerStep.Result, restored.Customer.Step); Equal(15, restored.GetReputation()); True(restored.Customer.WasSatisfied);
            False(restored.TryRecommendProduct(0, out error)); Equal(15, restored.GetReputation()); restored.NextDay(); AssertRoundtrip(restored);
            source = new ShopModel(); Set(source, 3, 1); source.OpenDayWithCustomer(1); source.PresentCustomer(); source.SelectCustomerTaste(RiceCakeTaste.Nutty);
            True(source.TryRecommendProduct(3, out error)); AssertRoundtrip(source); False(source.Customer.WasSatisfied);
            ShopSnapshot separate = source.CreateSnapshot(); separate.Customer.Dialogue = "수정된 저장 대사";
            Equal(string.Empty, source.Customer.Dialogue);
        }

        private static void CustomerSnapshotMigration()
        {
            ShopModel source = new ShopModel(); Set(source, 0, 17); source.ConfirmProduction(); source.Sell(0.80);
            ShopSnapshot legacy = source.CreateSnapshot(); legacy.Version = 1; legacy.HasCustomer = false; legacy.Customer = null;
            ShopSnapshot roundtrip = JsonUtility.FromJson<ShopSnapshot>(JsonUtility.ToJson(legacy)); ShopModel restored = new ShopModel();
            restored.RestoreSnapshot(roundtrip); True(restored.Customer == null); Equal(5, restored.CreateSnapshot().Version);
            Equal(source.GetMoney(), restored.GetMoney()); Equal(source.LastSale.TotalSold, restored.LastSale.TotalSold);
            restored.NextDay(); Equal(2, restored.GetDay());
            ShopSnapshot inactive = restored.CreateSnapshot(); inactive.Customer = new CustomerSnapshot(); restored.RestoreSnapshot(inactive);
            True(restored.Customer == null); inactive.Customer.Answer = RiceCakeTaste.Mild; string error;
            False(restored.TryRestoreSnapshot(inactive, out error));
        }

        private static void CustomerSnapshotValidation()
        {
            ShopModel model = new ShopModel(); Set(model, 4, 1); model.OpenDayWithCustomer(2); model.PresentCustomer(); model.SelectCustomerTaste(RiceCakeTaste.Sweet);
            string error; True(model.TryRecommendProduct(4, out error)); string before = JsonUtility.ToJson(model.CreateSnapshot());
            Action<ShopSnapshot>[] corruptions =
            {
                save => save.Version = 1, save => save.HasCustomer = false, save => save.Customer = null,
                save => save.Customer.Day++, save => save.Customer.Answer = RiceCakeTaste.Unspecified,
                save => save.Customer.SelectedTaste = RiceCakeTaste.Unspecified, save => save.Customer.Step = CustomerStep.None,
                save => save.Customer.ReputationBefore++, save => save.Customer.WasSatisfied = false,
                save => save.Customer.RecommendedProductIndex = 0, save => save.Customer.RecommendedProductIndex = 6,
                save => save.Customer.Step = CustomerStep.AwaitingRecommendation
            };
            foreach (Action<ShopSnapshot> corrupt in corruptions)
            {
                ShopSnapshot bad = model.CreateSnapshot(); corrupt(bad); False(model.TryRestoreSnapshot(bad, out error));
                True(!string.IsNullOrEmpty(error)); Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
            }
            ShopModel opening = new ShopModel(); opening.OpenDayWithCustomer(1); ShopSnapshot badOpening = opening.CreateSnapshot();
            badOpening.Customer.SelectedTaste = RiceCakeTaste.Mild; False(opening.TryRestoreSnapshot(badOpening, out error));
            badOpening = opening.CreateSnapshot(); badOpening.Customer.RecommendedProductIndex = 0;
            False(opening.TryRestoreSnapshot(badOpening, out error)); Equal(CustomerStep.Opening, opening.Customer.Step);
        }

        private static void CustomerDeclineEmptyDisplay()
        {
            ShopModel model = new ShopModel(); string error; False(model.TryDeclineRecommendation(out error));
            model.AddReputation((7) - model.GetReputation()); model.OpenDayWithCustomer(1); False(model.HasRecommendableProduct);
            False(model.TryDeclineRecommendation(out error)); model.PresentCustomer();
            model.SelectCustomerTaste(RiceCakeTaste.Mild); False(model.TryRecommendProduct(0, out error));
            Equal(CustomerStep.AwaitingRecommendation, model.Customer.Step); Equal(7, model.GetReputation());
            True(model.TryDeclineRecommendation(out error)); Equal(CustomerStep.Feedback, model.Customer.Step);
            True(model.Customer.IsResolved); False(model.Customer.WasSatisfied); Equal(-1, model.Customer.RecommendedProductIndex);
            Equal(7, model.GetReputation()); False(model.TryDeclineRecommendation(out error)); False(model.TryRecommendProduct(0, out error));
            Throws<InvalidOperationException>(() => model.NextDay()); model.FinishCustomerFeedback(); model.NextDay();
            Equal(2, model.GetDay()); Equal(7, model.GetReputation()); True(model.Customer == null);
            Equal(100000L, model.GetMoney());
        }

        private static void CustomerDeclineUnclassifiedDisplay()
        {
            ShopModel model = new ShopModel(); model.UnlockProduct(8); Set(model, 8, 30); model.OpenDayWithCustomer(3);
            Equal(30, model.TotalProduced); False(model.HasRecommendableProduct); model.PresentCustomer(); string error;
            foreach (RiceCakeTaste taste in new[] { RiceCakeTaste.Mild, RiceCakeTaste.Sweet, RiceCakeTaste.Nutty })
            {
                model.SelectCustomerTaste(taste); False(model.TryRecommendProduct(8, out error));
            }
            True(model.TryDeclineRecommendation(out error)); False(model.Customer.WasSatisfied); Equal(0, model.GetReputation());
            model.FinishCustomerFeedback(); model.NextDay(); Equal(2, model.GetDay());
        }

        private static void CustomerConfirmedDisplay()
        {
            ShopModel model = new ShopModel(); model.AddReputation((35) - model.GetReputation()); Set(model, 0, 1); model.OpenDayWithCustomer(1);
            Equal(0, model.LastSale.UnsoldCounts[0]); True(model.HasRecommendableProduct); model.PresentCustomer();
            model.SelectCustomerTaste(RiceCakeTaste.Mild); string error; False(model.TryDeclineRecommendation(out error));
            False(model.TryRecommendProduct(1, out error)); True(model.TryRecommendProduct(0, out error)); Equal(36, model.GetReputation());
            model = new ShopModel(); Set(model, 0, 1); model.OpenDayWithCustomer(2); model.PresentCustomer();
            model.SelectCustomerTaste(RiceCakeTaste.Sweet); False(model.TryRecommendProduct(4, out error));
            False(model.TryDeclineRecommendation(out error)); Equal(CustomerStep.AwaitingRecommendation, model.Customer.Step);
            model.SelectCustomerTaste(RiceCakeTaste.Mild); True(model.TryRecommendProduct(0, out error));
            False(model.Customer.WasSatisfied); Equal(0, model.GetReputation());
        }

        private static void CustomerDeclineSnapshotValidation()
        {
            ShopModel source = new ShopModel(); source.AddReputation((14) - source.GetReputation()); source.OpenDayWithCustomer(1); source.PresentCustomer();
            string error; True(source.TryDeclineRecommendation(out error)); AssertRoundtrip(source);
            ShopModel restored = new ShopModel(); restored.RestoreSnapshot(JsonUtility.FromJson<ShopSnapshot>(JsonUtility.ToJson(source.CreateSnapshot())));
            False(restored.HasRecommendableProduct); True(restored.Customer.IsResolved); False(restored.Customer.WasSatisfied);
            Equal(RiceCakeTaste.Unspecified, restored.Customer.SelectedTaste); Equal(14, restored.GetReputation());
            False(restored.TryDeclineRecommendation(out error)); False(restored.TryRecommendProduct(0, out error));
            restored.FinishCustomerFeedback(); AssertRoundtrip(restored); restored.NextDay(); Equal(2, restored.GetDay()); Equal(14, restored.GetReputation());
            ShopSnapshot invalid = source.CreateSnapshot(); invalid.Customer.WasSatisfied = true;
            False(source.TryRestoreSnapshot(invalid, out error));
            ShopModel recommendable = new ShopModel(); Set(recommendable, 0, 1); recommendable.OpenDayWithCustomer(1);
            recommendable.PresentCustomer(); recommendable.SelectCustomerTaste(RiceCakeTaste.Mild);
            True(recommendable.TryRecommendProduct(0, out error)); invalid = recommendable.CreateSnapshot();
            invalid.Customer.RecommendedProductIndex = -1; invalid.Customer.WasSatisfied = false;
            False(recommendable.TryRestoreSnapshot(invalid, out error));
            invalid = recommendable.CreateSnapshot(); invalid.Customer.RecommendedProductIndex = 1;
            False(recommendable.TryRestoreSnapshot(invalid, out error));
            source = new ShopModel(); source.UnlockProduct(8); Set(source, 8, 1); source.OpenDayWithCustomer(3); source.PresentCustomer();
            source.SelectCustomerTaste(RiceCakeTaste.Nutty); True(source.TryDeclineRecommendation(out error));
            AssertRoundtrip(source); source.FinishCustomerFeedback(); AssertRoundtrip(source);
        }

        private static ShopModel FundedModel(long money, int reputation = 0, int capacity = 30)
        {
            ShopModel model = new ShopModel(); ShopSnapshot snapshot = model.CreateSnapshot();
            snapshot.Version = 4; // Legacy fixture intentionally supplies the old capacity/unlock representation.
            snapshot.Money = money; snapshot.Reputation = reputation; snapshot.Capacity = capacity;
            model.RestoreSnapshot(snapshot); return model;
        }

        private static void OpenResearchEvening(ShopModel model, ResearchKind kind)
        {
            model.ConfirmProduction(); model.Sell(ShopModel.GetSalesBand(model.GetReputation()).Minimum);
            model.BeginEvening(); model.ChooseResearch(true); model.ChooseResearchKind(kind);
        }

        private static void EveningNavigation()
        {
            ShopModel model = FundedModel(900000, 35); OpenResearchEvening(model, ResearchKind.Cake);
            long money = model.GetMoney(); Equal(EveningStep.CakeResearch, model.Evening);
            model.BackFromEvening(); Equal(EveningStep.Choice, model.Evening); Equal(money, model.GetMoney());
            model.ChooseResearchKind(ResearchKind.Stand); Equal(EveningStep.StandResearch, model.Evening);
            model.BackFromEvening(); model.BackFromEvening(); Equal(EveningStep.Prompt, model.Evening);
            model.ChooseResearch(false); Equal(EveningStep.RestConfirmation, model.Evening);
            model.BackFromEvening(); Equal(EveningStep.Prompt, model.Evening);
            Equal(1, model.GetDay()); Equal(30, model.Capacity); Equal(money, model.GetMoney());
            True(model.PendingResearch == null); False(model.UnlockedProducts[5]);
            Throws<InvalidOperationException>(() => model.BackFromEvening());
            Throws<InvalidOperationException>(() => model.ChooseResearchKind(ResearchKind.Cake));
        }

        private static void EveningStartGuards()
        {
            ShopModel model = new ShopModel(); Throws<InvalidOperationException>(() => model.BeginEvening());
            model.OpenDayWithCustomer(1); Throws<InvalidOperationException>(() => model.BeginEvening());
            model.PresentCustomer(); Throws<InvalidOperationException>(() => model.BeginEvening());
            string error; True(model.TryDeclineRecommendation(out error)); Throws<InvalidOperationException>(() => model.BeginEvening());
            model.FinishCustomerFeedback(); model.BeginEvening(); Equal(EveningStep.Prompt, model.Evening);
            Throws<InvalidOperationException>(() => model.BeginEvening()); Throws<InvalidOperationException>(() => model.NextDay());
            model.ChooseResearch(true); Throws<ArgumentOutOfRangeException>(() => model.ChooseResearchKind(ResearchKind.None));
            Equal(EveningStep.Choice, model.Evening); Throws<InvalidOperationException>(() => model.NextDay());
        }

        private static void EveningRest()
        {
            ShopModel model = FundedModel(300000, 20); model.ConfirmProduction(); model.Sell(0.90); model.BeginEvening();
            Throws<InvalidOperationException>(() => model.ConfirmRest()); model.ChooseResearch(false);
            Throws<InvalidOperationException>(() => model.NextDay()); Equal(1, model.GetDay());
            model.ConfirmRest(); Equal(2, model.GetDay()); Equal(300000L, model.GetMoney()); Equal(EveningStep.None, model.Evening);
            Equal(string.Empty, model.MorningMessage); Equal(30, model.Capacity); False(model.UnlockedProducts[5]);
            Throws<InvalidOperationException>(() => model.ConfirmRest()); Set(model, 0, 1);
        }

        private static void ResearchFailureGuards()
        {
            ShopModel model = FundedModel(59999); string error;
            False(model.TryScheduleCakeResearch(5, out error)); Equal(59999L, model.GetMoney());
            OpenResearchEvening(model, ResearchKind.Cake); string before = JsonUtility.ToJson(model.CreateSnapshot());
            False(model.TryScheduleCakeResearch(5, out error)); Equal("연구비가 부족합니다.", error);
            False(model.TryScheduleCakeResearch(6, out error)); Equal("연구비가 부족합니다.", error);
            False(model.TryScheduleCakeResearch(0, out error)); Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
            model = FundedModel(1000000); OpenResearchEvening(model, ResearchKind.Cake);
            before = JsonUtility.ToJson(model.CreateSnapshot()); False(model.TryScheduleCakeResearch(6, out error));
            True(error.Contains("쑥떡쑥떡")); False(model.TryScheduleCakeResearch(8, out error));
            True(error.Contains("쑥떡쑥떡")); Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
            False(model.TryScheduleStandResearch(out error)); True(model.PendingResearch == null);
        }

        private static void SequentialCakeResearch()
        {
            ShopModel model = FundedModel(1500000); long[] costs = { 60000, 180000, 360000, 720000 };
            string error; long expectedMoney = model.GetMoney();
            for (int index = 5; index <= 8; index++)
            {
                OpenResearchEvening(model, ResearchKind.Cake);
                Equal(costs[index - 5], model.GetCakeResearchCost(index));
                for (int later = index + 1; later <= 8; later++) False(model.TryScheduleCakeResearch(later, out error));
                True(model.TryScheduleCakeResearch(index, out error)); expectedMoney -= costs[index - 5];
                Equal(expectedMoney, model.GetMoney()); False(model.UnlockedProducts[index]); Equal(index - 4, model.GetDay());
                Equal(ResearchKind.Cake, model.PendingResearch.Kind); Equal(index, model.PendingResearch.ProductIndex);
                model.NextDay(); True(model.UnlockedProducts[index]); Equal(expectedMoney, model.GetMoney());
                Equal(ShopModel.CakeResearchMorningMessage, model.MorningMessage); model.AcknowledgeMorning();
                for (int previous = 5; previous <= index; previous++) True(model.UnlockedProducts[previous]);
            }
            Equal(5, model.GetDay()); Equal(180000L, model.GetMoney());
            OpenResearchEvening(model, ResearchKind.Cake); False(model.TryScheduleCakeResearch(5, out error));
            True(error.Contains("완료")); Equal(180000L, model.GetMoney());
        }

        private static void SingleEveningAction()
        {
            ShopModel model = FundedModel(1000000, 35); OpenResearchEvening(model, ResearchKind.Cake);
            string error; True(model.TryScheduleCakeResearch(5, out error)); long money = model.GetMoney();
            False(model.TryScheduleCakeResearch(5, out error)); False(model.TryScheduleCakeResearch(6, out error));
            False(model.TryScheduleStandResearch(out error)); Throws<InvalidOperationException>(() => model.BackFromEvening());
            Throws<InvalidOperationException>(() => model.ChooseResearchKind(ResearchKind.Stand));
            Throws<InvalidOperationException>(() => model.ConfirmRest()); Equal(money, model.GetMoney());
            model.NextDay(); Throws<InvalidOperationException>(() => model.NextDay());
            Equal(2, model.GetDay()); True(model.UnlockedProducts[5]); False(model.UnlockedProducts[6]); Equal(30, model.Capacity);
        }

        private static void MorningProductionGuards()
        {
            ShopModel model = FundedModel(60000); OpenResearchEvening(model, ResearchKind.Cake); string error;
            True(model.TryScheduleCakeResearch(5, out error)); model.NextDay(); Equal(GamePhase.Arrangement, model.GetPhase());
            False(model.TrySetCount(5, 1, out error)); False(model.TrySetCount(0, 0, out error));
            False(model.TryAdjustQuantity("garaetteok", 1, out error)); False(model.TryConfirmProduction(out error));
            Throws<InvalidOperationException>(() => model.ConfirmProduction()); Throws<InvalidOperationException>(() => model.ClearSelection());
            Throws<InvalidOperationException>(() => model.AutoDistribute()); Throws<InvalidOperationException>(() => model.OpenDayWithCustomer());
            Throws<InvalidOperationException>(() => model.BeginEvening()); Equal(0, model.TotalSelected); Equal(0, model.TotalProduced);
            model.AcknowledgeMorning(); Equal(EveningStep.None, model.Evening); Equal(string.Empty, model.MorningMessage);
            Throws<InvalidOperationException>(() => model.AcknowledgeMorning()); Set(model, 5, 1); model.ConfirmProduction();
        }

        private static void StandResearchProgression()
        {
            ShopModel model = FundedModel(1100000, 15); string error;
            int[] targets = { 40, 50, 60, 70 }; long[] costs = { 150000, 150000, 400000, 400000 };
            long remaining = 1100000;
            for (int step = 0; step < 4; step++)
            {
                if (step == 2) model.AddReputation((35) - model.GetReputation());
                OpenResearchEvening(model, ResearchKind.Stand); Equal(costs[step], model.GetStandResearchCost());
                Equal(targets[step], model.GetStandResearchTargetCapacity()); True(model.GetStandResearchError() == null);
                True(model.TryScheduleStandResearch(out error)); remaining -= costs[step]; Equal(remaining, model.GetMoney());
                Equal(targets[step] - 10, model.Capacity); Equal(targets[step], model.PendingResearch.TargetCapacity);
                model.NextDay(); Equal(targets[step], model.Capacity); Equal(remaining, model.GetMoney());
                Equal(ShopModel.StandResearchMorningMessage, model.MorningMessage); model.AcknowledgeMorning();
            }
            Equal(70, model.Capacity); Equal(0L, model.GetMoney()); Equal(5, model.GetDay());
        }

        private static void StandResearchFailure()
        {
            string error; ShopModel model = FundedModel(149999, 0); OpenResearchEvening(model, ResearchKind.Stand);
            string before = JsonUtility.ToJson(model.CreateSnapshot()); False(model.TryScheduleStandResearch(out error));
            Equal("업그레이드 비용이 부족합니다.", error); Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
            model = FundedModel(150000, 14); OpenResearchEvening(model, ResearchKind.Stand);
            False(model.TryScheduleStandResearch(out error)); True(error.Contains("15")); Equal(150000L, model.GetMoney());
            model = FundedModel(400000, 34, 50); OpenResearchEvening(model, ResearchKind.Stand);
            False(model.TryScheduleStandResearch(out error)); True(error.Contains("35")); Equal(50, model.Capacity);
            model = FundedModel(400000, 35, 40); OpenResearchEvening(model, ResearchKind.Stand);
            True(model.TryScheduleStandResearch(out error)); Equal(150000L, model.PendingResearch.Cost);
            Equal(50, model.PendingResearch.TargetCapacity); Equal(40, model.Capacity);
            foreach (int cap in new[] { 70, 80 })
            {
                model = FundedModel(1000000, 100, cap); OpenResearchEvening(model, ResearchKind.Stand);
                False(model.TryScheduleStandResearch(out error)); True(error.Contains("70")); Equal(cap, model.Capacity); Equal(1000000L, model.GetMoney());
            }
        }

        private static void PendingResearchRoundtrip()
        {
            foreach (ResearchKind kind in new[] { ResearchKind.Cake, ResearchKind.Stand })
            {
                ShopModel source = FundedModel(500000, 35); OpenResearchEvening(source, kind); string error;
                True(kind == ResearchKind.Cake ? source.TryScheduleCakeResearch(5, out error) : source.TryScheduleStandResearch(out error));
                AssertRoundtrip(source); long chargedMoney = source.GetMoney();
                ShopModel restored = new ShopModel(); restored.RestoreSnapshot(JsonUtility.FromJson<ShopSnapshot>(JsonUtility.ToJson(source.CreateSnapshot())));
                Equal(kind, restored.PendingResearch.Kind); False(restored.TryScheduleCakeResearch(5, out error));
                False(restored.TryScheduleStandResearch(out error)); Equal(chargedMoney, restored.GetMoney());
                restored.NextDay(); Equal(chargedMoney, restored.GetMoney()); True(restored.PendingResearch == null); AssertRoundtrip(restored);
                ShopModel morning = new ShopModel(); morning.RestoreSnapshot(JsonUtility.FromJson<ShopSnapshot>(JsonUtility.ToJson(restored.CreateSnapshot())));
                Equal(EveningStep.MorningNotice, morning.Evening); Equal(2, morning.GetDay()); morning.AcknowledgeMorning();
                Equal(chargedMoney, morning.GetMoney()); Equal(kind == ResearchKind.Stand ? 40 : 30, morning.Capacity);
                Equal(kind == ResearchKind.Cake, morning.UnlockedProducts[5]);
            }
        }

        private static void EveningSnapshotRoundtrip()
        {
            ShopModel model = FundedModel(1000000, 35); model.ConfirmProduction(); model.Sell(1); model.BeginEvening(); AssertRoundtrip(model);
            model.ChooseResearch(true); AssertRoundtrip(model); model.ChooseResearchKind(ResearchKind.Cake); AssertRoundtrip(model);
            model.BackFromEvening(); model.ChooseResearchKind(ResearchKind.Stand); AssertRoundtrip(model);
            model.BackFromEvening(); model.BackFromEvening(); model.ChooseResearch(false); AssertRoundtrip(model);
            model.BackFromEvening(); model.ChooseResearch(true); model.ChooseResearchKind(ResearchKind.Cake);
            string error; True(model.TryScheduleCakeResearch(5, out error)); AssertRoundtrip(model); model.NextDay(); AssertRoundtrip(model);
            model.AcknowledgeMorning(); AssertRoundtrip(model);
            ShopSnapshot placeholder = model.CreateSnapshot(); placeholder.PendingResearch = new PendingResearchSnapshot();
            model.RestoreSnapshot(placeholder); True(model.PendingResearch == null);
            placeholder.PendingResearch.Cost = 1; False(model.TryRestoreSnapshot(placeholder, out error));
        }

        private static void EveningSnapshotValidation()
        {
            ShopModel model = FundedModel(1000000, 35); OpenResearchEvening(model, ResearchKind.Cake); string error;
            True(model.TryScheduleCakeResearch(5, out error)); string before = JsonUtility.ToJson(model.CreateSnapshot());
            Action<ShopSnapshot>[] corruptions =
            {
                save => save.Version = 2, save => save.Evening = (EveningStep)99, save => save.Evening = EveningStep.Prompt,
                save => save.HasPendingResearch = false, save => save.PendingResearch = null,
                save => save.PendingResearch.Kind = ResearchKind.None, save => save.PendingResearch.ProductIndex = 6,
                save => save.PendingResearch.RequestedDay++, save => save.PendingResearch.Cost++,
                save => save.PendingResearch.TargetCapacity = 40, save => save.UnlockedProducts[5] = true,
                save => save.MorningMessage = ShopModel.CakeResearchMorningMessage, save => save.Money = -1
            };
            foreach (Action<ShopSnapshot> corruption in corruptions)
            {
                ShopSnapshot bad = model.CreateSnapshot(); corruption(bad); False(model.TryRestoreSnapshot(bad, out error));
                Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
            }
            model.NextDay(); before = JsonUtility.ToJson(model.CreateSnapshot());
            foreach (Action<ShopSnapshot> corruption in new Action<ShopSnapshot>[]
            {
                save => save.MorningMessage = string.Empty, save => save.SelectedCounts[0] = 1,
                save => save.Evening = EveningStep.None, save => save.HasPendingResearch = true,
                save => save.PendingResearch.RequestedDay = save.Day
            })
            {
                ShopSnapshot bad = model.CreateSnapshot(); corruption(bad); False(model.TryRestoreSnapshot(bad, out error));
                Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
            }
            model = FundedModel(1000000, 35, 50); OpenResearchEvening(model, ResearchKind.Stand); True(model.TryScheduleStandResearch(out error));
            foreach (Action<ShopSnapshot> corruption in new Action<ShopSnapshot>[]
            {
                save => save.PendingResearch.Cost = 150000, save => save.PendingResearch.TargetCapacity = 70,
                save => save.PendingResearch.ProductIndex = 5, save => save.PendingResearch.Kind = ResearchKind.Cake
            })
            {
                ShopSnapshot bad = model.CreateSnapshot(); corruption(bad); False(model.TryRestoreSnapshot(bad, out error));
                Equal(50, model.Capacity); Equal(600000L, model.GetMoney());
            }
        }

        private static void EveningSnapshotMigration()
        {
            ShopModel model = FundedModel(900000, 35, 80); model.UnlockProduct(8); Set(model, 8, 3);
            ShopSnapshot legacy = model.CreateSnapshot(); legacy.Version = 2; legacy.PendingResearch = null; legacy.MorningMessage = null;
            ShopModel restored = new ShopModel(); restored.RestoreSnapshot(JsonUtility.FromJson<ShopSnapshot>(JsonUtility.ToJson(legacy)));
            Equal(5, restored.CreateSnapshot().Version); Equal(80, restored.Capacity); Equal(900000L, restored.GetMoney());
            True(restored.UnlockedProducts[8]); Equal(3, restored.SelectedCounts[8]); Equal(EveningStep.None, restored.Evening);
            True(restored.PendingResearch == null); Equal(string.Empty, restored.MorningMessage);
            legacy.Evening = EveningStep.Prompt; string error; False(restored.TryRestoreSnapshot(legacy, out error));
        }

        private static void ResearchExternalCompatibility()
        {
            ShopModel model = FundedModel(1000000, 35); Throws<InvalidOperationException>(() => model.UpgradeCapacity());
            Equal(30, model.Capacity); Equal(1000000L, model.GetMoney()); model.UnlockProduct(8); True(model.UnlockedProducts[8]);
            False(model.UnlockedProducts[5]); False(model.UnlockedProducts[6]);
            OpenResearchEvening(model, ResearchKind.Stand); model.UpgradeCapacity(); Equal(30, model.Capacity);
            Equal(850000L, model.GetMoney()); model.NextDay(); Equal(40, model.Capacity);
        }

        private static void CustomerDeferredSettlement()
        {
            ShopModel model = new ShopModel(); model.AddReputation((35) - model.GetReputation()); Set(model, 0, 30); long before = model.GetMoney();
            False(model.IsSaleSettled); model.OpenDayWithCustomer(1);
            Equal(45000L, model.LastSale.NetProfit); Equal(before, model.GetMoney()); False(model.IsSaleSettled);
            model.PresentCustomer(); Equal(before, model.GetMoney()); False(model.IsSaleSettled);
            model.SelectCustomerTaste(RiceCakeTaste.Mild); string error; True(model.TryRecommendProduct(0, out error));
            Equal(36, model.GetReputation()); Equal(before, model.GetMoney()); False(model.IsSaleSettled);
            long resultMoney = before + model.LastSale.NetProfit;
            model.FinishCustomerFeedback(); Equal(CustomerStep.Result, model.Customer.Step);
            Equal(resultMoney, model.GetMoney()); True(model.IsSaleSettled);
            Throws<InvalidOperationException>(() => model.FinishCustomerFeedback()); Equal(resultMoney, model.GetMoney());
            model.BeginEvening(); model.ChooseResearch(false); model.ConfirmRest();
            Equal(resultMoney, model.GetMoney()); False(model.IsSaleSettled); Equal(2, model.GetDay());
        }

        private static void CustomerNegativeSettlement()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 1); model.OpenDayWithCustomer(2);
            Equal(-1000L, model.LastSale.NetProfit); Equal(100000L, model.GetMoney()); False(model.IsSaleSettled);
            model.PresentCustomer(); model.SelectCustomerTaste(RiceCakeTaste.Mild); string error;
            True(model.TryRecommendProduct(0, out error)); False(model.Customer.WasSatisfied);
            Equal(0, model.GetReputation()); Equal(100000L, model.GetMoney()); False(model.IsSaleSettled);
            model.FinishCustomerFeedback(); Equal(99000L, model.GetMoney()); True(model.IsSaleSettled);
            False(model.TryRecommendProduct(0, out error)); Throws<InvalidOperationException>(() => model.FinishCustomerFeedback());
            Equal(99000L, model.GetMoney());
        }

        private static void CustomerZeroSettlement()
        {
            ShopModel model = new ShopModel(); int completed = 0;
            model.SaleCompleted += result => { completed++; Equal(0L, result.NetProfit); True(model.IsSaleSettled); };
            model.OpenDayWithCustomer(1); Equal(100000L, model.GetMoney()); False(model.IsSaleSettled); Equal(0, completed);
            model.PresentCustomer(); string error; True(model.TryDeclineRecommendation(out error));
            Equal(100000L, model.GetMoney()); False(model.IsSaleSettled); Equal(0, completed);
            model.FinishCustomerFeedback(); Equal(100000L, model.GetMoney()); True(model.IsSaleSettled); Equal(1, completed);
            Throws<InvalidOperationException>(() => model.FinishCustomerFeedback()); Equal(1, completed);
            model.NextDay(); False(model.IsSaleSettled); Equal(100000L, model.GetMoney());
        }

        private static void CustomerSettlementEvents()
        {
            ShopModel model = new ShopModel(); int completed = 0; bool changedAtResult = false;
            model.SaleCompleted += result =>
            {
                completed++; Equal(CustomerStep.Result, model.Customer.Step); True(model.IsSaleSettled);
                Equal(100000L + result.NetProfit, model.GetMoney()); True(ReferenceEquals(result, model.LastSale));
            };
            model.Changed += () =>
            {
                if (model.Customer != null && model.Customer.Step == CustomerStep.Result)
                { changedAtResult = true; True(model.IsSaleSettled); Equal(100000L + model.LastSale.NetProfit, model.GetMoney()); }
            };
            Set(model, 4, 12); model.OpenDayWithCustomer(2); Equal(0, completed);
            model.PresentCustomer(); model.SelectCustomerTaste(RiceCakeTaste.Sweet); string error;
            True(model.TryRecommendProduct(4, out error)); Equal(0, completed); False(changedAtResult);
            model.FinishCustomerFeedback(); Equal(1, completed); True(changedAtResult);
            model = new ShopModel(); completed = 0; model.SaleCompleted += result => { completed++; True(model.IsSaleSettled); };
            Set(model, 0, 30); model.ConfirmProduction(); model.StartSelling(); Equal(1, completed); True(model.IsSaleSettled);
            True(model.GetMoney() > 100000L);
        }

        private static ShopModel RestoreJson(ShopModel source)
        {
            ShopModel restored = new ShopModel();
            restored.RestoreSnapshot(JsonUtility.FromJson<ShopSnapshot>(JsonUtility.ToJson(source.CreateSnapshot())));
            return restored;
        }

        private static void CustomerSettlementRoundtrip()
        {
            ShopModel model = new ShopModel(); Set(model, 0, 30); model.OpenDayWithCustomer(1); AssertRoundtrip(model);
            model = RestoreJson(model); Equal(100000L, model.GetMoney()); False(model.IsSaleSettled);
            model.PresentCustomer(); model = RestoreJson(model); Equal(100000L, model.GetMoney()); False(model.IsSaleSettled);
            model.SelectCustomerTaste(RiceCakeTaste.Mild); string error; True(model.TryRecommendProduct(0, out error));
            model = RestoreJson(model); Equal(CustomerStep.Feedback, model.Customer.Step); Equal(100000L, model.GetMoney());
            False(model.IsSaleSettled); int completed = 0; model.SaleCompleted += result => completed++;
            long expected = model.GetMoney() + model.LastSale.NetProfit; model.FinishCustomerFeedback(); Equal(expected, model.GetMoney()); Equal(1, completed);
            model = RestoreJson(model); True(model.IsSaleSettled); Equal(expected, model.GetMoney());
            Throws<InvalidOperationException>(() => model.FinishCustomerFeedback()); Equal(expected, model.GetMoney());
            model.BeginEvening(); model = RestoreJson(model); Equal(expected, model.GetMoney());
            model.ChooseResearch(true); model.ChooseResearchKind(ResearchKind.Cake); True(model.TryScheduleCakeResearch(5, out error));
            model = RestoreJson(model); Equal(expected - 60000L, model.GetMoney()); True(model.IsSaleSettled);
            model.NextDay(); Equal(expected - 60000L, model.GetMoney()); False(model.IsSaleSettled); AssertRoundtrip(model);
        }

        private static void CustomerSettlementMigration()
        {
            foreach (CustomerStep step in new[] { CustomerStep.Opening, CustomerStep.AwaitingRecommendation, CustomerStep.Feedback, CustomerStep.Result })
            {
                ShopModel old = new ShopModel(); Set(old, 0, 30); old.OpenDayWithCustomer(1);
                if (step != CustomerStep.Opening) old.PresentCustomer();
                string error;
                if (step == CustomerStep.Feedback || step == CustomerStep.Result)
                { old.SelectCustomerTaste(RiceCakeTaste.Mild); True(old.TryRecommendProduct(0, out error)); }
                if (step == CustomerStep.Result) old.FinishCustomerFeedback();
                ShopSnapshot legacy = old.CreateSnapshot(); legacy.Version = 3; legacy.SaleSettled = false;
                long alreadyPaid = 100000L + old.LastSale.NetProfit;
                legacy.Money = alreadyPaid; ShopModel migrated = new ShopModel();
                migrated.RestoreSnapshot(JsonUtility.FromJson<ShopSnapshot>(JsonUtility.ToJson(legacy)));
                Equal(5, migrated.CreateSnapshot().Version); True(migrated.IsSaleSettled); Equal(alreadyPaid, migrated.GetMoney());
                migrated = RestoreJson(migrated); True(migrated.IsSaleSettled); Equal(alreadyPaid, migrated.GetMoney());
                int completed = 0; migrated.SaleCompleted += result => completed++;
                if (step == CustomerStep.Opening) migrated.PresentCustomer();
                if (step == CustomerStep.Opening || step == CustomerStep.AwaitingRecommendation)
                { migrated.SelectCustomerTaste(RiceCakeTaste.Mild); True(migrated.TryRecommendProduct(0, out error)); }
                if (step != CustomerStep.Result) migrated.FinishCustomerFeedback();
                Equal(alreadyPaid, migrated.GetMoney()); Equal(0, completed); True(migrated.IsSaleSettled);
                migrated.NextDay(); Equal(alreadyPaid, migrated.GetMoney()); False(migrated.IsSaleSettled);
            }
            for (int version = 1; version <= 3; version++)
            {
                ShopModel direct = new ShopModel(); Set(direct, 0, 30); direct.ConfirmProduction(); direct.Sell(.75);
                ShopSnapshot legacy = direct.CreateSnapshot(); legacy.Version = version; legacy.SaleSettled = false;
                ShopModel migrated = new ShopModel(); migrated.RestoreSnapshot(legacy);
                True(migrated.IsSaleSettled); Equal(direct.GetMoney(), migrated.GetMoney());
                migrated.NextDay(); False(migrated.IsSaleSettled); Equal(direct.GetMoney(), migrated.GetMoney());
            }
        }

        private static void CustomerSettlementValidation()
        {
            ShopModel model = new ShopModel(); ShopSnapshot invalid = model.CreateSnapshot(); invalid.SaleSettled = true;
            string error; False(model.TryRestoreSnapshot(invalid, out error)); Equal(100000L, model.GetMoney()); False(model.IsSaleSettled);
            model.ConfirmProduction(); invalid = model.CreateSnapshot(); invalid.SaleSettled = true;
            False(model.TryRestoreSnapshot(invalid, out error)); Equal(GamePhase.ReadyToSell, model.GetPhase());
            model.Sell(.75); invalid = model.CreateSnapshot(); invalid.SaleSettled = false;
            string before = JsonUtility.ToJson(model.CreateSnapshot()); False(model.TryRestoreSnapshot(invalid, out error));
            Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
            model = new ShopModel(); model.OpenDayWithCustomer(1); model.PresentCustomer(); True(model.TryDeclineRecommendation(out error));
            model.FinishCustomerFeedback(); invalid = model.CreateSnapshot(); invalid.SaleSettled = false;
            before = JsonUtility.ToJson(model.CreateSnapshot()); False(model.TryRestoreSnapshot(invalid, out error));
            Equal(before, JsonUtility.ToJson(model.CreateSnapshot()));
        }

        private static void Set(ShopModel model, int index, int count)
        {
            string error; if (!model.TrySetCount(index, count, out error)) throw new Exception(error);
        }
        private static void Counts(IReadOnlyList<int> actual, params int[] expected)
        {
            Equal(expected.Length, actual.Count); for (int i = 0; i < expected.Length; i++) Equal(expected[i], actual[i]);
        }
        private static void Equal<T>(T expected, T actual)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new Exception("Expected " + expected + ", got " + actual);
        }
        private static void True(bool condition) { if (!condition) throw new Exception("Expected true."); }
        private static void False(bool condition) { if (condition) throw new Exception("Expected false."); }
        private static void Throws<T>(Action action) where T : Exception
        {
            try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name);
        }
    }
}
