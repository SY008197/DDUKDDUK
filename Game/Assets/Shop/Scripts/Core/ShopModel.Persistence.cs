using System;
using System.Collections.Generic;

namespace DdukDduk.Shop
{
    public sealed partial class ShopModel
    {
        public ShopSnapshot CreateSnapshot()
        {
            return new ShopSnapshot
            {
                Day = day, Money = money, Reputation = reputation, Capacity = Capacity, Phase = phase,
                CommonData = CreateCommonDataSnapshot(),
                SelectedCounts = (int[])selected.Clone(), UnlockedProducts = (bool[])unlocked.Clone(),
                ProducedCounts = (int[])produced.Clone(), ProductionCost = CommittedProductionCost,
                HasLastSale = LastSale != null, SaleSettled = IsSaleSettled,
                LastSale = LastSale == null ? EmptySaleSnapshot() : new SaleSnapshot
                {
                    Day = LastSale.Day, Reputation = LastSale.Reputation, Rate = LastSale.Rate,
                    ProducedCounts = Copy(LastSale.ProducedCounts), SoldCounts = Copy(LastSale.SoldCounts),
                    ProductionCost = LastSale.ProductionCost, Revenue = LastSale.Revenue
                },
                HasCustomer = Customer != null,
                Customer = Customer == null ? EmptyCustomerSnapshot() : new CustomerSnapshot
                {
                    Day = Customer.Day, Answer = Customer.Answer, SelectedTaste = Customer.SelectedTaste,
                    RecommendedProductIndex = Customer.RecommendedProductIndex, Dialogue = Customer.Dialogue,
                    GeneratedWithAi = Customer.GeneratedWithAi, Step = Customer.Step,
                    WasSatisfied = Customer.WasSatisfied, ReputationBefore = Customer.ReputationBefore
                },
                Evening = Evening, MorningMessage = MorningMessage,
                HasPendingResearch = PendingResearch != null,
                PendingResearch = PendingResearch == null ? EmptyPendingResearchSnapshot() : new PendingResearchSnapshot
                {
                    Kind = PendingResearch.Kind, ProductIndex = PendingResearch.ProductIndex, RequestedDay = PendingResearch.RequestedDay,
                    Cost = PendingResearch.Cost, TargetCapacity = PendingResearch.TargetCapacity
                }
            };
        }

        public void RestoreSnapshot(ShopSnapshot snapshot)
        {
            string error;
            if (!TryRestoreSnapshot(snapshot, out error)) throw new ArgumentException(error, "snapshot");
        }

        public bool TryRestoreSnapshot(ShopSnapshot snapshot, out string error)
        {
            SaleResult restoredSale;
            CustomerVisit restoredCustomer;
            PendingResearchWork restoredResearch;
            EveningStep restoredEvening;
            string restoredMorning;
            bool restoredSaleSettled;
            int restoredMoney;
            RestoredCommonData restoredCommonData;
            try
            {
                ValidateSnapshot(snapshot, out restoredSale); restoredCustomer = ValidateCustomerSnapshot(snapshot, restoredSale);
                restoredSaleSettled = ValidateSaleSettlement(snapshot, restoredCustomer);
                restoredResearch = ValidateEveningSnapshot(snapshot, restoredCustomer, out restoredEvening, out restoredMorning);
                restoredMoney = checked((int)snapshot.Money);
                restoredCommonData = ValidateCommonDataSnapshot(snapshot);
            }
            catch (ArgumentException exception) { error = exception.Message; return false; }
            catch (OverflowException) { error = "저장된 값의 범위를 초과했습니다."; return false; }
            // Validate everything before assigning anything: a rejected save cannot partially mutate live state.
            int[] restoredSelected = (int[])snapshot.SelectedCounts.Clone();
            int[] restoredProduced = (int[])snapshot.ProducedCounts.Clone();
            bool[] restoredUnlocked = (bool[])snapshot.UnlockedProducts.Clone();
            day = snapshot.Day; money = restoredMoney; reputation = snapshot.Reputation; Capacity = snapshot.Capacity;
            phase = snapshot.Phase; selected = restoredSelected; produced = restoredProduced; unlocked = restoredUnlocked;
            RestoreCommonDataSnapshot(restoredCommonData);
            CommittedProductionCost = snapshot.ProductionCost; LastSale = restoredSale; IsSaleSettled = restoredSaleSettled; Customer = restoredCustomer;
            Evening = restoredEvening; PendingResearch = restoredResearch; MorningMessage = restoredMorning; restConfirmed = false;
            error = null; NotifyChanged(); return true;
        }

