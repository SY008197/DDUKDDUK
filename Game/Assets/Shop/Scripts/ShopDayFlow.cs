using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using DdukDduk.Shop.AI;
using UnityEngine;

namespace DdukDduk.Shop
{
    /// <summary>Coordinates the visible daily flow. Model owns the answer, accounting and reward.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(LocalCustomerDialogue))]
    public sealed class ShopDayFlow : MonoBehaviour
    {
        [SerializeField, Min(0.2f)] private float openingSeconds = 1.8f;
        [SerializeField, Min(0.2f)] private float feedbackSeconds = 2.8f;
        [SerializeField, Min(1f)] private float resultSeconds = 7f;
        [SerializeField, Min(5f)] private float dialogueTimeoutSeconds = 30f;
        private ShopModel model;
        private ShopView view;
        private LocalCustomerDialogue dialogue;
        private CancellationTokenSource lifetime;
        private CancellationTokenSource generation;
        private CustomerStep observedStep = CustomerStep.None;
        private int observedDay = -1;
        private float stepStarted;
        private Task<DialogueGenerationResult> pendingDialogue;
        private bool initialized;

        public float FeedbackSeconds => feedbackSeconds;
        public float ResultSeconds => resultSeconds;

        public void Initialize(ShopModel shopModel, ShopView shopView)
        {
            if (initialized) return;
            model = shopModel ?? throw new ArgumentNullException(nameof(shopModel));
            view = shopView ?? throw new ArgumentNullException(nameof(shopView));
            dialogue = GetComponent<LocalCustomerDialogue>();
            lifetime = new CancellationTokenSource();
            view.OpenDayRequested += OpenDay;
            view.SkipResultRequested += SkipResult;
            initialized = true;
            // Read-only visual test runs use prepared lines; normal play always tries the bundled AI.
            if (!HasArgument("-shopFakeDialogue")) Warmup();
            StartCoroutine(RunDays());
        }

        private async void Warmup()
        {
            try { await dialogue.WarmupAsync(lifetime.Token); }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogWarning("Local dialogue warmup: " + exception.Message); }
        }

        private void OpenDay()
        {
            if (!initialized || model.Customer != null) return;
            try { model.OpenDayWithCustomer(); }
            catch (Exception exception) { Debug.LogWarning("Could not open shop: " + exception.Message); }
        }

        private void SkipResult()
        {
            if (!initialized || model.GetPhase() != GamePhase.Closed || model.Evening != EveningStep.None) return;
            if (model.Customer != null && model.Customer.Step != CustomerStep.Result) return;
            BeginEvening();
        }

        private IEnumerator RunDays()
        {
            while (initialized)
            {
                // A saved reservation has already been paid. Resume its overnight completion once.
                if (model.PendingResearch != null && model.GetPhase() == GamePhase.Closed)
                {
                    model.NextDay();
                    view.SetFlowTiming(0);
                    yield return null;
                    continue;
                }
                var customer = model.Customer;
                var step = customer == null ? CustomerStep.None : customer.Step;
                // Older saves can contain a closed shop without the newly added customer visit.
                if (customer == null && model.GetPhase() == GamePhase.Closed)
                    step = CustomerStep.Result;
                if (observedDay != model.GetDay() || step != observedStep)
                {
                    observedDay = model.GetDay();
                    observedStep = step;
                    stepStarted = Time.unscaledTime;
                    if (step == CustomerStep.Opening) BeginDialogue(customer);
                }
                float elapsed = Time.unscaledTime - stepStarted;
                if (step == CustomerStep.Opening && customer != null)
                {
                    if (pendingDialogue != null && pendingDialogue.IsCompleted)
                    {
                        string text = null;
                        bool usedAi = false;
                        if (pendingDialogue.Status == TaskStatus.RanToCompletion)
                        {
                            var result = pendingDialogue.Result;
                            text = result.Text;
                            usedAi = result.UsedAi;
                            if (!usedAi && !string.IsNullOrEmpty(result.Diagnostic))
                                Debug.LogWarning("Local dialogue fallback: " + result.Diagnostic);
                        }
                        if (string.IsNullOrWhiteSpace(text)) text = Fallback(customer.Answer, customer.Day);
                        // Nonempty AI output is displayed as requested; there is no semantic quality filter.
                        model.SetCustomerDialogue(text, usedAi);
                        pendingDialogue = null;
                    }
                    if (elapsed >= dialogueTimeoutSeconds && string.IsNullOrWhiteSpace(customer.Dialogue))
                    {
                        generation?.Cancel();
                        pendingDialogue = null;
                        model.SetCustomerDialogue(Fallback(customer.Answer, customer.Day), false);
                    }
                    if (elapsed >= openingSeconds && !string.IsNullOrWhiteSpace(customer.Dialogue))
                        model.PresentCustomer();
                }
                else if (step == CustomerStep.Feedback && elapsed >= feedbackSeconds)
                    model.FinishCustomerFeedback();
                else if (step == CustomerStep.Result && model.Evening == EveningStep.None)
                {
                    view.SetFlowTiming(Mathf.Max(0, resultSeconds - elapsed));
                    if (elapsed >= resultSeconds) BeginEvening();
                }
                yield return null;
            }
        }

