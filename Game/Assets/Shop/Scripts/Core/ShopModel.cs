using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DdukDduk.Shop
{
    public enum RiceCakeTaste { Unspecified = 0, Mild = 1, Sweet = 2, Nutty = 3 }
    public enum CustomerStep { None = 0, Opening = 1, AwaitingRecommendation = 2, Feedback = 3, Result = 4 }
    public enum EveningStep { None = 0, Prompt = 1, Choice = 2, CakeResearch = 3, StandResearch = 4, RestConfirmation = 5, MorningNotice = 6 }
    public enum ResearchKind { None = 0, Cake = 1, Stand = 2 }

    public sealed class PendingResearchWork
    {
        public ResearchKind Kind { get; private set; }
        public int ProductIndex { get; private set; }
        public int RequestedDay { get; private set; }
        public long Cost { get; private set; }
        public int TargetCapacity { get; private set; }
        internal PendingResearchWork(ResearchKind kind, int productIndex, int day, long cost, int targetCapacity)
        { Kind = kind; ProductIndex = productIndex; RequestedDay = day; Cost = cost; TargetCapacity = targetCapacity; }
    }

    public sealed class ProductDefinition
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Description { get; private set; }
        public int SellPrice { get; private set; }
        public int Cost { get; private set; }
        public RiceCakeTaste Taste { get; private set; }

        internal ProductDefinition(string id, string name, string description, int sellPrice, int cost, RiceCakeTaste taste)
        {
            Id = id; Name = name; Description = description; SellPrice = sellPrice; Cost = cost; Taste = taste;
        }
    }

    public sealed class CustomerVisit
    {
        public int Day { get; private set; }
        public RiceCakeTaste Answer { get; private set; }
        public RiceCakeTaste SelectedTaste { get; private set; }
        public int RecommendedProductIndex { get; private set; }
        public string Dialogue { get; private set; }
        public bool GeneratedWithAi { get; private set; }
        public CustomerStep Step { get; private set; }
        public bool IsResolved { get { return Step == CustomerStep.Feedback || Step == CustomerStep.Result; } }
        public bool WasSatisfied { get; private set; }
        public int ReputationBefore { get; private set; }

        internal CustomerVisit(int day, RiceCakeTaste answer, int reputationBefore)
        {
            Day = day; Answer = answer; ReputationBefore = reputationBefore;
            RecommendedProductIndex = -1; Dialogue = string.Empty; Step = CustomerStep.Opening;
        }

        internal void SetDialogue(string text, bool generatedWithAi) { Dialogue = text; GeneratedWithAi = generatedWithAi; }
        internal void SetStep(CustomerStep step) { Step = step; }
        internal void SelectTaste(RiceCakeTaste taste) { SelectedTaste = taste; }
        internal void Resolve(int productIndex)
        {
            RecommendedProductIndex = productIndex; WasSatisfied = SelectedTaste == Answer; Step = CustomerStep.Feedback;
        }
        internal void Decline()
        {
            RecommendedProductIndex = -1; WasSatisfied = false; Step = CustomerStep.Feedback;
        }

        internal static CustomerVisit FromSnapshot(CustomerSnapshot snapshot)
        {
            CustomerVisit visit = new CustomerVisit(snapshot.Day, snapshot.Answer, snapshot.ReputationBefore);
            visit.SelectedTaste = snapshot.SelectedTaste; visit.RecommendedProductIndex = snapshot.RecommendedProductIndex;
            visit.Dialogue = snapshot.Dialogue ?? string.Empty; visit.GeneratedWithAi = snapshot.GeneratedWithAi;
            visit.Step = snapshot.Step; visit.WasSatisfied = snapshot.WasSatisfied;
            return visit;
        }
    }

    public struct SalesRateBand
    {
        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public SalesRateBand(double minimum, double maximum) { Minimum = minimum; Maximum = maximum; }
    }

    public sealed class ProductionReceipt
    {
        public int Day { get; private set; }
        public int TotalProduced { get; private set; }
        public long ProductionCost { get; private set; }
        public IReadOnlyList<int> ProducedCounts { get; private set; }

        internal ProductionReceipt(int day, int[] produced, long cost)
        {
            Day = day; ProductionCost = cost;
            ProducedCounts = Array.AsReadOnly((int[])produced.Clone());
            foreach (int count in produced) TotalProduced += count;
        }
    }

    public sealed class SaleResult
    {
        public int Day { get; private set; }
        public int Reputation { get; private set; }
        public double Rate { get; private set; }
        public int TotalProduced { get; private set; }
        public int TotalSold { get; private set; }
        public int TotalUnsold { get { return TotalProduced - TotalSold; } }
        public long Revenue { get; private set; }
        public long ProductionCost { get; private set; }
        public long NetProfit { get { return Revenue - ProductionCost; } }
        public IReadOnlyList<int> ProducedCounts { get; private set; }
        public IReadOnlyList<int> SoldCounts { get; private set; }
        public IReadOnlyList<int> UnsoldCounts { get; private set; }

        internal SaleResult(int day, int reputation, double rate, int[] produced, int[] sold, long cost, long revenue)
        {
            Day = day; Reputation = reputation; Rate = rate; ProductionCost = cost; Revenue = revenue;
            int[] unsold = new int[produced.Length];
            for (int i = 0; i < produced.Length; i++)
            {
                TotalProduced += produced[i]; TotalSold += sold[i]; unsold[i] = produced[i] - sold[i];
            }
            ProducedCounts = Array.AsReadOnly((int[])produced.Clone());
            SoldCounts = Array.AsReadOnly((int[])sold.Clone());
            UnsoldCounts = Array.AsReadOnly(unsold);
        }
    }

    // Public fields keep saves compatible with Unity's JsonUtility without a Unity dependency in this model.
    [Serializable]
    public sealed class ShopSnapshot
    {
        public int Version = 5;
        public int Day;
        public long Money;
        public int Reputation;
        public int Capacity;
        public GamePhase Phase;
        public int[] SelectedCounts;
        public bool[] UnlockedProducts;
        public int[] ProducedCounts;
        public long ProductionCost;
        // Unity serializes custom classes inline and can recreate a null field as an empty instance.
        // Presence is explicit so an empty placeholder can never become a completed sale.
        public bool HasLastSale;
        public SaleSnapshot LastSale;
        public bool SaleSettled;
        public bool HasCustomer;
        public CustomerSnapshot Customer;
        public EveningStep Evening;
        public bool HasPendingResearch;
        public PendingResearchSnapshot PendingResearch;
        public string MorningMessage;
        public CommonDataSnapshot CommonData;
    }

    [Serializable]
    public sealed class PendingResearchSnapshot
    {
        public ResearchKind Kind;
        public int ProductIndex;
        public int RequestedDay;
        public long Cost;
        public int TargetCapacity;
    }

    [Serializable]
    public sealed class CustomerSnapshot
    {
        public int Day;
        public RiceCakeTaste Answer;
        public RiceCakeTaste SelectedTaste;
        public int RecommendedProductIndex;
        public string Dialogue;
        public bool GeneratedWithAi;
        public CustomerStep Step;
        public bool WasSatisfied;
        public int ReputationBefore;
    }

    [Serializable]
    public sealed class SaleSnapshot
    {
        public int Day;
        public int Reputation;
        public double Rate;
        public int[] ProducedCounts;
        public int[] SoldCounts;
        public long ProductionCost;
        public long Revenue;
    }

    /// <summary>Pure shop state, including paid evening work and its application on the following morning.</summary>
    public sealed partial class ShopModel
    {
        public const string CakeResearchMorningMessage = "배합을 바꿔가며 밤새 떡을 만들었따. 마침내 원하는 맛을 찾아냈다";
        public const string StandResearchMorningMessage = "영업을 마친 뒤 판매대를 손봤다. 날이 밝을 무렵, 떡을 놓을 자리가 넓어졌다.";
        private static readonly long[] CakeResearchCosts = { 60000, 180000, 360000, 720000 };
        public static readonly IReadOnlyList<ProductDefinition> Catalog = Array.AsReadOnly(new[]
        {
            new ProductDefinition("garaetteok", "가래떡", "쫀득하고 담백한 기본 떡", 2500, 1000, RiceCakeTaste.Mild),
            new ProductDefinition("jeolpyeon", "절편", "고운 무늬를 담은 쫄깃한 떡", 2500, 1000, RiceCakeTaste.Mild),
            new ProductDefinition("baekseolgi", "백설기", "포근하고 부드러운 흰 떡", 3000, 1200, RiceCakeTaste.Mild),
            new ProductDefinition("injeolmi", "인절미", "고소한 콩고물을 입힌 찹쌀떡", 3500, 1400, RiceCakeTaste.Nutty),
            new ProductDefinition("baram", "바람떡", "달콤한 소를 품은 반달 떡", 3500, 1400, RiceCakeTaste.Sweet),
            new ProductDefinition("mugwort", "쑥떡쑥떡", "향긋한 쑥 향이 가득한 떡", 4500, 1800, RiceCakeTaste.Mild),
            new ProductDefinition("honey", "꿀떡꿀떡", "한입에 퍼지는 달콤한 꿀 떡", 6000, 2400, RiceCakeTaste.Sweet),
            new ProductDefinition("bean", "콩떡콩떡", "고소한 콩을 넉넉히 담은 떡", 8000, 3200, RiceCakeTaste.Nutty),
            new ProductDefinition("royal", "궁떡궁떡", "정성으로 빚은 특별한 궁중 떡", 12000, 4800, RiceCakeTaste.Unspecified)
        });

        private int[] selected = new int[Catalog.Count];
        private int[] produced = new int[Catalog.Count];
        private bool[] unlocked = new bool[Catalog.Count];
        private int reputation;
        private readonly Random random = new Random();
        private bool restConfirmed;

        public event Action Changed;
        public event Action<ProductionReceipt> ProductionConfirmed;
        public event Action<SaleResult> SaleCompleted;

        public IReadOnlyList<ProductDefinition> Products { get { return Catalog; } }
        public int Capacity { get; private set; }
        public IReadOnlyList<int> SelectedCounts { get { return Array.AsReadOnly(selected); } }
        public IReadOnlyList<int> ProducedCounts { get { return Array.AsReadOnly(produced); } }
        public IReadOnlyList<bool> UnlockedProducts { get { return Array.AsReadOnly(unlocked); } }
        public int TotalSelected { get { return Sum(selected); } }
        public int TotalProduced { get { return Sum(produced); } }
        public long EstimatedProductionCost { get { return CalculateCost(selected); } }
        public long CommittedProductionCost { get; private set; }
        public SaleResult LastSale { get; private set; }
        public SaleResult Result { get { return LastSale; } }
        public bool IsSaleSettled { get; private set; }
        public CustomerVisit Customer { get; private set; }
        public bool HasRecommendableProduct { get { return HasRecommendableProductIn(produced, unlocked); } }
        public EveningStep Evening { get; private set; }
        public PendingResearchWork PendingResearch { get; private set; }
        public string MorningMessage { get; private set; }

        public ShopModel()
        {
            day = 1; money = 100000; Capacity = 30; phase = GamePhase.Arrangement; MorningMessage = string.Empty;
            for (int i = 0; i < 5; i++) unlocked[i] = true;
        }

        public int SelectedQuantity(string id) { return selected[ProductIndex(id)]; }
        public bool IsUnlocked(string id) { return unlocked[ProductIndex(id)]; }

        public static int ProductIndex(string id)
        {
            for (int i = 0; i < Catalog.Count; i++) if (Catalog[i].Id == id) return i;
            throw new ArgumentException("알 수 없는 떡 종류입니다.", "id");
        }

        public bool TrySetCount(string id, int count, out string error)
        {
            int index;
            try { index = ProductIndex(id); }
            catch (ArgumentException exception) { error = exception.Message; return false; }
            return TrySetCount(index, count, out error);
        }

        public bool TrySetCount(int index, int count, out string error)
        {
            if (phase != GamePhase.Arrangement) { error = "생산을 확정한 뒤에는 수량을 바꿀 수 없습니다."; return false; }
            if (Evening != EveningStep.None) { error = "아침 연구 완료 안내를 확인한 뒤 떡을 배치해 주세요."; return false; }
            if (index < 0 || index >= Catalog.Count) { error = "알 수 없는 떡 종류입니다."; return false; }
            if (count < 0) { error = "수량은 0 이상이어야 합니다."; return false; }
            if (!unlocked[index] && count != 0) { error = "연구로 해금한 뒤 만들 수 있습니다."; return false; }
            if ((long)TotalSelected - selected[index] + count > Capacity) { error = "진열대의 전체 수량을 초과했습니다."; return false; }
            error = null;
            if (selected[index] != count) { selected[index] = count; NotifyChanged(); }
            return true;
        }

        public bool TryAdjustQuantity(string id, int delta, out string error)
        {
            int index;
            try { index = ProductIndex(id); }
            catch (ArgumentException exception) { error = exception.Message; return false; }
            long next = (long)selected[index] + delta;
            if (next < 0 || next > int.MaxValue) { error = "수량 범위를 초과했습니다."; return false; }
            return TrySetCount(index, (int)next, out error);
        }

        public void ClearSelection()
        {
            RequireProductionArrangement();
            Array.Clear(selected, 0, selected.Length); NotifyChanged();
        }

        public void DistributeToCapacity()
        {
            RequireProductionArrangement();
            int openCount = 0;
            foreach (bool open in unlocked) if (open) openCount++;
            int each = Capacity / openCount, remainder = Capacity % openCount;
            for (int i = 0; i < selected.Length; i++) selected[i] = unlocked[i] ? each + (remainder-- > 0 ? 1 : 0) : 0;
            NotifyChanged();
        }
        public void AutoDistribute() { DistributeToCapacity(); }

        // Kept for integrations using the former entry point; it now follows the same paid, overnight rules.
        public void UpgradeCapacity()
        {
            string error;
            if (!TryScheduleStandResearch(out error)) throw new InvalidOperationException(error);
        }

        // Explicit external/editor integration helper. Normal play uses the paid research scheduling methods.
        public void UnlockProduct(string id) { UnlockProduct(ProductIndex(id)); }
        public void UnlockProduct(int index)
        {
            if (index < 0 || index >= Catalog.Count) throw new ArgumentOutOfRangeException("index");
            if (!unlocked[index])
            {
                unlocked[index] = true;
                if (index >= 5) completedResearchIds.Add(Catalog[index].Id);
                NotifyChanged();
            }
        }

        public ProductionReceipt ConfirmProduction()
        {
            RequireProductionArrangement();
            int[] nextProduced = (int[])selected.Clone();
            long cost = CalculateCost(nextProduced);
            ProductionReceipt receipt = new ProductionReceipt(day, nextProduced, cost);
            produced = nextProduced; CommittedProductionCost = cost; phase = GamePhase.ReadyToSell;
            NotifyChanged();
            if (ProductionConfirmed != null) ProductionConfirmed(receipt);
            return receipt;
        }

        public bool TryConfirmProduction(out string error)
        {
            if (phase != GamePhase.Arrangement) { error = "이미 생산을 확정했습니다."; return false; }
            if (Evening != EveningStep.None) { error = "아침 연구 완료 안내를 확인한 뒤 떡을 배치해 주세요."; return false; }
            ConfirmProduction(); error = null; return true;
        }

        public static SalesRateBand GetSalesBand(int reputation)
        {
            if (reputation < 0) throw new ArgumentOutOfRangeException("reputation");
            if (reputation < 15) return new SalesRateBand(0.75, 0.80);
            if (reputation < 35) return new SalesRateBand(0.85, 0.95);
            return new SalesRateBand(1.0, 1.0);
        }

        // A caller can supply the day's sampled rate for reproducible saves/tests. Only the reputation band is allowed.
        public SaleResult Sell(double rate)
        {
            RequirePhase(GamePhase.ReadyToSell);
            ValidateRate(reputation, rate);
            int[] sold = AllocateSales(produced, rate);
            long revenue = CalculateRevenue(sold);
            // Normal play calculates the sale at opening, then posts its money when the result is shown.
            // Direct integrations without a customer keep their immediate settlement behavior.
            bool settleNow = Customer == null;
            int nextMoney = settleNow ? checked((int)(money + checked(revenue - CommittedProductionCost))) : money;
            SaleResult result = new SaleResult(day, reputation, rate, produced, sold, CommittedProductionCost, revenue);
            money = nextMoney; LastSale = result; IsSaleSettled = settleNow; phase = GamePhase.Closed;
            NotifyChanged();
            if (settleNow && SaleCompleted != null) SaleCompleted(result);
            return result;
        }

        public SaleResult StartSelling()
        {
            SalesRateBand band = GetSalesBand(reputation);
            return Sell(band.Minimum + random.NextDouble() * (band.Maximum - band.Minimum));
        }

        public CustomerVisit OpenDayWithCustomer(int forcedAnswer = 0)
        {
            if (Evening != EveningStep.None) throw new InvalidOperationException("아침 연구 완료 안내를 확인한 뒤 영업을 시작해 주세요.");
            if (phase != GamePhase.Arrangement && phase != GamePhase.ReadyToSell)
                throw new InvalidOperationException("이미 오늘 판매를 시작했습니다.");
            if (Customer != null) throw new InvalidOperationException("오늘 손님은 이미 방문했습니다.");
            if (forcedAnswer < 0 || forcedAnswer > 3) throw new ArgumentOutOfRangeException("forcedAnswer");
            RiceCakeTaste answer = (RiceCakeTaste)(forcedAnswer == 0 ? random.Next(1, 4) : forcedAnswer);
            if (phase == GamePhase.Arrangement) ConfirmProduction();
            Customer = new CustomerVisit(day, answer, reputation);
            // Today's sales use the reputation at opening. The recommendation reward affects the following day.
            try { StartSelling(); }
            catch { Customer = null; throw; }
            return Customer;
        }

        public void SetCustomerDialogue(string text, bool generatedWithAi)
        {
            RequireUnresolvedCustomer();
            // The generated text is deliberately displayed as received, even if it is unusual.
            Customer.SetDialogue(text ?? string.Empty, generatedWithAi); NotifyChanged();
        }

        public void PresentCustomer()
        {
            RequireCustomerStep(CustomerStep.Opening);
            Customer.SetStep(CustomerStep.AwaitingRecommendation); NotifyChanged();
        }

        public void SelectCustomerTaste(RiceCakeTaste taste)
        {
            RequireCustomerStep(CustomerStep.AwaitingRecommendation);
            if (!IsKnownTaste(taste)) throw new ArgumentOutOfRangeException("taste");
            if (Customer.SelectedTaste != taste) { Customer.SelectTaste(taste); NotifyChanged(); }
        }

        public bool TryRecommendProduct(int index, out string error)
        {
            if (Customer == null || Customer.Step != CustomerStep.AwaitingRecommendation)
            { error = "현재 손님에게 추천할 수 없습니다."; return false; }
            if (!IsKnownTaste(Customer.SelectedTaste)) { error = "먼저 떡의 맛을 선택해 주세요."; return false; }
            if (index < 0 || index >= Catalog.Count) { error = "알 수 없는 떡 종류입니다."; return false; }
            if (!unlocked[index]) { error = Catalog[index].Name + "은 연구가 필요합니다."; return false; }
            if (produced[index] == 0) { error = "오늘 배치한 떡만 추천할 수 있습니다."; return false; }
            if (Catalog[index].Taste != Customer.SelectedTaste)
            { error = "선택한 맛에 해당하는 떡을 추천해 주세요."; return false; }
            // Recommendation uses the confirmed display, regardless of how many units the sale calculation leaves unsold.
            int nextReputation = Customer.SelectedTaste == Customer.Answer ? checked(reputation + 1) : reputation;
            Customer.Resolve(index); reputation = nextReputation; error = null; NotifyChanged(); return true;
        }

        public bool TryDeclineRecommendation(out string error)
        {
            if (Customer == null || Customer.Step != CustomerStep.AwaitingRecommendation)
            { error = "현재 손님을 돌려보낼 수 없습니다."; return false; }
            if (HasRecommendableProduct) { error = "오늘 배치한 떡을 추천해 주세요."; return false; }
            Customer.Decline(); error = null; NotifyChanged(); return true;
        }

        public void FinishCustomerFeedback()
        {
            RequireCustomerStep(CustomerStep.Feedback);
            bool settling = !IsSaleSettled;
            int nextMoney = settling ? checked((int)(money + LastSale.NetProfit)) : money;
            money = nextMoney; IsSaleSettled = true; Customer.SetStep(CustomerStep.Result); NotifyChanged();
            if (settling && SaleCompleted != null) SaleCompleted(LastSale);
        }

        public void BeginEvening()
        {
            RequireCompletedDay();
            if (Evening != EveningStep.None) throw new InvalidOperationException("이미 저녁 행동을 선택하고 있습니다.");
            Evening = EveningStep.Prompt; NotifyChanged();
        }

        public void ChooseResearch(bool wantsResearch)
        {
            RequireEveningStep(EveningStep.Prompt);
            Evening = wantsResearch ? EveningStep.Choice : EveningStep.RestConfirmation; NotifyChanged();
        }

        public void ChooseResearchKind(ResearchKind kind)
        {
            RequireEveningStep(EveningStep.Choice);
            if (kind != ResearchKind.Cake && kind != ResearchKind.Stand) throw new ArgumentOutOfRangeException("kind");
            Evening = kind == ResearchKind.Cake ? EveningStep.CakeResearch : EveningStep.StandResearch; NotifyChanged();
        }

        public void BackFromEvening()
        {
            RequireCompletedDay();
            if (PendingResearch != null) throw new InvalidOperationException("확정한 연구는 이미 시작되었습니다.");
            if (Evening == EveningStep.CakeResearch || Evening == EveningStep.StandResearch) Evening = EveningStep.Choice;
            else if (Evening == EveningStep.Choice || Evening == EveningStep.RestConfirmation) Evening = EveningStep.Prompt;
            else throw new InvalidOperationException("현재 화면에서 뒤로 갈 수 없습니다.");
            NotifyChanged();
        }

        public void ConfirmRest()
        {
            RequireEveningStep(EveningStep.RestConfirmation);
            restConfirmed = true;
            try { NextDay(); }
            finally { restConfirmed = false; }
        }

        public long GetCakeResearchCost(int index)
        {
            if (index < 5 || index >= Catalog.Count) throw new ArgumentOutOfRangeException("index");
            return CakeResearchCosts[index - 5];
        }

        public long GetStandResearchCost() { return Capacity < 50 ? 150000L : 400000L; }
        public int GetStandResearchTargetCapacity() { return Capacity >= 70 ? Capacity : Capacity + 10; }

        public string GetCakeResearchError(int index)
        {
            if (index < 5 || index >= Catalog.Count) return "연구할 수 없는 떡입니다.";
            if (PendingResearch != null) return "오늘 저녁에는 이미 연구를 시작했습니다.";
            if (money < GetCakeResearchCost(index)) return "연구비가 부족합니다.";
            if (unlocked[index]) return "이미 연구가 완료된 떡입니다.";
            for (int previous = 5; previous < index; previous++)
                if (!unlocked[previous]) return Catalog[previous].Name + " 연구를 먼저 완료해야 합니다.";
            return null;
        }

        public string GetStandResearchError()
        {
            if (PendingResearch != null) return "오늘 저녁에는 이미 연구를 시작했습니다.";
            if (money < GetStandResearchCost()) return "업그레이드 비용이 부족합니다.";
            if (Capacity >= 70) return "가판대는 최대 70팩까지 업그레이드할 수 있습니다.";
            // Reaching 50 packs means that both first-tier upgrades were completed on earlier mornings.
            int requiredReputation = Capacity < 50 ? 15 : 35;
            if (reputation < requiredReputation) return "평판 " + requiredReputation + " 이상이 필요합니다.";
            return null;
        }

        public bool TryScheduleCakeResearch(int productIndex, out string error)
        {
            if (!CanScheduleIn(EveningStep.CakeResearch, out error)) return false;
            error = GetCakeResearchError(productIndex);
            if (error != null) return false;
            long cost = GetCakeResearchCost(productIndex);
            int nextMoney = checked(money - (int)cost);
            PendingResearch = new PendingResearchWork(ResearchKind.Cake, productIndex, day, cost, 0);
            money = nextMoney; NotifyChanged(); return true;
        }

        public bool TryScheduleStandResearch(out string error)
        {
            if (!CanScheduleIn(EveningStep.StandResearch, out error)) return false;
            error = GetStandResearchError();
            if (error != null) return false;
            long cost = GetStandResearchCost();
            int nextMoney = checked(money - (int)cost);
            PendingResearch = new PendingResearchWork(ResearchKind.Stand, -1, day, cost, GetStandResearchTargetCapacity());
            money = nextMoney; NotifyChanged(); return true;
        }

        public void AcknowledgeMorning()
        {
            if (phase != GamePhase.Arrangement || Evening != EveningStep.MorningNotice)
                throw new InvalidOperationException("현재 표시 중인 아침 연구 완료 안내가 없습니다.");
            Evening = EveningStep.None; MorningMessage = string.Empty; NotifyChanged();
        }

        public void NextDay()
        {
            RequireCompletedDay();
            // Direct calls remain compatible with old integrations that never entered the evening flow.
            if (Evening != EveningStep.None && PendingResearch == null && !restConfirmed)
                throw new InvalidOperationException("연구를 시작하거나 휴식을 확인한 뒤 다음 날로 갈 수 있습니다.");
            int nextDay = checked(day + 1);
            string nextMorningMessage = string.Empty;
            if (PendingResearch != null)
            {
                if (PendingResearch.Kind == ResearchKind.Cake)
                { RegisterCompletedResearch(Catalog[PendingResearch.ProductIndex].Id, false); nextMorningMessage = CakeResearchMorningMessage; }
                else
                { RegisterCompletedResearch(GetStandResearchId(PendingResearch.TargetCapacity), false); nextMorningMessage = StandResearchMorningMessage; }
            }
            day = nextDay; Array.Clear(selected, 0, selected.Length); Array.Clear(produced, 0, produced.Length);
            CommittedProductionCost = 0; LastSale = null; IsSaleSettled = false; Customer = null; PendingResearch = null; phase = GamePhase.Arrangement;
            MorningMessage = nextMorningMessage; Evening = string.IsNullOrEmpty(nextMorningMessage) ? EveningStep.None : EveningStep.MorningNotice;
            NotifyChanged();
        }

        private static void ValidateArray(int[] values)
        {
            if (values == null || values.Length != Catalog.Count) throw new ArgumentException("저장된 떡 수량 데이터가 잘못되었습니다.");
            foreach (int count in values) if (count < 0) throw new ArgumentException("저장된 떡 수량은 0 이상이어야 합니다.");
        }

        private static void ValidateRate(int reputation, double rate)
        {
            SalesRateBand band = GetSalesBand(reputation);
            if (double.IsNaN(rate) || double.IsInfinity(rate) || rate < band.Minimum || rate > band.Maximum)
                throw new ArgumentOutOfRangeException("rate", "판매율이 현재 평판 구간을 벗어났습니다.");
        }

        private static int[] AllocateSales(int[] production, double rate)
        {
            int total = Sum(production);
            int[] sold = new int[production.Length];
            if (total == 0) return sold;
            // Round the complete shop once, then distribute that integer total proportionally.
            // Flooring each product separately would silently lose sales in mixed displays.
            int target = (int)Math.Floor(total * rate);
            int assigned = 0;
            long[] remainders = new long[production.Length];
            int[] order = new int[production.Length];
            for (int i = 0; i < production.Length; i++)
            {
                long weighted = (long)production[i] * target;
                sold[i] = (int)(weighted / total); remainders[i] = weighted % total;
                assigned += sold[i]; order[i] = i;
            }
            Array.Sort(order, (left, right) =>
            {
                int comparison = remainders[right].CompareTo(remainders[left]);
                return comparison != 0 ? comparison : left.CompareTo(right);
            });
            for (int i = 0; i < target - assigned; i++) sold[order[i]]++;
            return sold;
        }

        private static int Sum(int[] values)
        {
            int total = 0; foreach (int value in values) total = checked(total + value); return total;
        }
        private static long CalculateCost(int[] values)
        {
            long total = 0; for (int i = 0; i < values.Length; i++) total = checked(total + (long)values[i] * Catalog[i].Cost); return total;
        }
        private static long CalculateRevenue(int[] values)
        {
            long total = 0; for (int i = 0; i < values.Length; i++) total = checked(total + (long)values[i] * Catalog[i].SellPrice); return total;
        }
        private static int[] Copy(IReadOnlyList<int> values)
        {
            int[] copy = new int[values.Count]; for (int i = 0; i < copy.Length; i++) copy[i] = values[i]; return copy;
        }
        private static bool Equal(int[] left, int[] right)
        {
            if (left.Length != right.Length) return false;
            for (int i = 0; i < left.Length; i++) if (left[i] != right[i]) return false;
            return true;
        }
        private void RequirePhase(GamePhase expected)
        {
            if (phase != expected) throw new InvalidOperationException("현재 단계에서 실행할 수 없습니다: " + phase);
        }
        private void RequireProductionArrangement()
        {
            RequirePhase(GamePhase.Arrangement);
            if (Evening != EveningStep.None) throw new InvalidOperationException("아침 연구 완료 안내를 확인한 뒤 떡을 배치해 주세요.");
        }
        private void RequireCompletedDay()
        {
            RequirePhase(GamePhase.Closed);
            if (Customer != null && Customer.Step != CustomerStep.Result)
                throw new InvalidOperationException("손님 추천과 결과 확인을 마친 뒤 저녁 행동을 선택할 수 있습니다.");
            if (!IsSaleSettled) throw new InvalidOperationException("판매 정산을 마친 뒤 저녁 행동을 선택할 수 있습니다.");
        }
        private void RequireEveningStep(EveningStep expected)
        {
            RequireCompletedDay();
            if (Evening != expected || PendingResearch != null)
                throw new InvalidOperationException("현재 저녁 단계에서 실행할 수 없습니다.");
        }
        private bool CanScheduleIn(EveningStep expected, out string error)
        {
            if (phase != GamePhase.Closed || (Customer != null && Customer.Step != CustomerStep.Result)
                || Evening != expected || PendingResearch != null)
            { error = "오늘 판매를 마친 뒤 연구 화면에서 연구를 시작해 주세요."; return false; }
            error = null; return true;
        }
        private static bool IsKnownTaste(RiceCakeTaste taste)
        {
            return taste == RiceCakeTaste.Mild || taste == RiceCakeTaste.Sweet || taste == RiceCakeTaste.Nutty;
        }
        private static bool HasRecommendableProductIn(int[] production, bool[] research)
        {
            for (int i = 0; i < Catalog.Count; i++)
                if (production[i] > 0 && research[i] && IsKnownTaste(Catalog[i].Taste)) return true;
            return false;
        }
        private void RequireCustomerStep(CustomerStep expected)
        {
            if (phase != GamePhase.Closed || Customer == null || Customer.Step != expected)
                throw new InvalidOperationException("현재 손님 단계에서 실행할 수 없습니다.");
        }
        private void RequireUnresolvedCustomer()
        {
            if (phase != GamePhase.Closed || Customer == null || Customer.IsResolved)
                throw new InvalidOperationException("대사를 설정할 수 있는 손님이 없습니다.");
        }
        private void NotifyChanged() { if (Changed != null) Changed(); }
    }
}
