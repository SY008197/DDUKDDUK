# 공통 데이터 API

기준: `공통_데이터_API_설계표_초안 (1).pdf`, 2026-10-07.

설계표의 17개 함수는 기존 `DdukDduk.Shop.ShopModel`에 구현했습니다. 표에는 클래스 이름이 없어 기존 이름을 유지했습니다. 화면과 하루 진행 코드도 새 API를 사용합니다.

```csharp
int day = app.Model.GetDay();
int money = app.Model.GetMoney();
app.Model.AddReputation(1);
bool completed = app.Model.IsResearchCompleted("mugwort");
```

| 관리 데이터 | 함수 시그니처 |
| --- | --- |
| `day`, `phase` | `int GetDay()` · `GamePhase GetPhase()` · `void ChangePhase(GamePhase phase)` |
| `money`, `reputation` | `int GetMoney()` · `void AddMoney(int amount)` · `bool TrySpendMoney(int amount)` · `int GetReputation()` · `void AddReputation(int amount)` |
| `completedResearchIds` | `bool IsResearchCompleted(string researchId)` · `void CompleteResearch(string researchId)` |
| `usedThisWeek`, `firstUsed`, `applyDay` | `bool IsMarketingUsedThisWeek(string marketingId)` · `bool IsMarketingFirstUsed(string marketingId)` · `void UseMarketing(string marketingId)` · `bool IsMarketingActive(string marketingId)` |
| 캐릭터별 `affection` | `int GetAffection(CharacterId id)` · `void AddAffection(CharacterId id, int amount)` |
| `completedEventIds` | 표의 ‘미정’ 상태를 유지합니다. 이벤트 API를 추가하지 않았습니다. |
| `currentRoute` | `Route GetCurrentRoute()` |

공통 상태는 private 필드입니다. 이전 `model.Day`, `model.Phase`, `model.Money`, `model.Reputation` 읽기는 각각 `GetDay()`, `GetPhase()`, `GetMoney()`, `GetReputation()`으로 바꿨습니다. 평판 변경은 `AddReputation(amount)`를 사용합니다.

## 기존 게임과 연결

`ShopPhase`를 `GamePhase`로 변경하고 기존 값 `Arrangement=0`, `ReadyToSell=1`, `Closed=2`를 유지했습니다. `ChangePhase`는 기존 생산 확정·판매·다음 날 처리를 통해 순서대로 전환합니다. 손님·저녁의 세부 단계는 기존 `CustomerStep`, `EveningStep`에서 관리합니다. 정상 영업의 입금은 추천 피드백 이후 판매 결과를 표시할 때 한 번 발생합니다.

떡 연구 ID는 기존 `mugwort`, `honey`, `bean`, `royal`입니다. 가판대 완료 기록은 대상 한도에 맞춘 `stand_40`, `stand_50`, `stand_60`, `stand_70`입니다. PDF에는 ID 값이 없으므로 기존 상품 ID와 가판대 한도에 연결했습니다. 다른 팀의 연구 ID도 완료 기록과 저장에 보존합니다.

`CompleteResearch`는 완료 등록이며 알려진 떡·가판대 ID를 해금·한도에도 연결합니다. 일반 플레이에서는 기존 `TryScheduleCakeResearch`, `TryScheduleStandResearch`가 비용을 즉시 차감하고, 다음 날에 해금·확장과 완료 기록을 함께 적용합니다. 예약 중인 연구를 당일에 공개 API로 완료할 수 없습니다.

마케팅은 ID별로 관리합니다. 게임 날짜 1~7일이 같은 주이고 8일부터 새 주입니다. `IsMarketingFirstUsed`는 첫 사용 기록이 있는지 반환합니다(사용 전 false, 한 번 사용한 이후 true). `UseMarketing`은 다음 영업일을 적용일로 기록하며 같은 주의 반복 호출은 적용일을 바꾸지 않습니다. `IsMarketingActive`는 현재 날짜와 적용일이 같은지 확인합니다. 표에 없는 광고 종류·비용·효과·UI는 추가하지 않았습니다.

`CharacterId`, `Route`는 표에 타입 이름만 있어 실제 멤버를 비워 두었습니다. 캐릭터별 호감도 초기 값은 0입니다. 캐릭터 이름, 루트 이름, 이벤트 식별 규칙은 추가하지 않았습니다.

## 저장

저장 버전 5는 기존 버전 1~4의 돈·평판·날짜·정산·연구 예약을 읽고, 떡 해금·가판대 한도에서 연구 완료 ID를 복원합니다. 이전 JSON 필드 이름은 변환용 DTO에 유지합니다. 마케팅·호감도·이벤트·루트 기록도 저장합니다.

보유금 API와 상태는 표의 `int`를 사용합니다. 오버플로와 음수 지출은 거부하고, 돈이 부족한 `TrySpendMoney`는 차감 없이 false를 반환합니다. `AddMoney`의 음수 값은 손실 반영에 사용할 수 있습니다. 평판 최종 값은 0 이상이어야 합니다. 잘못된 저장이나 int 범위 밖의 이전 보유금은 자르지 않고 복원을 거부하며, 원본 저장을 기본 상태로 덮어쓰지 않습니다.

## 파일

- `Game/Assets/Shop/Scripts/Core/ShopModel.CommonData.cs`: 공통 상태와 17개 API.
- `Game/Assets/Shop/Scripts/Core/CommonDataTypes.cs`: 공유 타입.
- `Game/Assets/Shop/Scripts/Core/ShopModel.cs`: 생산·판매·연구 흐름.
- `Game/Assets/Shop/Scripts/Core/ShopModel.Persistence.cs`: 저장과 이전 버전 복원.
- `Game/Assets/Shop/Scripts/Core/CommonDataSnapshot.cs`: JSON 데이터.
- `Game/Assets/Shop/Editor/ShopCommonApiVerification.cs`: 공통 API 검증.
