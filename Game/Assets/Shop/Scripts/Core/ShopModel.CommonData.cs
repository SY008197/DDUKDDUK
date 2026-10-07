using System;
using System.Collections.Generic;

namespace DdukDduk.Shop
{
    /// <summary>The common data API specified by the team's shared API draft.</summary>
    public sealed partial class ShopModel
    {
        private int day;
        private GamePhase phase;
        private int money;
        private HashSet<string> completedResearchIds = new HashSet<string>(StringComparer.Ordinal);
        private HashSet<string> completedEventIds = new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, int> usedThisWeek = new Dictionary<string, int>(StringComparer.Ordinal);
        private HashSet<string> firstUsed = new HashSet<string>(StringComparer.Ordinal);
        private Dictionary<string, int> applyDay = new Dictionary<string, int>(StringComparer.Ordinal);
        private Dictionary<CharacterId, int> affection = new Dictionary<CharacterId, int>();
        private Route currentRoute;

        public int GetDay() { return day; }
        public GamePhase GetPhase() { return phase; }

        public void ChangePhase(GamePhase phase)
        {
            if (!Enum.IsDefined(typeof(GamePhase), phase)) throw new ArgumentOutOfRangeException("phase");
            if (this.phase == phase) return;

            // A phase transition also keeps the production, settlement and day data coherent.
            // These existing domain methods validate the transition before changing state.
            if (this.phase == GamePhase.Arrangement && phase == GamePhase.ReadyToSell) ConfirmProduction();
            else if (this.phase == GamePhase.ReadyToSell && phase == GamePhase.Closed) StartSelling();
            else if (this.phase == GamePhase.Closed && phase == GamePhase.Arrangement) NextDay();
            else throw new InvalidOperationException("현재 진행 단계에서 요청한 단계로 변경할 수 없습니다.");
        }

        public int GetMoney() { return money; }

        public void AddMoney(int amount)
        {
            int nextMoney = checked(money + amount);
            if (nextMoney == money) return;
            money = nextMoney;
            NotifyChanged();
        }

        public bool TrySpendMoney(int amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException("amount", "사용할 금액은 0 이상이어야 합니다.");
            if (money < amount) return false;
            if (amount == 0) return true;
            money -= amount;
            NotifyChanged();
            return true;
        }

        public int GetReputation() { return reputation; }

        public void AddReputation(int amount)
        {
            int nextReputation = checked(reputation + amount);
            if (nextReputation < 0) throw new ArgumentOutOfRangeException("amount", "평판은 0 이상이어야 합니다.");
            if (nextReputation == reputation) return;
            reputation = nextReputation;
            NotifyChanged();
        }

        public bool IsResearchCompleted(string researchId)
        {
            ValidateCommonId(researchId, "researchId");
            return completedResearchIds.Contains(researchId);
        }

        public void CompleteResearch(string researchId) { RegisterCompletedResearch(researchId, true); }

        // Domain operations call this without notifying until all overnight state has been applied.
        internal void RegisterCompletedResearch(string researchId, bool notify)
        {
            ValidateCommonId(researchId, "researchId");
            if (completedResearchIds.Contains(researchId)) return;

            int cakeIndex = ResearchCakeIndex(researchId);
            int standCapacity = ResearchStandCapacity(researchId);
            if (notify && PendingResearch != null
                && ((PendingResearch.Kind == ResearchKind.Cake && cakeIndex == PendingResearch.ProductIndex)
                    || (PendingResearch.Kind == ResearchKind.Stand && standCapacity == PendingResearch.TargetCapacity)))
                throw new InvalidOperationException("예약한 연구는 다음 영업일에 완료됩니다.");
            if (cakeIndex >= 5)
            {
                for (int previous = 5; previous < cakeIndex; previous++)
                    if (!unlocked[previous])
                        throw new InvalidOperationException(Catalog[previous].Name + " 연구를 먼저 완료해야 합니다.");
                unlocked[cakeIndex] = true;
            }
            else if (standCapacity != 0)
            {
                if (standCapacity > Capacity && standCapacity != Capacity + 10)
                    throw new InvalidOperationException("가판대 연구는 이전 단계를 완료한 뒤 진행해야 합니다.");
                if (standCapacity > Capacity) Capacity = standCapacity;
            }

            completedResearchIds.Add(researchId);
            if (notify) NotifyChanged();
        }

        internal static string GetStandResearchId(int targetCapacity)
        {
            switch (targetCapacity)
            {
                case 40: return "stand_40";
                case 50: return "stand_50";
                case 60: return "stand_60";
                case 70: return "stand_70";
                default: throw new ArgumentOutOfRangeException("targetCapacity", "가판대 연구 한도는 40, 50, 60, 70팩입니다.");
            }
        }

        public bool IsMarketingUsedThisWeek(string marketingId)
        {
            ValidateCommonId(marketingId, "marketingId");
            int usedWeek;
            return usedThisWeek.TryGetValue(marketingId, out usedWeek) && usedWeek == (day - 1) / 7;
        }

        // firstUsed records whether this marketing ID has ever been used.
        public bool IsMarketingFirstUsed(string marketingId)
        {
            ValidateCommonId(marketingId, "marketingId");
            return firstUsed.Contains(marketingId);
        }

        public void UseMarketing(string marketingId)
        {
            ValidateCommonId(marketingId, "marketingId");
            if (IsMarketingUsedThisWeek(marketingId)) return;
            int nextApplyDay = checked(day + 1);
            usedThisWeek[marketingId] = (day - 1) / 7;
            firstUsed.Add(marketingId);
            applyDay[marketingId] = nextApplyDay;
            NotifyChanged();
        }

        public bool IsMarketingActive(string marketingId)
        {
            ValidateCommonId(marketingId, "marketingId");
            int effectDay;
            return applyDay.TryGetValue(marketingId, out effectDay) && effectDay == day;
        }

        public int GetAffection(CharacterId id)
        {
            ValidateCharacterId(id);
            int value;
            return affection.TryGetValue(id, out value) ? value : 0;
        }

        public void AddAffection(CharacterId id, int amount)
        {
            int previous = GetAffection(id);
            int nextAffection = checked(previous + amount);
            if (nextAffection == previous) return;
            affection[id] = nextAffection;
            NotifyChanged();
        }

        public Route GetCurrentRoute() { return currentRoute; }

        private static void ValidateCommonId(string id, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("ID는 비어 있을 수 없습니다.", parameterName);
        }

        private static void ValidateCharacterId(CharacterId id)
        {
            if ((int)id < 0) throw new ArgumentOutOfRangeException("id", "캐릭터 ID는 0 이상이어야 합니다.");
        }

        private static int ResearchCakeIndex(string researchId)
        {
            for (int index = 5; index < Catalog.Count; index++)
                if (string.Equals(Catalog[index].Id, researchId, StringComparison.Ordinal)) return index;
            return -1;
        }

        private static int ResearchStandCapacity(string researchId)
        {
            switch (researchId)
            {
                case "stand_40": return 40;
                case "stand_50": return 50;
                case "stand_60": return 60;
                case "stand_70": return 70;
                default: return 0;
            }
        }
    }
}