        private void BeginDialogue(CustomerVisit customer)
        {
            generation?.Cancel();
            generation?.Dispose();
            pendingDialogue = null;
            if (!string.IsNullOrWhiteSpace(customer.Dialogue)) return;
            if (HasArgument("-shopFakeDialogue"))
            {
                model.SetCustomerDialogue(Fallback(customer.Answer, customer.Day), false);
                return;
            }
            generation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            generation.CancelAfter(TimeSpan.FromSeconds(dialogueTimeoutSeconds));
            pendingDialogue = dialogue.GenerateAsync((int)customer.Answer, customer.Day, generation.Token);
        }

        private void BeginEvening()
        {
            generation?.Cancel();
            pendingDialogue = null;
            model.BeginEvening();
            view.SetFlowTiming(0);
        }

        private static bool HasArgument(string argument) => Array.IndexOf(Environment.GetCommandLineArgs(), argument) >= 0;

        public static string Fallback(RiceCakeTaste answer, int day)
        {
            string[][] lines =
            {
                new[] {
                    "너무 달지 않고 깔끔한 맛이 좋겠어요. 차 한 잔과 편하게 먹을 떡을 추천해 주실래요?",
                    "쌀 본래의 은은한 맛을 느끼고 싶어요. 달달한 것보다 담백하게 먹을 수 있는 떡이 있나요?",
                    "오늘은 자극적이지 않은 간식이 당겨요. 부담 없이 씹어 먹을 수 있는 담백한 떡을 골라 주세요.",
                    "달콤함이나 진한 콩 향보다는 깔끔한 맛이 좋아요. 그런 떡을 하나 추천해 주세요." },
                new[] {
                    "오늘은 달달한 간식으로 기분을 바꾸고 싶어요. 먹으면 입안이 달콤해지는 떡이 있을까요?",
                    "오후에 힘이 조금 빠지네요. 한입 먹으면 기분이 좋아지는 달콤한 떡을 추천해 주세요.",
                    "차와 함께 달달하게 즐길 간식을 찾고 있어요. 달콤한 맛이 나는 떡으로 골라 주실래요?",
                    "오늘은 담백한 것보다 달콤한 맛이 당기네요. 그런 떡을 추천해 주세요." },
                new[] {
                    "콩가루 같은 구수한 향이 참 좋아요. 씹을수록 고소한 맛이 나는 떡을 추천해 주세요.",
                    "진하게 달기보다는 볶은 콩처럼 구수한 맛이 좋겠어요. 고소한 떡이 있을까요?",
                    "오늘은 콩의 고소한 향을 느끼고 싶어요. 그런 맛이 잘 어울리는 떡으로 골라 주세요.",
                    "달달한 맛보다 고소하고 구수한 간식이 당겨요. 어울리는 떡을 추천해 주실래요?" }
            };
            int group = Mathf.Clamp((int)answer - 1, 0, 2);
            return lines[group][Math.Max(0, day - 1) % lines[group].Length];
        }

        private void OnDestroy()
        {
            initialized = false;
            if (view != null)
            {
                view.OpenDayRequested -= OpenDay;
                view.SkipResultRequested -= SkipResult;
            }
            generation?.Cancel();
            generation?.Dispose();
            lifetime?.Cancel();
            lifetime?.Dispose();
        }
    }
}
