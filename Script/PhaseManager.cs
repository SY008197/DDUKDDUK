using System;
using UnityEngine;

public class PhaseManager : MonoBehaviour   // 게임의 페이즈 및 날짜 관리
{
    // 싱글톤 인스턴스
    public static PhaseManager Instance { get; private set; }

    // 현재 상주 데이터
    public SaveData CurrentData { get; private set; } = new SaveData();

    // 페이즈 변경 시 UI 및 다른 시스템에 알릴 이벤트
    public event Action<GamePhase> OnPhaseChanged;  
    public event Action<int> OnDayChanged;

    private void Awake()    // 싱글톤 패턴 적용
    {
        if (Instance == null)   // 인스턴스가 존재하지 않으면 현재 객체를 인스턴스로 설정
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else // 이미 인스턴스가 존재하면 새로 생성된 객체는 파괴
        {
            Destroy(gameObject);
        }
    }

    private void Update()   // 누적 플레이 타임 갱신
    {
        if (CurrentData != null)
        {
            CurrentData.playTime += Time.deltaTime;
        }
    }

    public void AdvancePhase()  // 다음 페이즈로 진행 or 날짜 넘김
    {
        switch (CurrentData.phase) 
        {
            case GamePhase.Morning:
                SetPhase(GamePhase.Lunch);
                break;

            case GamePhase.Lunch:
                SetPhase(GamePhase.Closing);
                break;

            case GamePhase.Closing:
                SetPhase(GamePhase.Evening);
                break;

            case GamePhase.Evening:
                SetPhase(GamePhase.Night);
                break;

            case GamePhase.Night:
                AdvanceNextDay();
                break;
        }
    }

    private void SetPhase(GamePhase newPhase)   // 페이즈 변경
    {
        CurrentData.phase = newPhase;
        OnPhaseChanged?.Invoke(CurrentData.phase);
        Debug.Log($"[PhaseManager] 페이즈 변경: {newPhase}");
    }

    private void AdvanceNextDay()   // 날짜 변경
    {
        CurrentData.day++;

        // 주차 변경 시 마케팅 주간 사용 여부 리셋 (7일 단위)
        if (CurrentData.day % 7 == 1)
        {
            CurrentData.chaerinMarketing.usedThisWeek = false;
            CurrentData.soeunMarketing.usedThisWeek = false;
            CurrentData.seogyeongMarketing.usedThisWeek = false;
        }

        OnDayChanged?.Invoke(CurrentData.day);
        SetPhase(GamePhase.Morning);
        Debug.Log($"[PhaseManager] 날짜 변경: {CurrentData.day}일차");
    }

    public void LoadState(SaveData loadedData)  // 세이브 데이터 로드
    {
        CurrentData = loadedData;   // 로드된 데이터로 현재 상태 갱신
        OnDayChanged?.Invoke(CurrentData.day);  // 로드 시 현재 날짜 알림
        OnPhaseChanged?.Invoke(CurrentData.phase);  // 로드 시 현재 페이즈 및 날짜 알림
    }
}