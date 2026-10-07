#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DdukDduk.Shop
{
    /// <summary>Opt-in development-player UI check. Has no visible controls and does not run during normal play.</summary>
    public sealed class ShopVisualVerification : MonoBehaviour
    {
        private string output;
        private readonly List<string> checks = new List<string>();
        private ShopApp app;
        private ShopView view;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-shopCaptureDir");
            if (index < 0 || index + 1 >= args.Length) return;
            var probe = new GameObject("Shop visual verification").AddComponent<ShopVisualVerification>();
            probe.output = Path.GetFullPath(args[index + 1]);
            Directory.CreateDirectory(probe.output);
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLog;
            yield return null;
            // A desktop player may still be displaying its startup splash after scene initialization.
            while (!UnityEngine.Rendering.SplashScreen.isFinished) yield return null;
            yield return new WaitForSecondsRealtime(1f);
            app = FindFirstObjectByType<ShopApp>();
            view = FindFirstObjectByType<ShopView>();
            yield return Steps();
            File.WriteAllText(Path.Combine(output, "ui-verification.txt"), string.Join("\n", checks));
            Debug.Log("SHOP_UI_VERIFICATION_OK: " + checks.Count + " checks");
            Application.logMessageReceived -= OnLog;
            Application.Quit(0);
        }

        private void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert) return;
            File.WriteAllText(Path.Combine(output, "ui-error.txt"), message + "\n" + stack);
            File.WriteAllText(Path.Combine(output, "ui-verification.partial.txt"), string.Join("\n", checks));
            Application.Quit(2);
        }

        private IEnumerator Steps()
        {
            Check(app != null && view != null && app.Model != null, "scene initializes");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-shopRequireAi") >= 0)
            {
                yield return LiveAiSteps();
                yield break;
            }
            var model = app.Model;
            Check(model.Capacity == 30 && model.TotalSelected == 0, "initial shared capacity is 30");
            var primary = ButtonNamed("Confirm Production or Sell");
            Check(primary.interactable, "zero-selection placement is enabled");
            yield return Capture("01-shop-start.png");
            var plus = CardButton(0, "Increase");
            Check(IsRaycastTarget(plus), "quantity button receives pointer raycasts");
            for (int i = 0; i < 30; i++) Click(plus);
            Check(model.SelectedCounts[0] == 30 && !plus.interactable, "one recipe can fill shared capacity 30");
            Click(plus);
            Check(model.TotalSelected == 30, "disabled plus cannot exceed capacity");
            Click(ButtonNamed("Clear"));
            int[] mixed = { 7, 5, 8, 6, 4 };
            for (int i = 0; i < mixed.Length; i++)
                for (int j = 0; j < mixed[i]; j++) Click(CardButton(i, "Increase"));
            Check(model.TotalSelected == 30 && model.SelectedCounts[1] == 5 && model.SelectedCounts[3] == 6,
                "mixed recipes share a total of 30");
            var scroll = view.GetComponentInChildren<ScrollRect>();
            scroll.verticalNormalizedPosition = 0;
            yield return null;
            for (int i = 5; i < 9; i++)
                Check(!CardButton(i, "Increase").interactable, "production research lock " + model.Products[i].Name);
            yield return Capture("02-production-research.png");
            scroll.verticalNormalizedPosition = 1;
            model.AddReputation((14) - model.GetReputation());
            long beforePlacementMoney = model.GetMoney();
            Click(primary);
            Check(model.Customer != null && model.Customer.Step == CustomerStep.Opening, "placement immediately opens the shop");
            Check(!model.IsSaleSettled, "opening keeps the calculated sale unsettled");
            CheckMoney(beforePlacementMoney, "placement completion");
            Check(!HasActiveTransform("Daily Sales Result"), "placement completion does not expose the sales result");
            Check(HasText("판매가 시작되었습니다"), "sales opening message shown");
            Check(model.LastSale.Reputation == 14 && model.LastSale.Rate >= .75 && model.LastSale.Rate <= .8,
                "daily sales rate is fixed before recommendation");
            yield return Capture("03-sales-start.png");
            yield return AwaitStep(CustomerStep.AwaitingRecommendation);
            yield return new WaitForSecondsRealtime(2.8f);
            Check(!string.IsNullOrWhiteSpace(model.Customer.Dialogue), "customer requirement is visible");
            Check(!model.Customer.GeneratedWithAi, "offline fallback test uses its prepared line");
            Check(!model.IsSaleSettled, "arriving customer keeps the calculated sale unsettled");
            CheckMoney(beforePlacementMoney, "customer arrival");
            yield return Capture("04-customer-request.png");

            var answer = model.Customer.Answer;
            var chosen = answer;
            Click(ButtonNamed(TasteButtonName(chosen)));
            Check(model.Customer.SelectedTaste == chosen, "taste button selects its numeric answer");
            for (int i = 0; i < model.Products.Count; i++)
            {
                bool matches = model.Products[i].Taste == chosen && model.UnlockedProducts[i] && model.ProducedCounts[i] > 0;
                Check(ButtonNamed("Recommend " + model.Products[i].Id).interactable == matches,
                    "recommendation eligibility " + model.Products[i].Name);
            }
            int mismatched = Enumerable.Range(0, 5).First(i => model.Products[i].Taste != chosen);
            string mismatchText = TasteCaption(chosen) + "이 아닙니다.";
            Check(FindNamed("Recommendation Tray " + model.Products[mismatched].Id).GetComponentsInChildren<Text>().Any(t => t.text == mismatchText),
                "nonmatching cakes show exact taste restriction");
            Check(FindNamed("Recommendation Tray mugwort").GetComponentsInChildren<Text>().Any(t => t.text.Contains("연구가 필요합니다.")),
                "research lock remains in recommendation screen");
            yield return Capture("05-customer-taste-filter.png");
            int index = RecipeFor(chosen);
            var recommend = ButtonNamed("Recommend " + model.Products[index].Id);
            Check(IsRaycastTarget(recommend), "recommendation button receives pointer raycasts");
            Click(recommend);
            Check(model.Customer.WasSatisfied && model.GetReputation() == 15, "correct taste rewards exactly one reputation");
            Check(HasText("손님이 만족했습니다") && HasText("소문난 떡집"), "success feedback and new reputation grade shown");
            Check(!model.IsSaleSettled, "successful recommendation feedback has not settled the sale");
            CheckMoney(beforePlacementMoney, "successful recommendation feedback");
            long money = model.GetMoney();
            Click(recommend);
            Check(model.GetReputation() == 15 && model.GetMoney() == money, "repeat recommendation cannot reward or charge twice");
            yield return Capture("06-customer-satisfied.png");
            yield return AwaitStep(CustomerStep.Result);
            long settledMoney = beforePlacementMoney + model.LastSale.NetProfit;
            Check(model.IsSaleSettled, "sales result settles the calculated sale");
            CheckMoney(settledMoney, "sales result");
            Check(HasActiveTransform("Daily Sales Result"), "money settles while the sales result window is displayed");
            // Rebind follows the same UI initialization path as resuming a saved result screen.
            view.Bind(model);
            Check(model.IsSaleSettled, "result rebind preserves the completed settlement state");
            Check(HasActiveTransform("Daily Sales Result"), "result rebind keeps its sales result window visible");
            CheckMoney(settledMoney, "sales result rebind");
            view.Refresh();
            view.Refresh();
            CheckMoney(settledMoney, "repeated result refresh");
            Check(ButtonNamed("Next Day").interactable, "result provides an evening transition button");
            yield return Capture("07-daily-result.png");
            int nextDay = model.GetDay() + 1;
            yield return AwaitEvening(EveningStep.Prompt);
            Check(model.GetDay() == nextDay - 1, "result timer opens the evening without advancing the date");
            yield return EveningNavigationSteps();
            Check(model.Customer == null && model.TotalSelected == 0 && model.GetReputation() == 15,
                "confirmed rest advances to a cleared next day");
            Check(model.GetDay() == nextDay && model.Capacity == 30, "navigation and rest cannot grant a free capacity upgrade");
            CheckMoney(settledMoney, "next morning after a settled sale");

            // Empty production cannot recommend any cake, and has an explicit no-recommendation exit.
            long beforeEmptyMoney = model.GetMoney();
            Click(ButtonNamed("Confirm Production or Sell"));
            Check(model.LastSale.TotalProduced == 0 && model.LastSale.NetProfit == 0, "zero-production day opens without money change");
            Check(!model.IsSaleSettled, "empty display also waits for its result before settlement");
            CheckMoney(beforeEmptyMoney, "empty display opening");
            yield return AwaitStep(CustomerStep.AwaitingRecommendation);
            yield return new WaitForSecondsRealtime(2.8f);
            Click(ButtonNamed("Taste Mild"));
            for (int i = 0; i < model.Products.Count; i++)
                Check(!ButtonNamed("Recommend " + model.Products[i].Id).interactable, "empty display cannot recommend " + model.Products[i].Name);
            Check(FindNamed("Recommendation Tray garaetteok").GetComponentsInChildren<Text>().Any(t => t.text == "진열한 떡이 없습니다"),
                "zero-quantity cake shows its stock restriction");
            Check(!model.TryRecommendProduct(0, out _) && model.GetReputation() == 15,
                "model also rejects zero-quantity recommendations");
            Check(!model.HasRecommendableProduct && IsRaycastTarget(ButtonNamed("Decline Empty Display")),
                "empty day provides an explicit no-recommendation exit");
            yield return Capture("08-empty-display.png");
            Click(ButtonNamed("Decline Empty Display"));
            Check(!model.Customer.WasSatisfied && model.GetReputation() == 15 && model.Customer.RecommendedProductIndex == -1,
                "declining an empty display leaves reputation unchanged");
            Check(HasText("손님이 만족하지 못하였습니다"), "failure feedback exact message shown");
            Check(!model.IsSaleSettled, "empty display failure feedback remains unsettled");
            CheckMoney(beforeEmptyMoney, "empty display feedback");
            yield return new WaitForSecondsRealtime(.8f);
            yield return Capture("08-customer-unsatisfied.png");
            yield return AwaitStep(CustomerStep.Result);
            Check(model.IsSaleSettled && HasActiveTransform("Daily Sales Result"), "empty display settles at its result window");
            CheckMoney(beforeEmptyMoney, "empty display result");
            Click(ButtonNamed("Next Day"));
            yield return RestFromPrompt();
            Check(model.GetDay() == nextDay + 1 && model.Customer == null && model.Capacity == 30, "result skip still requires an evening rest confirmation");
            CheckMoney(beforeEmptyMoney, "empty display result skip and next morning");

            // Wrong taste can still be recommended when that cake was actually placed.
            model.TrySetCount(0, 1, out _);
            long beforeWrongMoney = model.GetMoney();
            model.OpenDayWithCustomer(2);
            Check(model.LastSale.NetProfit < 0, "single unsold cake provides a negative profit settlement case");
            CheckMoney(beforeWrongMoney, "wrong taste day opening");
            yield return AwaitStep(CustomerStep.AwaitingRecommendation);
            yield return new WaitForSecondsRealtime(2.8f);
            Click(ButtonNamed("Taste Mild"));
            Click(ButtonNamed("Recommend garaetteok"));
            Check(!model.Customer.WasSatisfied && model.GetReputation() == 15, "wrong taste with positive stock leaves reputation unchanged");
            Check(!model.IsSaleSettled, "wrong taste failure feedback remains unsettled");
            CheckMoney(beforeWrongMoney, "wrong taste recommendation feedback");
            yield return AwaitStep(CustomerStep.Result);
            long wrongSettledMoney = beforeWrongMoney + model.LastSale.NetProfit;
            Check(model.IsSaleSettled && HasActiveTransform("Daily Sales Result"), "wrong taste day settles at its result window");
            CheckMoney(wrongSettledMoney, "wrong taste sales result");
            view.Refresh();
            CheckMoney(wrongSettledMoney, "wrong taste repeated result refresh");
            Click(ButtonNamed("Next Day"));
            CheckMoney(wrongSettledMoney, "wrong taste result skip");

            yield return ResearchBookNavigationSteps();
            yield return CakeResearchSteps();
            yield return StandResearchSteps();

            CloseDayAtEvening();
            Click(ButtonNamed("Evening Research Yes"));
            Click(ButtonNamed("Evening Cake Choice"));
            yield return SelectRecipePage(3);
            Check(!ButtonNamed("Recipe Start Research").interactable, "completed recipe research cannot be purchased again");
            Screen.SetResolution(1280, 800, FullScreenMode.Windowed);
            yield return new WaitForSecondsRealtime(.8f);
            yield return Capture("22-research-1280x800.png");
            Click(ButtonNamed("Evening Back"));
            Click(ButtonNamed("Evening Back"));
            yield return RestFromPrompt();

            model.TrySetCount(1, 4, out _);
            model.TrySetCount(3, 4, out _);
            model.TrySetCount(4, 3, out _);
            model.TrySetCount(5, 2, out _);
            model.TrySetCount(8, 1, out _);
            model.OpenDayWithCustomer(1);
            yield return AwaitStep(CustomerStep.AwaitingRecommendation);
            Click(ButtonNamed("Taste Mild"));
            Check(ButtonNamed("Recommend mugwort").interactable, "research unlock enables its matching recommendation");
            Check(!ButtonNamed("Recommend royal").interactable, "unclassified royal recipe is never a quiz answer");
            Check(!ButtonNamed("Recommend garaetteok").interactable && ButtonNamed("Recommend jeolpyeon").interactable,
                "zero garaetteok is blocked while a placed mild cake remains recommendable");
            Check(!model.TryRecommendProduct(0, out _) && !model.Customer.IsResolved,
                "zero garaetteok cannot bypass the UI stock restriction");
            yield return new WaitForSecondsRealtime(2.8f);
            yield return Capture("09-customer-1280x800.png");
        }

        private IEnumerator LiveAiSteps()
        {
            var lines = new List<string>();
            for (int answer = 1; answer <= 3; answer++)
            {
                app.Model.TrySetCount(RecipeFor((RiceCakeTaste)answer), 2, out _);
                long beforeMoney = app.Model.GetMoney();
                app.Model.OpenDayWithCustomer(answer);
                Check(!app.Model.IsSaleSettled, "live AI opening waits for settlement for taste " + answer);
                CheckMoney(beforeMoney, "live AI opening for taste " + answer);
                yield return AwaitStep(CustomerStep.AwaitingRecommendation);
                var customer = app.Model.Customer;
                Check(customer.GeneratedWithAi && !string.IsNullOrWhiteSpace(customer.Dialogue),
                    "bundled local AI generates real dialogue for taste " + answer);
                lines.Add("Taste " + answer + ": " + customer.Dialogue);
                yield return new WaitForSecondsRealtime(2.8f);
                CheckMoney(beforeMoney, "live AI customer arrival for taste " + answer);
                Click(ButtonNamed(TasteButtonName(customer.Answer)));
                yield return Capture("live-ai-taste-" + answer + ".png");
                Click(ButtonNamed("Recommend " + app.Model.Products[RecipeFor(customer.Answer)].Id));
                Check(app.Model.Customer.WasSatisfied && app.Model.GetReputation() == answer, "live AI recommendation succeeds for taste " + answer);
                Check(!app.Model.IsSaleSettled, "live AI feedback remains unsettled for taste " + answer);
                CheckMoney(beforeMoney, "live AI recommendation feedback for taste " + answer);
                yield return AwaitStep(CustomerStep.Result);
                long afterMoney = beforeMoney + app.Model.LastSale.NetProfit;
                Check(app.Model.IsSaleSettled && HasActiveTransform("Daily Sales Result"), "live AI settles with its result window for taste " + answer);
                CheckMoney(afterMoney, "live AI sales result for taste " + answer);
                view.Refresh();
                CheckMoney(afterMoney, "live AI repeated result refresh for taste " + answer);
                Click(ButtonNamed("Next Day"));
                yield return RestFromPrompt();
                Check(app.Model.Customer == null, "live AI loop resets for another generation");
                CheckMoney(afterMoney, "live AI result skip and next morning for taste " + answer);
            }
            File.WriteAllText(Path.Combine(output, "live-ai-dialogues.txt"), string.Join("\n", lines));
        }

        private IEnumerator AwaitStep(CustomerStep desired)
        {
            float until = Time.unscaledTime + 40;
            while (app.Model.Customer == null || app.Model.Customer.Step != desired)
            {
                if (Time.unscaledTime >= until) throw new InvalidOperationException("Customer step timed out: " + desired);
                yield return new WaitForSecondsRealtime(.05f);
            }
            Check(app.Model.Customer.Step == desired, "flow reaches " + desired);
        }

        private IEnumerator EveningNavigationSteps()
        {
            var model = app.Model;
            int day = model.GetDay();
            long money = model.GetMoney();
            Check(IsRaycastTarget(ButtonNamed("Evening Research Yes")), "evening research choice receives pointer raycasts");
            yield return Capture("10-evening-prompt.png");
            Click(ButtonNamed("Evening Research Yes"));
            Check(model.Evening == EveningStep.Choice, "yes opens the research kind choice");
            yield return Capture("11-evening-choice.png");
            Click(ButtonNamed("Evening Cake Choice"));
            Check(model.Evening == EveningStep.CakeResearch, "cake choice opens the recipe book");
            Click(ButtonNamed("Evening Back"));
            Check(model.Evening == EveningStep.Choice, "recipe book back returns to the kind choice");
            Click(ButtonNamed("Evening Stand Choice"));
            Check(model.Evening == EveningStep.StandResearch, "stand choice opens the capacity research");
            Click(ButtonNamed("Evening Back"));
            Check(model.Evening == EveningStep.Choice, "stand back returns to the kind choice");
            Click(ButtonNamed("Evening Back"));
            Check(model.Evening == EveningStep.Prompt, "kind choice back returns to the evening question");
            Click(ButtonNamed("Evening Rest No"));
            Check(model.Evening == EveningStep.RestConfirmation && model.GetDay() == day, "rest opens confirmation before changing the date");
            Click(ButtonNamed("Rest Confirmation Back"));
            Check(model.Evening == EveningStep.Prompt, "rest confirmation back returns to the evening question");
            Check(model.GetMoney() == money && model.PendingResearch == null && model.GetDay() == day,
                "all back routes preserve funds, research, and date");
            yield return RestFromPrompt();
            Check(model.GetDay() == day + 1 && model.GetMoney() == money, "confirmed rest advances exactly once without charging");
        }

        private IEnumerator RestFromPrompt()
        {
            yield return AwaitEvening(EveningStep.Prompt);
            int day = app.Model.GetDay();
            Click(ButtonNamed("Evening Rest No"));
            Check(app.Model.Evening == EveningStep.RestConfirmation, "rest requires explicit confirmation");
            Click(ButtonNamed("Rest Confirm Next Day"));
            Check(app.Model.GetDay() == day + 1 && app.Model.Evening == EveningStep.None,
                "rest confirmation starts the next arrangement day");
            yield return null;
        }

        private IEnumerator ResearchBookNavigationSteps()
        {
            yield return AwaitEvening(EveningStep.Prompt);
            var model = app.Model;
            SetMoney(50000);
            Click(ButtonNamed("Evening Research Yes"));
            Click(ButtonNamed("Evening Cake Choice"));
            yield return SelectRecipePage(0);
            Check(!ButtonNamed("Recipe Start Research").interactable, "insufficient funds disable the first recipe research");
            long money = model.GetMoney();
            Check(!model.TryScheduleCakeResearch(5, out _) && model.GetMoney() == money && model.PendingResearch == null,
                "model rejects an unaffordable recipe without charging");
            SetMoney(3000000);
            yield return Capture("12-recipe-mugwort.png");
            Check(view.GetComponentsInChildren<Button>().Count(b => b.name == "Recipe Previous Page" || b.name == "Recipe Next Page") == 2,
                "the book has exactly two paper-corner navigation controls");
            Check(!HasActiveTransform("Recipe Left Corner") && !HasActiveTransform("Recipe Right Corner"),
                "legacy duplicate page controls are absent");
            Check(!ButtonNamed("Recipe Previous Page").interactable && ButtonNamed("Recipe Next Page").interactable,
                "first page enables only its next paper corner");
            Click(ButtonNamed("Recipe Previous Page"));
            Check(view.CurrentRecipePage == 0 && !view.IsRecipePageTurning,
                "the disabled first-page corner cannot turn beyond the book");
            Check(IsRaycastTarget(ButtonNamed("Recipe Next Page")), "book page control receives pointer raycasts");
            yield return VerifyRecipeTurn(1, new[] { "13a-recipe-forward-early.png", "13a2-recipe-forward-rising.png",
                "13-recipe-page-turn.png", "13b1-recipe-forward-settling.png", "13b-recipe-forward-late.png" });
            Check(view.CurrentRecipePage == 1 && HasText("꿀떡꿀떡"), "turned page displays the honey recipe");
            Check(!ButtonNamed("Recipe Start Research").interactable, "later recipes are readable but cannot skip the predecessor");
            money = model.GetMoney();
            Check(!model.TryScheduleCakeResearch(6, out _) && model.GetMoney() == money,
                "model rejects out of order honey research without charging");
            yield return Capture("14-recipe-honey-locked.png");
            Check(IsRaycastTarget(ButtonNamed("Recipe Previous Page")), "previous paper corner receives pointer raycasts");
            yield return VerifyRecipeTurn(-1, new[] { "13c-recipe-backward-early.png", "13c2-recipe-backward-rising.png",
                "13d-recipe-backward-middle.png", "13e1-recipe-backward-settling.png", "13e-recipe-backward-late.png" });
            Check(view.CurrentRecipePage == 0 && !ButtonNamed("Recipe Previous Page").interactable,
                "backward turn restores the first-page boundary");
            yield return VerifyCancelledRecipeTurn(.18f);
            yield return VerifyCancelledRecipeTurn(.82f);
            yield return SelectRecipePage(3);
            Check(HasText("궁떡궁떡") && !ButtonNamed("Recipe Start Research").interactable,
                "last recipe can be previewed while its research is locked");
            Check(!ButtonNamed("Recipe Next Page").interactable && ButtonNamed("Recipe Previous Page").interactable,
                "last page enables only its previous paper corner");
            Click(ButtonNamed("Recipe Next Page"));
            Check(view.CurrentRecipePage == 3 && !view.IsRecipePageTurning,
                "the disabled final-page corner cannot turn beyond the book");
            yield return SelectRecipePage(0);
            Click(ButtonNamed("Recipe Start Research"));
            Check(model.GetMoney() == money && model.PendingResearch == null, "opening a cost confirmation does not charge");
            yield return Capture("15-recipe-confirmation.png");
            Click(ButtonNamed("Research Confirm Back"));
            Check(model.Evening == EveningStep.CakeResearch && model.GetMoney() == money,
                "cost confirmation back returns to the book without charging");
            Click(ButtonNamed("Evening Back"));
            Click(ButtonNamed("Evening Back"));
            Check(model.Evening == EveningStep.Prompt, "book preview can return to the evening question");
        }

        private IEnumerator CakeResearchSteps()
        {
            long[] costs = { 60000, 180000, 360000, 720000 };
            for (int page = 0; page < 4; page++)
            {
                var model = app.Model;
                if (model.GetPhase() == GamePhase.Arrangement) CloseDayAtEvening();
                Check(model.Evening == EveningStep.Prompt, "recipe evening is available " + page);
                Click(ButtonNamed("Evening Research Yes"));
                Click(ButtonNamed("Evening Cake Choice"));
                yield return SelectRecipePage(page);
                int product = page + 5;
                Check(!model.UnlockedProducts[product] && ButtonNamed("Recipe Start Research").interactable,
                    "next sequential recipe is available " + model.Products[product].Name);
                Check(FindNamed("Recipe Cost").GetComponent<Text>().text.Contains(costs[page].ToString("N0")),
                    "recipe book shows exact cost " + model.Products[product].Name);
                if (page == 2) yield return Capture("17-recipe-bean.png");
                if (page == 3) yield return Capture("17-recipe-royal.png");
                int day = model.GetDay();
                long money = model.GetMoney();
                int capacity = model.Capacity;
                bool sawPaidPending = false;
                Action observe = () =>
                {
                    if (model.PendingResearch != null && model.PendingResearch.Kind == ResearchKind.Cake)
                        sawPaidPending |= model.GetDay() == day && !model.UnlockedProducts[product] && model.Capacity == capacity
                            && model.GetMoney() == money - costs[page] && model.PendingResearch.ProductIndex == product;
                };
                model.Changed += observe;
                Click(ButtonNamed("Recipe Start Research"));
                Check(model.GetMoney() == money && model.GetDay() == day && !model.UnlockedProducts[product],
                    "recipe remains locked before final confirmation " + product);
                Click(ButtonNamed("Research Confirm Start"));
                model.Changed -= observe;
                Check(sawPaidPending, "recipe cost is charged while the current day unlock remains pending " + product);
                Check(model.GetDay() == day + 1 && model.GetMoney() == money - costs[page] && model.UnlockedProducts[product]
                    && model.PendingResearch == null && model.Evening == EveningStep.MorningNotice,
                    "confirmed recipe unlock applies once on the following morning " + product);
                Check(HasText(ShopModel.CakeResearchMorningMessage), "cake morning displays the exact requested narrative " + product);
                Check(!model.TrySetCount(product, 1, out _) && !model.TryConfirmProduction(out _),
                    "morning announcement blocks production before acknowledgment " + product);
                if (page == 0) yield return Capture("16-cake-morning.png");
                Click(ButtonNamed("Morning Prepare Rice Cakes"));
                Check(model.Evening == EveningStep.None && model.MorningMessage == string.Empty,
                    "cake morning acknowledgment clears the one time announcement " + product);
                Check(model.TrySetCount(product, 1, out _), "new recipe is producible after the morning announcement " + product);
                model.ClearSelection();
                yield return null;
            }
            Check(modelAllResearchUnlocked(), "all four recipes unlock in the required order");
        }

        private IEnumerator StandResearchSteps()
        {
            var model = app.Model;
            model.AddReputation((14) - model.GetReputation());
            CloseDayAtEvening();
            Click(ButtonNamed("Evening Research Yes"));
            Click(ButtonNamed("Evening Stand Choice"));
            long money = model.GetMoney();
            Check(!ButtonNamed("Stand Start Research").interactable && !model.TryScheduleStandResearch(out _)
                && model.GetMoney() == money && model.Capacity == 30, "reputation below fifteen blocks first stand research without charging");
            model.AddReputation((15) - model.GetReputation());
            for (int upgrade = 0; upgrade < 4; upgrade++)
            {
                if (upgrade > 0)
                {
                    CloseDayAtEvening();
                    Click(ButtonNamed("Evening Research Yes"));
                    Click(ButtonNamed("Evening Stand Choice"));
                }
                int before = 30 + upgrade * 10;
                long cost = upgrade < 2 ? 150000 : 400000;
                Check(model.Capacity == before, "stand stage begins at the expected applied capacity " + before);
                if (upgrade == 0)
                {
                    money = model.GetMoney();
                    SetMoney(149999);
                    Check(!ButtonNamed("Stand Start Research").interactable && !model.TryScheduleStandResearch(out _)
                        && model.GetMoney() == 149999, "first stand stage requires its full one hundred fifty thousand won");
                    SetMoney(money);
                }
                if (upgrade == 2)
                {
                    money = model.GetMoney();
                    Check(!ButtonNamed("Stand Start Research").interactable && !model.TryScheduleStandResearch(out _)
                        && model.GetMoney() == money, "second stand stage requires reputation thirty five");
                    model.AddReputation((35) - model.GetReputation());
                    SetMoney(399999);
                    Check(!ButtonNamed("Stand Start Research").interactable && !model.TryScheduleStandResearch(out _)
                        && model.GetMoney() == 399999, "second stand stage requires its full four hundred thousand won");
                    SetMoney(money);
                    yield return Capture("20-stand-second-stage.png");
                }
                Check(ButtonNamed("Stand Start Research").interactable,
                    "stand research accepts its reputation and completed predecessor " + before);
                Check(FindNamed("Stand Cost").GetComponent<Text>().text.Contains(cost.ToString("N0")),
                    "stand displays its exact stage cost " + before);
                if (upgrade == 0) yield return Capture("18-stand-first-stage.png");
                int day = model.GetDay();
                money = model.GetMoney();
                Click(ButtonNamed("Stand Start Research"));
                Check(model.GetMoney() == money && model.Capacity == before && model.GetDay() == day,
                    "stand cost confirmation preserves capacity and funds " + before);
                Click(ButtonNamed("Research Confirm Back"));
                Check(model.GetMoney() == money && model.PendingResearch == null && model.Evening == EveningStep.StandResearch,
                    "stand cost confirmation back preserves the unchanged evening " + before);
                bool sawPaidPending = false;
                Action observe = () =>
                {
                    if (model.PendingResearch != null && model.PendingResearch.Kind == ResearchKind.Stand)
                        sawPaidPending |= model.GetDay() == day && model.Capacity == before && model.GetMoney() == money - cost
                            && model.PendingResearch.TargetCapacity == before + 10;
                };
                model.Changed += observe;
                Click(ButtonNamed("Stand Start Research"));
                Click(ButtonNamed("Research Confirm Start"));
                model.Changed -= observe;
                Check(sawPaidPending, "stand payment occurs before the next day capacity increase " + before);
                Check(model.GetDay() == day + 1 && model.GetMoney() == money - cost && model.Capacity == before + 10
                    && model.PendingResearch == null && model.Evening == EveningStep.MorningNotice,
                    "stand upgrade applies exactly ten packs the next morning " + before);
                Check(HasText(ShopModel.StandResearchMorningMessage), "stand morning displays the exact requested narrative " + before);
                Check(!model.TrySetCount(0, 1, out _) && !model.TryConfirmProduction(out _),
                    "stand morning announcement blocks production before acknowledgment " + before);
                if (upgrade == 0) yield return Capture("19-stand-morning.png");
                Click(ButtonNamed("Morning Prepare Rice Cakes"));
                Check(model.Evening == EveningStep.None && model.TrySetCount(0, before + 10, out _),
                    "new stand capacity is usable after the morning announcement " + before);
                Check(!model.TrySetCount(1, 1, out _), "expanded stand still enforces one shared capacity " + before);
                model.ClearSelection();
                yield return null;
            }
            CloseDayAtEvening();
            Click(ButtonNamed("Evening Research Yes"));
            Click(ButtonNamed("Evening Stand Choice"));
            money = model.GetMoney();
            Check(model.Capacity == 70 && !ButtonNamed("Stand Start Research").interactable
                && !model.TryScheduleStandResearch(out _) && model.GetMoney() == money, "maximum seventy packs blocks additional purchases");
            yield return Capture("21-stand-max.png");
            Click(ButtonNamed("Evening Back"));
            Click(ButtonNamed("Evening Back"));
            yield return RestFromPrompt();
        }

        private bool modelAllResearchUnlocked()
        {
            for (int i = 5; i < 9; i++) if (!app.Model.UnlockedProducts[i]) return false;
            return true;
        }

        private void SetMoney(long money)
        {
            var snapshot = app.Model.CreateSnapshot();
            snapshot.Money = money;
            Check(app.Model.TryRestoreSnapshot(snapshot, out _), "verification money preset restores a valid snapshot");
        }

        private void CloseDayAtEvening()
        {
            Check(app.Model.GetPhase() == GamePhase.Arrangement && app.Model.Evening == EveningStep.None,
                "research test starts from a normal arrangement morning");
            app.Model.ConfirmProduction();
            app.Model.Sell(ShopModel.GetSalesBand(app.Model.GetReputation()).Minimum);
            app.Model.BeginEvening();
        }

        private IEnumerator SelectRecipePage(int page)
        {
            yield return AwaitPageTurn();
            while (view.CurrentRecipePage != page)
            {
                Click(ButtonNamed(view.CurrentRecipePage < page ? "Recipe Next Page" : "Recipe Previous Page"));
                yield return AwaitPageTurn();
            }
            Check(view.CurrentRecipePage == page, "recipe navigation reaches page " + page);
        }

        private IEnumerator VerifyRecipeTurn(int direction, string[] screenshots)
        {
            var model = app.Model;
            int source = view.CurrentRecipePage;
            int day = model.GetDay();
            long money = model.GetMoney();
            int capacity = model.Capacity;
            int reputation = model.GetReputation();
            var unlocked = model.UnlockedProducts.ToArray();
            Click(ButtonNamed(direction > 0 ? "Recipe Next Page" : "Recipe Previous Page"));
            Check(view.IsRecipePageTurning, direction > 0
                ? "next recipe plays a page turn animation" : "previous recipe plays a page turn animation");
            float[] phases = { .18f, .35f, .5f, .65f, .82f };
            for (int i = 0; i < phases.Length; i++)
            {
                yield return HoldRecipeTurn(phases[i]);
                string stage = (direction > 0 ? "forward " : "backward ") + phases[i].ToString("F2", CultureInfo.InvariantCulture);
                Check(view.IsRecipePageTurning && Mathf.Abs(view.RecipeTurnProgress - phases[i]) < .01f,
                    "recipe turn capture uses its requested progress " + stage);
                Check(!ButtonNamed("Recipe Previous Page").interactable && !ButtonNamed("Recipe Next Page").interactable
                    && !ButtonNamed("Recipe Start Research").interactable,
                    "all book actions remain guarded during the turn " + stage);
                if (i == 0)
                {
                    Click(ButtonNamed("Recipe Next Page"));
                    Click(ButtonNamed("Recipe Previous Page"));
                    Click(ButtonNamed("Recipe Start Research"));
                    Check(view.IsRecipePageTurning && view.CurrentRecipePage == source
                        && !HasActiveTransform("Research Cost Confirmation"),
                        "rapid corner and research clicks cannot interrupt the current turn " + stage);
                }
                yield return Capture(screenshots[i]);
                VerifyTurnSurface(stage);
            }
            view.SetRecipeTurnVerificationProgress(null);
            yield return AwaitPageTurn();
            Check(view.CurrentRecipePage == source + direction && !HasActiveTransform("Turning Recipe Page"),
                "the finished page turn commits one page and removes its overlay " + direction);
            Check(model.GetMoney() == money && model.GetDay() == day && model.Capacity == capacity && model.GetReputation() == reputation
                && model.UnlockedProducts.SequenceEqual(unlocked) && model.PendingResearch == null,
                "reading a recipe in either direction preserves all progression state " + direction);
            Check(ButtonNamed("Recipe Previous Page").interactable == (view.CurrentRecipePage > 0)
                && ButtonNamed("Recipe Next Page").interactable == (view.CurrentRecipePage < 3),
                "finishing the turn restores the correct corner boundaries " + direction);
        }

        private IEnumerator HoldRecipeTurn(float progress)
        {
            view.SetRecipeTurnVerificationProgress(progress);
            float until = Time.unscaledTime + 3;
            var paper = view.GetComponentsInChildren<RecipePageTurnGraphic>(true).FirstOrDefault(t => t.name == "Curling Paper");
            while (paper == null || paper.FrontTexture == null || paper.BackTexture == null)
            {
                if (Time.unscaledTime >= until) throw new InvalidOperationException("Recipe page snapshots did not become ready.");
                yield return null;
                view.SetRecipeTurnVerificationProgress(progress);
                paper = view.GetComponentsInChildren<RecipePageTurnGraphic>(true).FirstOrDefault(t => t.name == "Curling Paper");
            }
            view.SetRecipeTurnVerificationProgress(progress);
            Canvas.ForceUpdateCanvases();
            yield return null;
        }

        private void VerifyTurnSurface(string stage)
        {
            var paper = FindNamed("Curling Paper").GetComponent<RecipePageTurnGraphic>();
            Check(paper.FrontTexture != null && paper.BackTexture != null
                && paper.FrontTexture.width == Screen.width && paper.FrontTexture.height == Screen.height
                && paper.BackTexture.width == Screen.width && paper.BackTexture.height == Screen.height,
                "both sides of the turning sheet use rendered page snapshots " + stage);
            Check(paper.FrontTexture != paper.BackTexture && ValidPageUv(paper.FrontUv) && ValidPageUv(paper.BackUv),
                "the turning sheet samples two distinct pages inside valid texture bounds " + stage);
            Check(!HasActiveTransform("Writing on Turning Page"),
                "the turn contains no floating placeholder writing " + stage);
            // CanvasRenderer owns this mesh; inspection must not destroy its live drawing data.
            var mesh = paper.canvasRenderer.GetMesh();
            Check(mesh != null, "page curl renderer provides its current mesh " + stage);
            bool edgeOn = Mathf.Abs(Mathf.Cos(paper.Progress * Mathf.PI)) < .0015f;
            bool finiteAndVisible = edgeOn ? mesh.vertexCount == 0 && mesh.triangles.Length == 0
                : mesh.vertexCount >= 12 && mesh.triangles.Length >= 12;
            var canvas = paper.canvas;
            var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            foreach (var vertex in mesh.vertices)
            {
                if (!Finite(vertex.x) || !Finite(vertex.y) || !Finite(vertex.z)) { finiteAndVisible = false; break; }
                var point = RectTransformUtility.WorldToScreenPoint(camera, paper.rectTransform.TransformPoint(vertex));
                if (!Finite(point.x) || !Finite(point.y) || point.x < -24 || point.x > Screen.width + 24
                    || point.y < -24 || point.y > Screen.height + 24) { finiteAndVisible = false; break; }
            }
            Check(finiteAndVisible,
                "page curl stays on screen and leaves a clean edge-on seam " + stage);
        }

        private static bool Finite(float number) => !float.IsNaN(number) && !float.IsInfinity(number);

        private static bool ValidPageUv(Rect uv) => Finite(uv.x) && Finite(uv.y) && Finite(uv.width) && Finite(uv.height)
            && uv.xMin >= -.001f && uv.xMax <= 1.001f && uv.yMin >= -.001f && uv.yMax <= 1.001f
            && uv.width > .1f && uv.height > .3f;

        private IEnumerator VerifyCancelledRecipeTurn(float progress)
        {
            var model = app.Model;
            int day = model.GetDay();
            long money = model.GetMoney();
            int capacity = model.Capacity;
            int reputation = model.GetReputation();
            var unlocked = model.UnlockedProducts.ToArray();
            Click(ButtonNamed("Recipe Next Page"));
            yield return HoldRecipeTurn(progress);
            Check(IsRaycastTarget(ButtonNamed("Evening Back")),
                "book back control remains reachable during a page turn " + progress);
            Click(ButtonNamed("Evening Back"));
            yield return null;
            Check(model.Evening == EveningStep.Choice && !view.IsRecipePageTurning
                && !HasActiveTransform("Turning Recipe Page"),
                "leaving the book cancels and hides a partial page turn " + progress);
            Check(model.GetDay() == day && model.GetMoney() == money && model.Capacity == capacity && model.GetReputation() == reputation
                && model.UnlockedProducts.SequenceEqual(unlocked) && model.PendingResearch == null,
                "cancelling a partial page turn preserves all progression state " + progress);
            var paper = view.GetComponentsInChildren<RecipePageTurnGraphic>(true).First(t => t.name == "Curling Paper");
            Check(paper.FrontTexture == null && paper.BackTexture == null,
                "cancelling releases both transient page snapshots " + progress);
            Click(ButtonNamed("Evening Cake Choice"));
            yield return null;
            Check(view.CurrentRecipePage == 0 && !view.IsRecipePageTurning && !HasActiveTransform("Turning Recipe Page")
                && !ButtonNamed("Recipe Previous Page").interactable && ButtonNamed("Recipe Next Page").interactable,
                "reopening the book restores a clean first page after cancellation " + progress);
            Check(IsRaycastTarget(ButtonNamed("Recipe Next Page")),
                "the page corner receives raycasts after reopening the book " + progress);
        }

        private IEnumerator AwaitPageTurn()
        {
            float until = Time.unscaledTime + 2;
            while (view.IsRecipePageTurning)
            {
                if (Time.unscaledTime >= until) throw new InvalidOperationException("Recipe page turn did not finish.");
                yield return null;
            }
            yield return null;
        }

        private IEnumerator AwaitEvening(EveningStep desired)
        {
            float until = Time.unscaledTime + 12;
            while (app.Model.Evening != desired)
            {
                if (Time.unscaledTime >= until) throw new InvalidOperationException("Evening step timed out: " + desired);
                yield return new WaitForSecondsRealtime(.05f);
            }
            Check(app.Model.Evening == desired, "flow reaches evening " + desired);
        }

        private bool HasText(string text) => view.GetComponentsInChildren<Text>().Any(t => t.text == text);
        private bool HasActiveTransform(string name) => view.GetComponentsInChildren<Transform>().Any(t => t.name == name && t.gameObject.activeInHierarchy);
        private void CheckMoney(long expected, string stage)
        {
            Check(app.Model.GetMoney() == expected, "model balance is correct at " + stage);
            var amount = FindNamed("Money").Find("Amount").GetComponent<Text>();
            Check(amount.text == expected.ToString("N0", CultureInfo.InvariantCulture) + "원", "visible money amount is correct at " + stage);
        }
        private static int RecipeFor(RiceCakeTaste taste) => taste == RiceCakeTaste.Mild ? 0 : taste == RiceCakeTaste.Sweet ? 4 : 3;
        private static string TasteButtonName(RiceCakeTaste taste) => "Taste " + taste;
        private static string TasteCaption(RiceCakeTaste taste) => taste == RiceCakeTaste.Mild ? "담백한 떡" : taste == RiceCakeTaste.Sweet ? "달콤한 떡" : "고소한 떡";

        private IEnumerator Capture(string filename)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForSecondsRealtime(.15f);
            string path = Path.Combine(output, filename);
            if (File.Exists(path)) File.Delete(path);
            // A hidden Windows window may skip rendering its back buffer. Render the same
            // Canvas through its camera into a texture so visual checks remain possible.
            var canvas = view.GetComponentInChildren<Canvas>();
            var camera = Camera.main;
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            var oldTarget = camera.targetTexture;
            var target = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            target.Create();
            camera.targetTexture = target;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
            Check(RenderPipeline.SupportsRenderRequest(camera, request), "camera supports offscreen UI capture");
            RenderPipeline.SubmitRenderRequest(camera, request);
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var screenshot = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            screenshot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            screenshot.Apply();
            File.WriteAllBytes(path, screenshot.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = oldTarget;
            canvas.renderMode = oldMode;
            canvas.worldCamera = oldCamera;
            target.Release();
            Destroy(target);
            Canvas.ForceUpdateCanvases();
            Check(File.Exists(path) && new FileInfo(path).Length > 1000, "screenshot captured: " + filename);
            Color sampleA = screenshot.GetPixel(screenshot.width / 2, screenshot.height / 2);
            Color sampleB = screenshot.GetPixel(screenshot.width / 4, screenshot.height / 4);
            Check(sampleA.maxColorComponent > .1f && sampleB.maxColorComponent > .1f,
                "screenshot contains rendered UI: " + filename);
            Destroy(screenshot);
        }

        private void Check(bool condition, string description)
        {
            if (!condition) throw new InvalidOperationException("Shop UI verification failed: " + description);
            checks.Add("PASS " + description);
        }

        private Transform FindNamed(string name)
        {
            var found = view.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == name && t.gameObject.activeInHierarchy);
            if (found == null) throw new InvalidOperationException("Active UI transform missing: " + name
                + " (evening " + app.Model.Evening + ")");
            return found;
        }

        private Button ButtonNamed(string name) => FindNamed(name).GetComponent<Button>();
        private Button CardButton(int index, string name) => FindNamed(ShopModel.Catalog[index].Name + " · Quantity Card").Find(name).GetComponent<Button>();

        private static void Click(Button button)
        {
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
                ExecuteEvents.pointerClickHandler);
        }

        private static bool IsRaycastTarget(Button button)
        {
            var rect = (RectTransform)button.transform;
            var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = center }, results);
            return results.Count > 0 && results[0].gameObject == button.gameObject;
        }
    }
}
#endif