        private static void ValidateSnapshot(ShopSnapshot snapshot, out SaleResult restoredSale)
        {
            restoredSale = null;
            if (snapshot == null) throw new ArgumentException("저장 데이터가 없습니다.");
            if (snapshot.Version < 1 || snapshot.Version > 5) throw new ArgumentException("지원하지 않는 저장 버전입니다.");
            if (snapshot.Money < int.MinValue || snapshot.Money > int.MaxValue)
                throw new ArgumentException("저장된 보유 금액이 공통 API의 int 범위를 초과했습니다.");
            if (snapshot.Day < 1 || snapshot.Reputation < 0 || snapshot.Capacity < 30 || (snapshot.Capacity - 30) % 10 != 0)
                throw new ArgumentException("저장된 날짜, 평판 또는 진열대 크기가 잘못되었습니다.");
            if (!Enum.IsDefined(typeof(GamePhase), snapshot.Phase)) throw new ArgumentException("저장된 진행 단계가 잘못되었습니다.");
            ValidateArray(snapshot.SelectedCounts); ValidateArray(snapshot.ProducedCounts);
            if (snapshot.UnlockedProducts == null || snapshot.UnlockedProducts.Length != Catalog.Count)
                throw new ArgumentException("저장된 연구 데이터가 잘못되었습니다.");
            long selectedTotal = 0, producedTotal = 0;
            for (int i = 0; i < Catalog.Count; i++)
            {
                if (i < 5 && !snapshot.UnlockedProducts[i]) throw new ArgumentException("기본 떡은 항상 해금되어야 합니다.");
                if (!snapshot.UnlockedProducts[i] && (snapshot.SelectedCounts[i] != 0 || snapshot.ProducedCounts[i] != 0))
                    throw new ArgumentException("잠긴 떡을 생산할 수 없습니다.");
                selectedTotal += snapshot.SelectedCounts[i]; producedTotal += snapshot.ProducedCounts[i];
            }
            if (selectedTotal > snapshot.Capacity || producedTotal > snapshot.Capacity)
                throw new ArgumentException("저장된 수량이 진열대 크기를 초과했습니다.");
            if (snapshot.Phase == GamePhase.Arrangement)
            {
                if (producedTotal != 0 || snapshot.ProductionCost != 0 || snapshot.HasLastSale || !IsEmptySaleSnapshot(snapshot.LastSale))
                    throw new ArgumentException("배치 단계의 생산 또는 판매 데이터가 잘못되었습니다.");
                return;
            }
            if (!Equal(snapshot.SelectedCounts, snapshot.ProducedCounts) || snapshot.ProductionCost != CalculateCost(snapshot.ProducedCounts))
                throw new ArgumentException("저장된 생산 수량 또는 비용이 잘못되었습니다.");
            if (snapshot.Phase == GamePhase.ReadyToSell)
            {
                if (snapshot.HasLastSale || !IsEmptySaleSnapshot(snapshot.LastSale))
                    throw new ArgumentException("판매 전에는 판매 결과가 없어야 합니다.");
                return;
            }
            SaleSnapshot sale = snapshot.LastSale;
            if (!snapshot.HasLastSale || sale == null || sale.Day != snapshot.Day || sale.Reputation < 0 || sale.ProductionCost != snapshot.ProductionCost)
                throw new ArgumentException("저장된 판매 결과가 잘못되었습니다.");
            ValidateArray(sale.ProducedCounts); ValidateArray(sale.SoldCounts); ValidateRate(sale.Reputation, sale.Rate);
            if (!Equal(sale.ProducedCounts, snapshot.ProducedCounts) || !Equal(sale.SoldCounts, AllocateSales(snapshot.ProducedCounts, sale.Rate)))
                throw new ArgumentException("저장된 판매 수량이 잘못되었습니다.");
            if (sale.Revenue != CalculateRevenue(sale.SoldCounts)) throw new ArgumentException("저장된 매출이 잘못되었습니다.");
            restoredSale = new SaleResult(sale.Day, sale.Reputation, sale.Rate, sale.ProducedCounts, sale.SoldCounts, sale.ProductionCost, sale.Revenue);
        }

