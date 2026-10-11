using System;
using System.Collections.Generic;

[Serializable]
public class MarketingData  // 마케팅 관련 데이터
{
    public bool usedThisWeek;   // 이번 주 사용 여부
    public bool firstUsed;    // 첫 사용 여부
    public int applyDay;    // 적용된 날짜
}

[Serializable]
public class CharacterAffection  // 호감도 데이터
{
    public int affection;   // 호감도 점수
}

[Serializable]
public class SaveData
{
    // [세이브 메타]
    public string version = "1.0.0";
    public string savedAt; // 세이브 시각
    public float playTime;  // 누적 플레이 타임

    // [일정]
    public int day = 1; // 현재 날짜
    public GamePhase phase = GamePhase.Morning; // 현재 페이즈
    
    // [경제]
    public int money;   // 현재 보유 금액
    public int reputation;  // 현재 평판 점수

    // [연구]
    public List<string> completedResearchIds = new List<string>();  // 완료한 연구 ID 목록
    public List<string> pendingEffectIds = new List<string>();  // 미해금 연구 ID 목록

    // [마케팅]
    public MarketingData chaerinMarketing = new MarketingData(); // 한채린 마케팅 주간 사용 여부
    public MarketingData soeunMarketing = new MarketingData();   // 임소은 마케팅 주간 사용 여부 (참기름 6병)
    public MarketingData seogyeongMarketing = new MarketingData();   // 윤서경 마케팅 주간 사용 여부

    // [연애]
    public CharacterAffection chaerinAffection = new CharacterAffection();
    public CharacterAffection soeunAffection = new CharacterAffection();
    public CharacterAffection seogyeongAffection = new CharacterAffection();

    // [스토리]
    public List<string> completedEventIds = new List<string>(); // 완료한 이벤트 ID 목록
    public string currentRoute;    // 현재 진행 중인 루트 ID

    // [데이트 진행]
    public string currentEventId;   // 현재 진행 중인 이벤트 ID
    public int lineIndex = -1;  // 현재 진행 중인 이벤트의 대사 라인 인덱스 (-1이면 이벤트 시작 전)
    public List<int> choiceHistory = new List<int>();   // 현재 진행 중인 이벤트에서 선택한 선택지 인덱스 기록
}