        private static SaleSnapshot EmptySaleSnapshot()
        {
            // A canonical non-null payload roundtrips through JsonUtility in every shop phase.
            return new SaleSnapshot { ProducedCounts = new int[Catalog.Count], SoldCounts = new int[Catalog.Count] };
        }

        private static bool ValidateSaleSettlement(ShopSnapshot snapshot, CustomerVisit customer)
        {
            // Every previous release posted the sale at opening, including unresolved customer visits.
            if (snapshot.Version < 4) return snapshot.Phase == GamePhase.Closed;
            if (snapshot.Phase != GamePhase.Closed)
            {
                if (snapshot.SaleSettled) throw new ArgumentException("판매 전에는 정산 완료 기록이 없어야 합니다.");
                return false;
            }
            if (!snapshot.SaleSettled && (customer == null || customer.Step == CustomerStep.Result))
                throw new ArgumentException("판매 결과를 확인한 영업일은 정산이 완료되어야 합니다.");
            // Migrated old saves may already be settled during Opening, Awaiting or Feedback.
            return snapshot.SaleSettled;
        }

        private static CustomerSnapshot EmptyCustomerSnapshot()
        {
            return new CustomerSnapshot { RecommendedProductIndex = -1, Dialogue = string.Empty };
        }

        private static PendingResearchSnapshot EmptyPendingResearchSnapshot()
        {
            return new PendingResearchSnapshot { ProductIndex = -1 };
        }

        private static bool IsEmptyPendingResearchSnapshot(PendingResearchSnapshot work)
        {
            if (work == null) return true;
            return work.Kind == ResearchKind.None && (work.ProductIndex == -1 || work.ProductIndex == 0)
                && work.RequestedDay == 0 && work.Cost == 0 && work.TargetCapacity == 0;
        }

        private static PendingResearchWork ValidateEveningSnapshot(ShopSnapshot snapshot, CustomerVisit visit,
            out EveningStep evening, out string morningMessage)
        {
            evening = EveningStep.None; morningMessage = string.Empty;
            if (snapshot.Version < 3)
            {
                if (snapshot.Evening != EveningStep.None || snapshot.HasPendingResearch
                    || !IsEmptyPendingResearchSnapshot(snapshot.PendingResearch) || !string.IsNullOrEmpty(snapshot.MorningMessage))
                    throw new ArgumentException("이전 저장 버전에는 저녁 연구 기록이 없습니다.");
                return null;
            }
            if (!Enum.IsDefined(typeof(EveningStep), snapshot.Evening)) throw new ArgumentException("저장된 저녁 행동 단계가 잘못되었습니다.");
            evening = snapshot.Evening; morningMessage = snapshot.MorningMessage ?? string.Empty;
            if (evening == EveningStep.MorningNotice)
            {
                if (snapshot.Phase != GamePhase.Arrangement || snapshot.Day < 2 || snapshot.HasPendingResearch
                    || Sum(snapshot.SelectedCounts) != 0 || (morningMessage != CakeResearchMorningMessage && morningMessage != StandResearchMorningMessage))
                    throw new ArgumentException("저장된 아침 연구 완료 안내가 잘못되었습니다.");
            }
            else
            {
                if (!string.IsNullOrEmpty(morningMessage)) throw new ArgumentException("연구 완료 안내는 아침에만 표시할 수 있습니다.");
                if (evening != EveningStep.None && (snapshot.Phase != GamePhase.Closed || (visit != null && visit.Step != CustomerStep.Result)))
                    throw new ArgumentException("하루 판매와 손님 추천을 마친 뒤 저녁 행동을 선택할 수 있습니다.");
            }
            if (!snapshot.HasPendingResearch)
            {
                if (!IsEmptyPendingResearchSnapshot(snapshot.PendingResearch)) throw new ArgumentException("예약되지 않은 연구 데이터가 있습니다.");
                return null;
            }
            PendingResearchSnapshot pending = snapshot.PendingResearch;
            if (pending == null || snapshot.Money < 0 || pending.RequestedDay != snapshot.Day)
                throw new ArgumentException("저장된 연구 예약 정보가 잘못되었습니다.");
            if (pending.Kind == ResearchKind.Cake)
            {
                int index = pending.ProductIndex;
                if (evening != EveningStep.CakeResearch || index < 5 || index >= Catalog.Count
                    || snapshot.UnlockedProducts[index] || pending.Cost != CakeResearchCosts[index - 5] || pending.TargetCapacity != 0)
                    throw new ArgumentException("저장된 떡 연구 예약 정보가 잘못되었습니다.");
                for (int previous = 5; previous < index; previous++)
                    if (!snapshot.UnlockedProducts[previous]) throw new ArgumentException("떡 연구는 순서대로 완료해야 합니다.");
            }
            else if (pending.Kind == ResearchKind.Stand)
            {
                int requiredReputation = snapshot.Capacity < 50 ? 15 : 35;
                long cost = snapshot.Capacity < 50 ? 150000L : 400000L;
                if (evening != EveningStep.StandResearch || pending.ProductIndex != -1 || snapshot.Capacity >= 70
                    || pending.Cost != cost || pending.TargetCapacity != snapshot.Capacity + 10 || snapshot.Reputation < requiredReputation)
                    throw new ArgumentException("저장된 가판대 연구 예약 정보가 잘못되었습니다.");
            }
            else throw new ArgumentException("저장된 연구 종류가 잘못되었습니다.");
            return new PendingResearchWork(pending.Kind, pending.ProductIndex, pending.RequestedDay, pending.Cost, pending.TargetCapacity);
        }

        private static bool IsEmptyCustomerSnapshot(CustomerSnapshot visit)
        {
            if (visit == null) return true;
            // The default inline object produced by JsonUtility uses zero rather than the canonical -1 index.
            return visit.Day == 0 && visit.Answer == RiceCakeTaste.Unspecified && visit.SelectedTaste == RiceCakeTaste.Unspecified
                && (visit.RecommendedProductIndex == -1 || visit.RecommendedProductIndex == 0)
                && string.IsNullOrEmpty(visit.Dialogue) && !visit.GeneratedWithAi && visit.Step == CustomerStep.None
                && !visit.WasSatisfied && visit.ReputationBefore == 0;
        }

        private static CustomerVisit ValidateCustomerSnapshot(ShopSnapshot snapshot, SaleResult sale)
        {
            if (!snapshot.HasCustomer)
            {
                if (!IsEmptyCustomerSnapshot(snapshot.Customer)) throw new ArgumentException("손님이 없는 저장 데이터에 방문 기록이 있습니다.");
                return null;
            }
            if (snapshot.Version == 1) throw new ArgumentException("이전 저장 버전에는 손님 방문 기록이 없습니다.");
            CustomerSnapshot visit = snapshot.Customer;
            if (snapshot.Phase != GamePhase.Closed || sale == null || visit == null || visit.Day != snapshot.Day
                || !IsKnownTaste(visit.Answer) || visit.ReputationBefore < 0 || sale.Reputation != visit.ReputationBefore
                || visit.Step < CustomerStep.Opening || visit.Step > CustomerStep.Result)
                throw new ArgumentException("저장된 손님 방문 정보가 잘못되었습니다.");
            if (visit.SelectedTaste != RiceCakeTaste.Unspecified && !IsKnownTaste(visit.SelectedTaste))
                throw new ArgumentException("저장된 맛 선택이 잘못되었습니다.");
            if (visit.Step == CustomerStep.Opening && visit.SelectedTaste != RiceCakeTaste.Unspecified)
                throw new ArgumentException("손님 등장 전에는 맛을 선택할 수 없습니다.");
            bool resolved = visit.Step == CustomerStep.Feedback || visit.Step == CustomerStep.Result;
            if (!resolved)
            {
                if (visit.RecommendedProductIndex != -1 || visit.WasSatisfied)
                    throw new ArgumentException("추천 전에는 손님의 만족 결과가 없어야 합니다.");
            }
            else
            {
                int index = visit.RecommendedProductIndex;
                if (index == -1)
                {
                    if (visit.WasSatisfied || HasRecommendableProductIn(snapshot.ProducedCounts, snapshot.UnlockedProducts))
                        throw new ArgumentException("추천할 떡이 있는 손님을 돌려보낼 수 없습니다.");
                }
                else if (!IsKnownTaste(visit.SelectedTaste) || index < 0 || index >= Catalog.Count
                    || !snapshot.UnlockedProducts[index] || snapshot.ProducedCounts[index] == 0
                    || Catalog[index].Taste != visit.SelectedTaste || visit.WasSatisfied != (visit.SelectedTaste == visit.Answer))
                    throw new ArgumentException("저장된 손님 추천 결과가 잘못되었습니다.");
            }
            return CustomerVisit.FromSnapshot(visit);
        }

        private static bool IsEmptySaleSnapshot(SaleSnapshot sale)
        {
            if (sale == null) return true;
            return sale.Day == 0 && sale.Reputation == 0 && sale.Rate == 0 && sale.ProductionCost == 0 && sale.Revenue == 0
                && IsEmptySaleArray(sale.ProducedCounts) && IsEmptySaleArray(sale.SoldCounts);
        }

        private static bool IsEmptySaleArray(int[] values)
        {
            // Null/empty arrays can occur in Unity's default inline-class placeholder.
            if (values == null || values.Length == 0) return true;
            if (values.Length != Catalog.Count) return false;
            foreach (int count in values) if (count != 0) return false;
            return true;
        }

        private CommonDataSnapshot CreateCommonDataSnapshot()
        {
            string[] marketingIds = SortedCommonIds(usedThisWeek.Keys);
            MarketingWeekSnapshot[] weeks = new MarketingWeekSnapshot[marketingIds.Length];
            for (int index = 0; index < marketingIds.Length; index++)
                weeks[index] = new MarketingWeekSnapshot { marketingId = marketingIds[index], week = usedThisWeek[marketingIds[index]] };
            marketingIds = SortedCommonIds(applyDay.Keys);
            MarketingApplyDaySnapshot[] applyDays = new MarketingApplyDaySnapshot[marketingIds.Length];
            for (int index = 0; index < marketingIds.Length; index++)
                applyDays[index] = new MarketingApplyDaySnapshot { marketingId = marketingIds[index], day = applyDay[marketingIds[index]] };
            List<CharacterId> characterIds = new List<CharacterId>(affection.Keys);
            characterIds.Sort((left, right) => ((int)left).CompareTo((int)right));
            AffectionSnapshot[] affectionValues = new AffectionSnapshot[characterIds.Count];
            for (int index = 0; index < characterIds.Count; index++)
                affectionValues[index] = new AffectionSnapshot { id = characterIds[index], amount = affection[characterIds[index]] };
            return new CommonDataSnapshot
            {
                completedResearchIds = SortedCommonIds(completedResearchIds),
                usedThisWeek = weeks, firstUsed = SortedCommonIds(firstUsed), applyDay = applyDays,
                affection = affectionValues, completedEventIds = SortedCommonIds(completedEventIds), currentRoute = currentRoute
            };
        }

        private static string[] SortedCommonIds(IEnumerable<string> values)
        {
            List<string> sorted = new List<string>(values);
            sorted.Sort(StringComparer.Ordinal);
            return sorted.ToArray();
        }

        private sealed class RestoredCommonData
        {
            internal HashSet<string> Research = new HashSet<string>(StringComparer.Ordinal);
            internal HashSet<string> Events = new HashSet<string>(StringComparer.Ordinal);
            internal Dictionary<string, int> Weeks = new Dictionary<string, int>(StringComparer.Ordinal);
            internal HashSet<string> FirstUsed = new HashSet<string>(StringComparer.Ordinal);
            internal Dictionary<string, int> ApplyDays = new Dictionary<string, int>(StringComparer.Ordinal);
            internal Dictionary<CharacterId, int> Affection = new Dictionary<CharacterId, int>();
            internal Route CurrentRoute;
        }

        private static RestoredCommonData ValidateCommonDataSnapshot(ShopSnapshot snapshot)
        {
            RestoredCommonData values = new RestoredCommonData();
            if (snapshot.Version < 5)
            {
                // Old saves stored completed shop research only as product unlocks and stall capacity.
                for (int index = 5; index < Catalog.Count; index++)
                    if (snapshot.UnlockedProducts[index]) values.Research.Add(Catalog[index].Id);
                for (int capacity = 40; capacity <= 70; capacity += 10)
                    if (snapshot.Capacity >= capacity) values.Research.Add(GetStandResearchId(capacity));
                return values;
            }

            CommonDataSnapshot common = snapshot.CommonData;
            if (common == null) throw new ArgumentException("저장된 공통 데이터가 없습니다.");
            values.Research = ValidateCommonIds(common.completedResearchIds, "연구");
            values.Events = ValidateCommonIds(common.completedEventIds, "이벤트");
            values.FirstUsed = ValidateCommonIds(common.firstUsed, "마케팅 최초 이용");
            for (int index = 5; index < Catalog.Count; index++)
                if (values.Research.Contains(Catalog[index].Id) != snapshot.UnlockedProducts[index])
                    throw new ArgumentException("저장된 연구 완료 ID와 떡 해금 정보가 일치하지 않습니다.");
            for (int capacity = 40; capacity <= 70; capacity += 10)
                if (values.Research.Contains(GetStandResearchId(capacity)) != (snapshot.Capacity >= capacity))
                    throw new ArgumentException("저장된 연구 완료 ID와 가판대 한도가 일치하지 않습니다.");

            if (common.usedThisWeek == null || common.applyDay == null || common.affection == null)
                throw new ArgumentException("저장된 공통 데이터 배열이 잘못되었습니다.");
            int currentWeek = (snapshot.Day - 1) / 7;
            foreach (MarketingWeekSnapshot marketing in common.usedThisWeek)
            {
                if (marketing == null || string.IsNullOrWhiteSpace(marketing.marketingId)
                    || marketing.week < 0 || marketing.week > currentWeek || values.Weeks.ContainsKey(marketing.marketingId))
                    throw new ArgumentException("저장된 마케팅 주간 이용 기록이 잘못되었습니다.");
                values.Weeks.Add(marketing.marketingId, marketing.week);
            }
            foreach (MarketingApplyDaySnapshot marketing in common.applyDay)
            {
                if (marketing == null || string.IsNullOrWhiteSpace(marketing.marketingId) || marketing.day < 2
                    || (long)marketing.day > (long)snapshot.Day + 1 || values.ApplyDays.ContainsKey(marketing.marketingId))
                    throw new ArgumentException("저장된 마케팅 적용일 기록이 잘못되었습니다.");
                values.ApplyDays.Add(marketing.marketingId, marketing.day);
            }
            if (values.Weeks.Count != values.FirstUsed.Count || values.ApplyDays.Count != values.FirstUsed.Count)
                throw new ArgumentException("저장된 마케팅 이용 기록이 서로 일치하지 않습니다.");
            foreach (string marketingId in values.FirstUsed)
            {
                int usedWeek, effectDay;
                if (!values.Weeks.TryGetValue(marketingId, out usedWeek) || !values.ApplyDays.TryGetValue(marketingId, out effectDay)
                    || usedWeek != (effectDay - 2) / 7)
                    throw new ArgumentException("저장된 마케팅 이용 주와 적용일이 일치하지 않습니다.");
            }
            foreach (AffectionSnapshot character in common.affection)
            {
                if (character == null || (int)character.id < 0 || values.Affection.ContainsKey(character.id))
                    throw new ArgumentException("저장된 캐릭터 호감도 기록이 잘못되었습니다.");
                values.Affection.Add(character.id, character.amount);
            }
            if ((int)common.currentRoute < 0) throw new ArgumentException("저장된 스토리 루트가 잘못되었습니다.");
            values.CurrentRoute = common.currentRoute;
            return values;
        }

        private static HashSet<string> ValidateCommonIds(string[] ids, string kind)
        {
            if (ids == null) throw new ArgumentException("저장된 " + kind + " ID 배열이 없습니다.");
            HashSet<string> values = new HashSet<string>(StringComparer.Ordinal);
            foreach (string id in ids)
                if (string.IsNullOrWhiteSpace(id) || !values.Add(id))
                    throw new ArgumentException("저장된 " + kind + " ID가 비어 있거나 중복되었습니다.");
            return values;
        }

        private void RestoreCommonDataSnapshot(RestoredCommonData values)
        {
            completedResearchIds = values.Research; completedEventIds = values.Events;
            usedThisWeek = values.Weeks; firstUsed = values.FirstUsed; applyDay = values.ApplyDays;
            affection = values.Affection; currentRoute = values.CurrentRoute;
        }

    }
}
