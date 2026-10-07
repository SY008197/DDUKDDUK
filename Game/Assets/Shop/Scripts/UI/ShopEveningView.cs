using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using T = DdukDduk.Shop.ShopTheme;

namespace DdukDduk.Shop
{
    public sealed partial class ShopView
    {
        private RectTransform eveningLayer, eveningPrompt, eveningChoice, recipeBook, standResearch, restConfirmation, morningNotice;
        private RectTransform bookLeftPage, bookRightPage, researchConfirmation, flipRoot, recipeReference, turnSnapshotRoot;
        private Text recipeTitle, recipeSubtitle, recipeOpening, recipeIngredients, recipeConclusion, recipeLetter;
        private readonly Text[] recipeExperimentLines = new Text[3];
        private Text recipeName, recipeCost, recipePrerequisite, recipeResult, recipeStatus, recipePageNumber;
        private Text standCapacity, standCost, standRequirement, standStatus, morningMessage, morningEffect;
        private Text confirmationTitle, confirmationBody, confirmationCost;
        private Button recipePrevious, recipeNext, recipeStart, standStart, confirmationStart, recipeBookBack;
        private CanvasGroup recipePageGroup, recipeStaticGroup;
        private RawImage turnUnderPage, turnStationaryPage;
        private Texture2D turnOldSnapshot, turnNewSnapshot;
        private Rect turnLeftUv, turnRightUv, turnSpreadUv;
        private RecipePageTurnGraphic turningPaper, turningShadow;
        private EveningStep lastEvening = (EveningStep)(-1);
        private int recipePage, nextRecipePage;
        private float recipeTurnStarted, recipeTurnProgress;
        private const float RecipeTurnDuration = .82f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private float? verificationTurnProgress;
#endif
        private bool recipeTurning, recipeTurnRebound;
        private ResearchKind confirmationKind;
        private int confirmationProductIndex;

        public int CurrentRecipePage { get { return recipePage; } }
        public bool IsRecipePageTurning { get { return recipeTurning; } }
        public float RecipeTurnProgress { get { return recipeTurnProgress; } }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void SetRecipeTurnVerificationProgress(float? progress)
        {
            if (!recipeTurning) return;
            verificationTurnProgress = progress.HasValue ? Mathf.Clamp01(progress.Value) : (float?)null;
            if (progress.HasValue) ApplyRecipeTurn(verificationTurnProgress.Value);
            else recipeTurnStarted = Time.unscaledTime - recipeTurnProgress * RecipeTurnDuration;
        }
#endif

        private void BuildEveningScreens()
        {
            Array.Clear(recipeSceneSprites, 0, recipeSceneSprites.Length);
            Array.Clear(recipeButtonSprites, 0, recipeButtonSprites.Length);
            lastEvening = (EveningStep)(-1);
            recipePage = 0;
            recipeTurning = false;
            eveningLayer = T.Rect("Evening Screen", stage);
            T.Fill(eveningLayer);
            var wash = T.At("Evening Light", eveningLayer, 0, 151, 1920, 929);
            T.Paint(wash, new Color(.13f, .10f, .06f, .30f), 0);

            eveningPrompt = EveningPanel("Research Prompt Screen");
            EveningHeading(eveningPrompt, "오늘의 영업을 마쳤어요", "떡을 빚던 자리가 조용해졌다. 내일을 위해 무엇을 해볼까?");
            EveningChoiceCard(eveningPrompt, "Evening Research Yes", 338, "연구할까?", "새로운 맛과 넓어진 가판대를 준비해요.", "연구하기", () => model?.ChooseResearch(true));
            EveningChoiceCard(eveningPrompt, "Evening Rest No", 990, "아니 오늘은 쉬자", "오늘은 쉬고, 내일 다시 정성껏 빚어요.", "하루 마무리", () => model?.ChooseResearch(false));
            EveningNarration(eveningPrompt, "오늘도 손님들에게 떡을 건넸다.\n불을 끄기 전, 엄마의 레시피 책이 눈에 들어왔다.");

            eveningChoice = EveningPanel("Research Kind Screen");
            EveningHeading(eveningChoice, "내일을 위한 작은 준비", "오늘 저녁에는 한 가지 연구를 진행할 수 있어요.");
            EveningChoiceCard(eveningChoice, "Evening Cake Choice", 338, "떡 연구를 진행", "엄마의 책을 펼쳐 다음 레시피를 찾아요.", "떡 연구소", () => model?.ChooseResearchKind(ResearchKind.Cake));
            EveningChoiceCard(eveningChoice, "Evening Stand Choice", 990, "가판대 업그레이드를 진행", "떡을 놓을 자리를 조금 더 넓혀요.", "가판대 연구", () => model?.ChooseResearchKind(ResearchKind.Stand));
            EveningBack(eveningChoice);
            EveningNarration(eveningChoice, "새로운 떡의 맛을 찾아볼까?\n아니면 더 많은 떡을 놓을 자리를 마련할까?");

            BuildRecipeBook();
            BuildStandResearch();

            restConfirmation = EveningPanel("Rest Confirmation Screen");
            var rest = Board("Rest Confirmation", restConfirmation, 477, 329, 966, 443, T.DarkWood, T.LightPaper, 16);
            T.Label("Rest Title", rest, 54, 50, 858, 65, "다음 날로 갈까요?", 41, T.Ink, TextAnchor.MiddleCenter, true, true);
            T.Label("Rest Description", rest, 88, 141, 790, 99, "오늘의 연구를 쉬고 하루를 마무리해요.\n아침이 밝으면 다시 떡을 준비할 수 있어요.", 25, T.MutedInk, TextAnchor.MiddleCenter);
            T.Button("Rest Confirmation Back", rest, 65, 307, 355, 77, "뒤로 가기", T.Hex("E6D1AF"), EveningBackAction, 27);
            T.Button("Rest Confirm Next Day", rest, 440, 307, 461, 77, "다음 날로 가기", T.Green, () => { if (model != null) model.ConfirmRest(); }, 28, T.LightPaper);

            morningNotice = EveningPanel("Morning Research Completion Screen");
            var morning = Board("Morning Research Letter", morningNotice, 330, 242, 1260, 616, T.DarkWood, T.LightPaper, 17);
            T.Label("Morning Title", morning, 64, 39, 1132, 59, "밤을 지나, 새로운 아침", 42, T.Green, TextAnchor.MiddleCenter, true, true);
            T.Label("Morning Subtitle", morning, 64, 108, 1132, 35, "오늘의 떡방앗간", 22, T.MutedInk, TextAnchor.MiddleCenter, false, true);
            T.Panel("Morning Rule", morning, 86, 176, 1088, 2, T.Gold, 0);
            morningMessage = T.Label("Morning Message", morning, 112, 200, 1036, 151, "", 32, T.Ink, TextAnchor.MiddleCenter, false, true);
            morningEffect = T.Label("Morning Effect", morning, 112, 363, 1036, 74, "", 28, T.Green, TextAnchor.MiddleCenter, true);
            T.Button("Morning Prepare Rice Cakes", morning, 319, 479, 622, 86, "오늘의 떡 준비하기", T.Green, () => model?.AcknowledgeMorning(), 30, T.LightPaper);

            BuildResearchConfirmation();
            eveningLayer.gameObject.SetActive(false);
        }

        private RectTransform EveningPanel(string name)
        {
            var layer = T.Rect(name, eveningLayer);
            T.Fill(layer);
            return layer;
        }

        private void EveningHeading(Transform parent, string title, string subtitle)
        {
            T.Label("Evening Title", parent, 336, 191, 1248, 75, title, 46, T.LightPaper, TextAnchor.MiddleCenter, true, true);
            T.Label("Evening Subtitle", parent, 336, 270, 1248, 40, subtitle, 24, T.LightPaper, TextAnchor.MiddleCenter);
        }

        private void EveningChoiceCard(Transform parent, string name, float x, string title, string description, string caption, Action action)
        {
            var card = Board(name + " Board", parent, x, 356, 592, 376, T.DarkWood, T.Paper, 14);
            var image = card.GetComponent<Image>();
            image.raycastTarget = true;
            var button = card.gameObject.AddComponent<Button>();
            card.name = name;
            button.targetGraphic = image;
            button.onClick.AddListener(() => { if (model != null) action(); });
            var colors = button.colors;
            colors.highlightedColor = T.Hex("FFEBBD");
            colors.pressedColor = T.Hex("CAB48C");
            button.colors = colors;
            T.Label("Choice Title", card, 32, 58, 528, 93, title, 35, T.Ink, TextAnchor.MiddleCenter, true, true);
            T.Label("Choice Description", card, 42, 162, 508, 77, description, 23, T.MutedInk, TextAnchor.MiddleCenter);
            T.Panel("Choice Ribbon", card, 122, 274, 348, 60, T.Green, 9);
            T.Label("Choice Caption", card, 130, 274, 332, 60, caption, 27, T.LightPaper, TextAnchor.MiddleCenter, true);
        }

        private void EveningNarration(Transform parent, string text)
        {
            var note = Board("Evening Narrative", parent, 235, 833, 1450, 151, T.DarkWood, T.Paper, 10);
            T.Label("Evening Narrative Text", note, 58, 25, 1334, 101, text, 27, T.Ink, TextAnchor.MiddleCenter, false, true);
        }

        private void EveningBack(Transform parent)
        {
            T.Button("Evening Back", parent, 63, 181, 210, 60, "‹ 뒤로 가기", T.LightPaper, EveningBackAction, 24);
        }

        private void EveningBackAction()
        {
            if (researchConfirmation != null && researchConfirmation.gameObject.activeSelf)
            {
                researchConfirmation.gameObject.SetActive(false);
                confirmationKind = ResearchKind.None;
                return;
            }
            StopRecipeTurn();
            model?.BackFromEvening();
        }

        private readonly Sprite[] recipeSceneSprites = new Sprite[4];
        private readonly Sprite[] recipeButtonSprites = new Sprite[4];
        private Image recipeSceneImage;
        private RectTransform recipeLiveLeft;
        private Image recipeOriginalLeft;
        private Font recipeHandwrittenFont, recipeBrushFont;
        private static readonly string[] RecipeSpreadResources =
        {
            "Shop/RecipeSpreadMugwort", "Shop/RecipeSpreadHoney", "Shop/RecipeSpreadBean", "Shop/RecipeSpreadRoyal"
        };

        private Font RecipeHandwriting
        {
            get
            {
                if (recipeHandwrittenFont == null)
                    recipeHandwrittenFont = Resources.Load<Font>("Shop/Fonts/RecipeHandwriting")
                        ?? Font.CreateDynamicFontFromOSFont(new[] { "HY궁서B", "궁서", "Gungsuh", "바탕", "Batang", "Malgun Gothic" }, 32);
                return recipeHandwrittenFont;
            }
        }

        private Font RecipeBrushTitle
        {
            get
            {
                if (recipeBrushFont == null)
                    recipeBrushFont = Resources.Load<Font>("Shop/Fonts/RecipeBrushTitle") ?? RecipeHandwriting;
                return recipeBrushFont;
            }
        }

        private Text RecipeLabel(string name, Transform parent, float x, float y, float width, float height,
            string value, int size, TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false, Color? color = null, bool brush = false)
        {
            var label = T.Label(name, parent, x, y, width, height, value, size, color ?? T.Hex("302216"), alignment, bold);
            label.font = brush ? RecipeBrushTitle : RecipeHandwriting;
            return label;
        }

        private void RecipeInkLine(string name, Transform parent, Vector2 from, Vector2 to, Color ink, float thickness)
        {
            var direction = to - from;
            var stroke = T.Panel(name, parent, from.x, from.y, direction.magnitude, thickness, ink, 0).rectTransform;
            stroke.localRotation = Quaternion.Euler(0, 0, -Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        }

        private void RecipeConclusionMark(Transform parent)
        {
            var ink = new Color(.65f, .23f, .17f, .88f);
            var circle = T.At("Hand-drawn Conclusion Circle", parent, 221, 811, 401, 45);
            const int segments = 72;
            for (int i = 0; i < segments; i++)
            {
                var a = i * Mathf.PI * 2 / segments;
                var b = (i + 1) * Mathf.PI * 2 / segments;
                var from = new Vector2(200 + 200 * Mathf.Cos(a), 22 + 20 * Mathf.Sin(a) + .8f * Mathf.Sin(a * 3));
                var to = new Vector2(200 + 200 * Mathf.Cos(b), 22 + 20 * Mathf.Sin(b) + .8f * Mathf.Sin(b * 3));
                RecipeInkLine("Conclusion Pencil Stroke " + i, circle, from, to, ink, 2.0f);
            }
            var star = T.At("Hand-drawn Conclusion Star", parent, 194, 817, 27, 29);
            var points = new Vector2[5];
            for (int i = 0; i < points.Length; i++)
            {
                var angle = -Mathf.PI / 2 + i * Mathf.PI * 2 / 5;
                points[i] = new Vector2(13 + 13 * Mathf.Cos(angle), 14 + 13 * Mathf.Sin(angle));
            }
            for (int i = 0; i < points.Length; i++)
                RecipeInkLine("Conclusion Star Stroke " + i, star, points[(i * 2) % 5], points[((i + 1) * 2) % 5], ink, 1.7f);
        }

        private Sprite RecipeImageCrop(Texture2D texture, float x, float y, float width, float height, string name)
        {
            if (texture == null) return null;
            // Artwork uses the supplied 1818 x 1023 layout; source files can retain their own export resolution.
            var rect = new Rect(x / 1818f * texture.width,
                (1023 - y - height) / 1023f * texture.height,
                width / 1818f * texture.width, height / 1023f * texture.height);
            var sprite = Sprite.Create(texture, rect, new Vector2(.5f, .5f), 100, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            generatedAtlasSprites.Add(sprite);
            return sprite;
        }

        private void BuildRecipeBook()
        {
            recipeBook = EveningPanel("Recipe Book Screen");
            // Preserve the photograph's proportions below the game's live status bar.
            var scene = T.At("Reference Recipe Scene", recipeBook, 0, 151, 1920, 929);
            var reference = T.At("Recipe Reference Layout · 1818 x 923", scene, 0, 0, 1818, 923);
            recipeReference = reference;
            reference.localScale = new Vector3(1920f / 1818f, 929f / 923f, 1);
            var staticContent = T.Rect("Recipe Static Content", reference);
            T.Fill(staticContent);
            recipeStaticGroup = staticContent.gameObject.AddComponent<CanvasGroup>();
            var background = T.Rect("Mother Recipe Book", staticContent);
            T.Fill(background);
            recipeSceneImage = T.Paint(background, Color.white, 0);
            recipeSceneImage.type = Image.Type.Simple;

            // On the first spread the user's supplied writing and photographs stay pixel-for-pixel intact.
            var originalTexture = Resources.Load<Texture2D>("Shop/RecipeBookReference");
            var originalLeft = T.At("Original Mugwort Recipe Page", staticContent, 100, 15, 880, 895);
            recipeOriginalLeft = T.Paint(originalLeft, Color.white, 0);
            recipeOriginalLeft.type = Image.Type.Simple;
            recipeOriginalLeft.sprite = RecipeImageCrop(originalTexture, 100, 115, 880, 895, "Original mugwort recipe notes");
            originalLeft.gameObject.SetActive(false);

            recipeLiveLeft = T.Rect("Live Handwritten Recipe Notes", staticContent);
            T.Fill(recipeLiveLeft);
            bookLeftPage = recipeLiveLeft;
            RecipeLabel("Book Origin", bookLeftPage, 278, 43, 329, 37, "어머니의 레시피", 30);
            recipeTitle = RecipeLabel("Recipe Handwritten Name", bookLeftPage, 277, 82, 330, 86, "", 68, brush: true);
            recipeSubtitle = RecipeLabel("Recipe Subtitle", bookLeftPage, 280, 172, 327, 25, "", 18);
            recipeSubtitle.gameObject.SetActive(false);
            recipeOpening = RecipeLabel("Recipe Opening", bookLeftPage, 281, 181, 324, 136, "", 27, TextAnchor.UpperLeft);
            RecipeLabel("Ingredients Title", bookLeftPage, 305, 355, 154, 43, "재료 메모", 30);
            recipeIngredients = RecipeLabel("Recipe Ingredients", bookLeftPage, 302, 506, 427, 92, "", 26, TextAnchor.UpperLeft);
            RecipeLabel("Experiments Title", bookLeftPage, 203, 639, 329, 39, "실험 기록", 30);
            RecipeInkLine("Experiments Heading Underline", bookLeftPage, new Vector2(203, 676), new Vector2(328, 674), new Color(.24f, .18f, .12f, .64f), 1.3f);
            var experimentRows = T.At("Recipe Experiments", bookLeftPage, 202, 686, 415, 113);
            for (int i = 0; i < recipeExperimentLines.Length; i++)
            {
                var y = i * 38;
                recipeExperimentLines[i] = RecipeLabel("Experiment Line " + (i + 1), experimentRows, 0, y, 415, 34, "", 27);
                recipeExperimentLines[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                RecipeInkLine("Experiment Writing Rule " + (i + 1), experimentRows,
                    new Vector2(-2, y + 34), new Vector2(413, y + 33), new Color(.47f, .31f, .16f, .38f), 1.1f);
            }
            RecipeConclusionMark(bookLeftPage);
            recipeConclusion = RecipeLabel("Recipe Conclusion", bookLeftPage, 235, 811, 376, 45, "", 25, TextAnchor.MiddleLeft, false, T.Red);
            recipeConclusion.horizontalOverflow = HorizontalWrapMode.Overflow;
            recipeLetter = RecipeLabel("Recipe Mother Letter", bookLeftPage, 661, 707, 280, 172, "", 25, TextAnchor.UpperLeft);
            recipeOpening.rectTransform.localRotation = Quaternion.Euler(0, 0, 3);
            recipeLetter.rectTransform.localRotation = Quaternion.Euler(0, 0, 7);

            bookRightPage = T.At("Recipe Right Page", staticContent, 1044, 62, 628, 828);
            RecipeLabel("Research Information Heading", bookRightPage, 76, 2, 425, 65, "연구 정보", 44, TextAnchor.MiddleCenter, brush: true);
            recipeName = RecipeLabel("Recipe Name", bookRightPage, 31, 96, 320, 89, "", 68, brush: true);
            var description = RecipeLabel("Recipe Description Label", bookRightPage, 17, 201, 311, 108, "", 25, TextAnchor.UpperLeft);
            description.text = "새로운 배합을 연구해\n내일의 떡을 준비해요.";
            RecipeLabel("Recipe Cost Label", bookRightPage, 83, 323, 154, 50, "연구비", 29);
            recipeCost = RecipeLabel("Recipe Cost", bookRightPage, 246, 323, 352, 50, "", 31);
            RecipeLabel("Recipe Prerequisite Label", bookRightPage, 83, 385, 154, 50, "필요 조건", 29);
            recipePrerequisite = RecipeLabel("Recipe Prerequisite", bookRightPage, 246, 385, 352, 50, "", 29);
            RecipeLabel("Recipe Result Label", bookRightPage, 83, 457, 154, 60, "연구 결과", 29);
            recipeResult = RecipeLabel("Recipe Result", bookRightPage, 246, 451, 373, 85, "", 26);
            recipeResult.supportRichText = true;
            RecipeLabel("Recipe Timing Label", bookRightPage, 83, 541, 154, 52, "적용 시점", 29);
            RecipeLabel("Recipe Timing", bookRightPage, 246, 541, 352, 52, "다음 영업일", 29);
            RecipeLabel("Recipe Evening Action Label", bookRightPage, 83, 605, 154, 52, "소모", 29);
            RecipeLabel("Recipe Evening Action", bookRightPage, 246, 605, 352, 52, "저녁 행동 1회", 29);
            recipeStatus = T.Label("Recipe Status", bookRightPage, 11, 665, 610, 25, "", 17, T.Red, TextAnchor.MiddleCenter);
            recipeStart = T.Button("Recipe Start Research", bookRightPage, 43, 714, 561, 99, "연구 시작", T.Hex("172D42"), () => RequestResearchConfirmation(ResearchKind.Cake), 48, T.Hex("E6BB72"));
            var bookIcon = T.At("Original Gold Book Icon", recipeStart.transform, 92, 23, 63, 63);
            var bookIconImage = T.Paint(bookIcon, Color.white, 0);
            bookIconImage.type = Image.Type.Simple;
            bookIconImage.sprite = RecipeImageCrop(originalTexture, 1253, 866, 63, 63, "Original golden recipe book icon");
            var caption = recipeStart.GetComponentInChildren<Text>();
            caption.font = RecipeBrushTitle;
            caption.fontSize = 58;
            caption.fontStyle = FontStyle.Normal;
            caption.rectTransform.anchorMin = caption.rectTransform.anchorMax = new Vector2(0, 1);
            caption.rectTransform.pivot = new Vector2(0, 1);
            caption.rectTransform.anchoredPosition = new Vector2(140, -11);
            caption.rectTransform.sizeDelta = new Vector2(292, 77);
            var buttonColors = recipeStart.colors;
            buttonColors.highlightedColor = new Color(1.1f, 1.08f, 1.03f);
            // This sprite includes the photographed parchment corners; tint only the caption when locked.
            buttonColors.disabledColor = Color.white;
            recipeStart.colors = buttonColors;

            recipePageGroup = reference.gameObject.AddComponent<CanvasGroup>();
            recipePrevious = CornerPageButton(reference, "Recipe Previous Page", 117, 820, -1);
            recipeNext = CornerPageButton(reference, "Recipe Next Page", 1638, 820, 1);
            recipePageNumber = T.Label("Recipe Page Number", recipeBook, 825, 1041, 270, 30, "", 22, T.LightPaper, TextAnchor.MiddleCenter);
            recipePageNumber.font = RecipeHandwriting;

            turnSnapshotRoot = T.Rect("Actual Recipe Pages During Turn", reference);
            T.Fill(turnSnapshotRoot);
            turnUnderPage = SnapshotImage("Underlying Recipe Spread", turnSnapshotRoot, 0, 0, 1818, 923);
            turnStationaryPage = SnapshotImage("Stationary Recipe Page", turnSnapshotRoot, 100, 20, 878, 872);
            turnSnapshotRoot.gameObject.SetActive(false);

            // The fold is anchored to the photographed book's binding, in the same reference coordinates.
            flipRoot = T.At("Turning Recipe Page", reference, 978, 20, 878, 872);
            flipRoot.pivot = new Vector2(0, .5f);
            flipRoot.anchoredPosition = new Vector2(978, -456);
            var shadow = T.Rect("Fold Shadow", flipRoot);
            HingeGraphic(shadow);
            turningShadow = shadow.gameObject.AddComponent<RecipePageTurnGraphic>();
            turningShadow.IsShadow = true;
            turningShadow.raycastTarget = false;
            var paper = T.Rect("Curling Paper", flipRoot);
            HingeGraphic(paper);
            turningPaper = paper.gameObject.AddComponent<RecipePageTurnGraphic>();
            turningPaper.raycastTarget = false;
            flipRoot.gameObject.SetActive(false);
            recipeBookBack = T.Button("Evening Back", recipeBook, 28, 153, 182, 45, "‹ 뒤로 가기", T.Hex("172D42"), EveningBackAction, 21, T.Hex("E6BB72"));
            recipeBookBack.gameObject.AddComponent<CanvasGroup>();
        }

        private Button CornerPageButton(Transform parent, string name, float x, float y, int direction)
        {
            var rect = T.At(name, parent, x, y, 101, 77);
            // Inactive book controls must serialize their renderer before their Graphic is enabled.
            rect.gameObject.AddComponent<CanvasRenderer>();
            var fold = rect.gameObject.AddComponent<RecipePageCornerGraphic>();
            fold.Direction = direction;
            fold.raycastTarget = true;
            rect.gameObject.AddComponent<CanvasGroup>();
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = fold;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.08f, 1.06f, 1.02f);
            colors.selectedColor = Color.white;
            colors.pressedColor = new Color(.88f, .83f, .75f);
            colors.disabledColor = new Color(1, 1, 1, .22f);
            colors.fadeDuration = .15f;
            button.colors = colors;
            button.onClick.AddListener(() => TurnRecipePage(direction));
            return button;
        }

        private RawImage SnapshotImage(string name, Transform parent, float x, float y, float width, float height)
        {
            var image = T.At(name, parent, x, y, width, height).gameObject.AddComponent<RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static void HingeGraphic(RectTransform graphic)
        {
            graphic.gameObject.AddComponent<CanvasRenderer>();
            graphic.anchorMin = graphic.anchorMax = new Vector2(0, .5f);
            graphic.pivot = new Vector2(0, .5f);
            graphic.anchoredPosition = Vector2.zero;
            graphic.sizeDelta = new Vector2(878, 872);
        }

        private void BuildStandResearch()
        {
            standResearch = EveningPanel("Stand Research Screen");
            EveningBack(standResearch);
            var panel = Board("Stand Research Board", standResearch, 349, 213, 1222, 791, T.DarkWood, T.Paper, 15);
            T.Label("Stand Research Title", panel, 51, 35, 1120, 63, "떡을 놓을 자리를 넓히자", 42, T.Ink, TextAnchor.MiddleCenter, true, true);
            T.Label("Stand Research Subtitle", panel, 60, 104, 1102, 56, "영업을 마친 저녁에 손보고, 다음 날 아침부터 사용해요.", 24, T.MutedInk, TextAnchor.MiddleCenter);
            var tiers = T.At("Stand Tier Information", panel, 66, 185, 1090, 202);
            T.Panel("First Stand Tier", tiers, 0, 0, 525, 202, T.LightPaper, 8);
            T.Panel("Second Stand Tier", tiers, 550, 0, 540, 202, T.LightPaper, 8);
            T.Label("First Tier Heading", tiers, 24, 20, 477, 38, "1차 가판대 연구", 28, T.Green, TextAnchor.MiddleLeft, true, true);
            T.Label("First Tier Detail", tiers, 24, 70, 477, 116, "평판 15 이상 · 회당 150,000원\n30 → 40 → 50팩\n10팩씩, 총 두 번 확장", 23, T.Ink);
            T.Label("Second Tier Heading", tiers, 574, 20, 492, 38, "2차 가판대 연구", 28, T.Green, TextAnchor.MiddleLeft, true, true);
            T.Label("Second Tier Detail", tiers, 574, 70, 492, 116, "평판 35 이상 · 회당 400,000원\n50 → 60 → 70팩\n1차 확장 두 번 완료 필요", 23, T.Ink);
            standCapacity = StandInfoRow(panel, "Stand Capacity", 417, "이번 확장");
            standCost = StandInfoRow(panel, "Stand Cost", 481, "연구비");
            standRequirement = StandInfoRow(panel, "Stand Requirement", 545, "필요 조건");
            standStatus = T.Label("Stand Status", panel, 66, 622, 1090, 46, "", 23, T.Red, TextAnchor.MiddleCenter);
            standStart = T.Button("Stand Start Research", panel, 253, 689, 716, 70, "가판대 연구 시작", T.Green, () => RequestResearchConfirmation(ResearchKind.Stand), 29, T.LightPaper);
        }

        private Text StandInfoRow(Transform parent, string name, float y, string label)
        {
            T.Label(name + " Label", parent, 93, y, 250, 48, label, 25, T.MutedInk, TextAnchor.MiddleLeft, true);
            T.Panel(name + " Rule", parent, 82, y + 54, 1060, 1, T.Gold, 0);
            return T.Label(name, parent, 368, y, 741, 48, "", 27, T.Ink);
        }

        private void BuildResearchConfirmation()
        {
            researchConfirmation = T.Rect("Research Cost Confirmation", eveningLayer);
            T.Fill(researchConfirmation);
            var shade = T.Paint(researchConfirmation, new Color(.10f, .085f, .06f, .72f), 0);
            shade.raycastTarget = true;
            var card = Board("Research Confirmation Card", researchConfirmation, 487, 301, 946, 508, T.DarkWood, T.LightPaper, 15);
            confirmationTitle = T.Label("Research Confirmation Title", card, 44, 28, 858, 58, "연구를 시작할까요?", 38, T.Ink, TextAnchor.MiddleCenter, true, true);
            confirmationBody = T.Label("Research Confirmation Body", card, 69, 104, 808, 127, "", 26, T.Ink, TextAnchor.MiddleCenter);
            confirmationCost = T.Label("Research Confirmation Cost", card, 69, 251, 808, 68, "", 30, T.Green, TextAnchor.MiddleCenter, true);
            T.Label("Research Confirmation Hint", card, 69, 326, 808, 36, "확정하면 비용이 차감되고, 다음 날 아침에 완료돼요.", 21, T.MutedInk, TextAnchor.MiddleCenter);
            T.Button("Research Confirm Back", card, 52, 401, 360, 68, "뒤로 가기", T.Hex("E6D1AF"), EveningBackAction, 26);
            confirmationStart = T.Button("Research Confirm Start", card, 432, 401, 462, 68, "연구 시작 확정", T.Green, ConfirmSelectedResearch, 27, T.LightPaper);
            researchConfirmation.gameObject.SetActive(false);
        }

        private void RefreshEvening(ShopModel state)
        {
            if (eveningLayer == null) return;
            var step = state.Evening;
            var visible = step != EveningStep.None;
            if (step != lastEvening)
            {
                if (lastEvening != (EveningStep)(-1) || step != EveningStep.None) CloseDialog();
                researchConfirmation.gameObject.SetActive(false);
                confirmationKind = ResearchKind.None;
                StopRecipeTurn();
                if (step == EveningStep.CakeResearch)
                {
                    recipePage = 0;
                    for (int i = 0; i < RecipeBookContent.Entries.Length; i++)
                    {
                        if (!state.UnlockedProducts[RecipeBookContent.Entries[i].ProductIndex]) { recipePage = i; break; }
                    }
                }
                lastEvening = step;
            }
            eveningLayer.gameObject.SetActive(visible);
            if (visible)
            {
                productionLayer.gameObject.SetActive(false);
                customerLayer.gameObject.SetActive(false);
                dateHint.text = step == EveningStep.MorningNotice ? "새로운 아침 · 연구 완료" : "영업 마감 · 내일을 준비해요";
            }
            eveningPrompt.gameObject.SetActive(step == EveningStep.Prompt);
            eveningChoice.gameObject.SetActive(step == EveningStep.Choice);
            recipeBook.gameObject.SetActive(step == EveningStep.CakeResearch);
            standResearch.gameObject.SetActive(step == EveningStep.StandResearch);
            restConfirmation.gameObject.SetActive(step == EveningStep.RestConfirmation);
            morningNotice.gameObject.SetActive(step == EveningStep.MorningNotice);
            if (step == EveningStep.CakeResearch) RefreshRecipePage(state);
            if (step == EveningStep.StandResearch) RefreshStandResearch(state);
            if (step == EveningStep.MorningNotice)
            {
                morningMessage.text = state.MorningMessage;
                // The model's completion message is authoritative; the effect follows the newly applied state.
                morningEffect.text = state.MorningMessage != null && state.MorningMessage.Contains("판매대")
                    ? "가판대 수용량 " + Number(state.Capacity) + "팩 · 오늘부터 더 넉넉하게"
                    : LatestResearchedName(state) + " · 오늘부터 생산하고 판매할 수 있어요";
            }
        }

        private string LatestResearchedName(ShopModel state)
        {
            for (int i = 8; i >= 5; i--) if (state.UnlockedProducts[i]) return ShopModel.Catalog[i].Name;
            return "새로운 떡";
        }

        private void RefreshRecipePage(ShopModel state)
        {
            var entry = RecipeBookContent.Entries[recipePage];
            var product = ShopModel.Catalog[entry.ProductIndex];
            recipeTitle.text = recipeName.text = product.Name;
            recipeSubtitle.text = entry.Subtitle;
            recipeOpening.text = entry.Opening;
            recipeIngredients.text = entry.Ingredients;
            var experiments = entry.Experiments.Split('\n');
            for (int i = 0; i < recipeExperimentLines.Length; i++)
                recipeExperimentLines[i].text = i < experiments.Length ? experiments[i] : "";
            recipeConclusion.text = entry.Conclusion;
            recipeLetter.text = entry.Letter;
            recipeCost.text = Won(state.GetCakeResearchCost(entry.ProductIndex));
            recipePrerequisite.text = recipePage == 0 ? "필요 조건 없음" : ShopModel.Catalog[entry.ProductIndex - 1].Name + " 연구 완료";
            recipeResult.text = "<size=30>판매 해금</size>\n판매가 " + Won(product.SellPrice) + " / 생산 원가 " + Won(product.Cost);
            recipeResult.fontSize = 26;
            recipePageNumber.text = (recipePage + 1) + " / " + RecipeBookContent.Entries.Length;
            var unlocked = state.UnlockedProducts[entry.ProductIndex];
            var error = state.GetCakeResearchError(entry.ProductIndex);
            recipeStatus.text = unlocked ? "이미 연구를 마친 레시피입니다." : error ?? "오늘 밤, 새로운 배합을 찾아볼까요?";
            recipeStatus.color = unlocked || error == null ? T.Green : T.Red;
            recipeStart.interactable = model != null && !recipeTurning && error == null;
            var researchCaption = recipeStart.GetComponentInChildren<Text>();
            researchCaption.text = unlocked ? "연구 완료" : "연구 시작";
            researchCaption.color = recipeStart.interactable ? T.Hex("E6BB72") : T.Hex("AAA696");
            recipePrevious.interactable = !recipeTurning && recipePage > 0;
            recipeNext.interactable = !recipeTurning && recipePage < RecipeBookContent.Entries.Length - 1;
            recipeLiveLeft.gameObject.SetActive(recipePage != 0 || recipeOriginalLeft.sprite == null);
            recipeOriginalLeft.gameObject.SetActive(recipePage == 0 && recipeOriginalLeft.sprite != null);
            var sourceTexture = Resources.Load<Texture2D>(RecipeSpreadResources[recipePage]);
            if (sourceTexture == null) sourceTexture = Resources.Load<Texture2D>("Shop/RecipeBookReference");
            if (recipeSceneSprites[recipePage] == null)
                recipeSceneSprites[recipePage] = RecipeImageCrop(sourceTexture, 0, 100, 1818, 923, "Recipe spread " + recipePage);
            recipeSceneImage.sprite = recipeSceneSprites[recipePage];
            recipeSceneImage.color = sourceTexture == null ? T.Hex("CDB08B") : Color.white;
            if (recipeButtonSprites[recipePage] == null)
            {
                var buttonTexture = Resources.Load<Texture2D>("Shop/RecipeResearchButtonSource");
                if (buttonTexture == null) buttonTexture = sourceTexture;
                recipeButtonSprites[recipePage] = RecipeImageCrop(buttonTexture, 1087, 847, 561, 101, "Navy and gold research button " + recipePage);
            }
            var startImage = recipeStart.GetComponent<Image>();
            startImage.sprite = recipeButtonSprites[recipePage];
            startImage.type = Image.Type.Simple;
            startImage.color = Color.white;
            var description = bookRightPage.Find("Recipe Description Label").GetComponent<Text>();
            description.text = entry.ResearchDescription;
        }

        private void RefreshStandResearch(ShopModel state)
        {
            var maximum = state.Capacity >= 70;
            standCapacity.text = maximum ? Number(state.Capacity) + "팩 · 모든 확장 완료" : Number(state.Capacity) + " → " + Number(state.GetStandResearchTargetCapacity()) + "팩";
            standCost.text = maximum ? "추가 연구 없음" : Won(state.GetStandResearchCost());
            standRequirement.text = maximum ? "최대 70팩까지 진열할 수 있어요" : state.Capacity < 50 ? "평판 15 이상" : "평판 35 이상 · 1차 확장 두 번 완료";
            var error = state.GetStandResearchError();
            standStatus.text = error ?? "오늘 밤 손보면, 내일부터 10팩 더 놓을 수 있어요.";
            standStatus.color = error == null || maximum ? T.Green : T.Red;
            standStart.interactable = model != null && error == null;
            standStart.GetComponentInChildren<Text>().text = maximum ? "모든 확장 완료" : "가판대 연구 시작";
        }

        private void RequestResearchConfirmation(ResearchKind kind)
        {
            if (model == null || recipeTurning) return;
            var index = RecipeBookContent.Entries[recipePage].ProductIndex;
            var error = kind == ResearchKind.Cake ? model.GetCakeResearchError(index) : model.GetStandResearchError();
            if (error != null) { Notify(error); return; }
            confirmationKind = kind;
            confirmationProductIndex = index;
            confirmationTitle.text = kind == ResearchKind.Cake ? ShopModel.Catalog[index].Name + " 연구" : "가판대 확장 연구";
            confirmationBody.text = kind == ResearchKind.Cake
                ? "오늘 밤 새로운 레시피를 연구해요.\n다음 날 아침부터 이 떡을 생산하고 판매할 수 있어요."
                : "오늘 밤 가판대를 손봐요.\n다음 날 아침 수용량이 " + Number(model.Capacity) + "팩에서 " + Number(model.GetStandResearchTargetCapacity()) + "팩으로 늘어나요.";
            confirmationCost.text = "차감할 연구비  " + Won(kind == ResearchKind.Cake ? model.GetCakeResearchCost(index) : model.GetStandResearchCost());
            confirmationStart.interactable = true;
            researchConfirmation.gameObject.SetActive(true);
            researchConfirmation.SetAsLastSibling();
        }

        private void ConfirmSelectedResearch()
        {
            if (model == null || confirmationKind == ResearchKind.None) return;
            var kind = confirmationKind;
            var index = confirmationProductIndex;
            string error;
            var success = kind == ResearchKind.Cake
                ? model.TryScheduleCakeResearch(index, out error)
                : model.TryScheduleStandResearch(out error);
            if (!success) { Notify(error); researchConfirmation.gameObject.SetActive(false); return; }
            confirmationKind = ResearchKind.None;
            researchConfirmation.gameObject.SetActive(false);
            model.NextDay();
        }

        private void TurnRecipePage(int direction)
        {
            if (model == null || model.Evening != EveningStep.CakeResearch || recipeTurning || researchConfirmation.gameObject.activeSelf) return;
            var next = recipePage + direction;
            if (next < 0 || next >= RecipeBookContent.Entries.Length) return;
            PrepareRecipeSnapshots(next, direction);
            nextRecipePage = next;
            recipeTurning = true;
            recipeTurnRebound = false;
            recipeTurnProgress = 0;
            recipeTurnStarted = Time.unscaledTime;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            verificationTurnProgress = null;
#endif
            turningPaper.Direction = turningShadow.Direction = direction;
            turningPaper.Progress = turningShadow.Progress = 0;
            turningPaper.StartWidth = turningShadow.StartWidth = direction > 0 ? 762 : 878;
            turningPaper.EndWidth = turningShadow.EndWidth = direction > 0 ? 878 : 762;
            turningPaper.StartTopInset = turningShadow.StartTopInset = direction > 0 ? .07f : .12f;
            turningPaper.EndTopInset = turningShadow.EndTopInset = direction > 0 ? .12f : .07f;
            turningPaper.FrontTexture = turnOldSnapshot;
            turningPaper.BackTexture = turnNewSnapshot;
            turningPaper.FrontUv = direction > 0 ? turnRightUv : turnLeftUv;
            turningPaper.BackUv = direction > 0 ? turnLeftUv : turnRightUv;
            flipRoot.gameObject.SetActive(true);
            turnSnapshotRoot.gameObject.SetActive(true);
            recipeStaticGroup.alpha = 0;
            recipePageGroup.interactable = false;
            recipePageGroup.blocksRaycasts = false;
            recipeStart.interactable = false;
            recipePrevious.interactable = recipeNext.interactable = false;
        }

        private void PrepareRecipeSnapshots(int next, int direction)
        {
            var current = recipePage;
            var previousCorner = recipePrevious.GetComponent<CanvasGroup>();
            var nextCorner = recipeNext.GetComponent<CanvasGroup>();
            var backGroup = recipeBookBack.GetComponent<CanvasGroup>();
            previousCorner.alpha = nextCorner.alpha = 0;
            backGroup.alpha = 0;
            recipePageNumber.gameObject.SetActive(false);
            try
            {
                turnOldSnapshot = CaptureRecipeSnapshot();
                recipePage = next;
                RefreshRecipePage(model);
                turnNewSnapshot = CaptureRecipeSnapshot();
            }
            finally
            {
                recipePage = current;
                RefreshRecipePage(model);
                previousCorner.alpha = nextCorner.alpha = 1;
                backGroup.alpha = 1;
                recipePageNumber.gameObject.SetActive(true);
            }
            turnUnderPage.texture = turnNewSnapshot;
            turnUnderPage.uvRect = turnSpreadUv;
            turnStationaryPage.texture = turnOldSnapshot;
            turnStationaryPage.uvRect = direction > 0 ? turnLeftUv : turnRightUv;
            var rect = turnStationaryPage.rectTransform;
            rect.anchoredPosition = new Vector2(direction > 0 ? 100 : 978, -20);
            rect.sizeDelta = new Vector2(direction > 0 ? 878 : 762, 872);
        }

        private Texture2D CaptureRecipeSnapshot()
        {
            // Capture the already rendered photograph AND live text as one surface.
            // The same URP offscreen path is used by development visual verification.
            var canvas = canvasRect.GetComponent<Canvas>();
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Recipe page capture requires the scene camera.");
            var oldMode = canvas.renderMode;
            var oldCamera = canvas.worldCamera;
            var oldPlane = canvas.planeDistance;
            var oldTarget = camera.targetTexture;
            var oldActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(Screen.width, Screen.height, 24, RenderTextureFormat.ARGB32);
            Texture2D snapshot = null;
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1;
                Canvas.ForceUpdateCanvases();
                turnLeftUv = RecipeSnapshotUv(camera, 100, 20, 878, 872, target.width, target.height);
                turnRightUv = RecipeSnapshotUv(camera, 978, 20, 762, 872, target.width, target.height);
                turnSpreadUv = RecipeSnapshotUv(camera, 0, 0, 1818, 923, target.width, target.height);
                var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                if (!RenderPipeline.SupportsRenderRequest(camera, request))
                    throw new InvalidOperationException("The scene pipeline does not support recipe snapshots.");
                RenderPipeline.SubmitRenderRequest(camera, request);
                RenderTexture.active = target;
                snapshot = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false)
                {
                    name = "Rendered recipe page " + recipePage,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                snapshot.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                snapshot.Apply(false, true);
                return snapshot;
            }
            catch
            {
                DisposeVisual(snapshot);
                throw;
            }
            finally
            {
                RenderTexture.active = oldActive;
                camera.targetTexture = oldTarget;
                canvas.renderMode = oldMode;
                canvas.worldCamera = oldCamera;
                canvas.planeDistance = oldPlane;
                RenderTexture.ReleaseTemporary(target);
                Canvas.ForceUpdateCanvases();
            }
        }

        private Rect RecipeSnapshotUv(Camera camera, float x, float y, float width, float height, int textureWidth, int textureHeight)
        {
            var topLeft = RectTransformUtility.WorldToScreenPoint(camera, recipeReference.TransformPoint(new Vector3(x, -y, 0)));
            var bottomRight = RectTransformUtility.WorldToScreenPoint(camera, recipeReference.TransformPoint(new Vector3(x + width, -y - height, 0)));
            return new Rect(topLeft.x / textureWidth, bottomRight.y / textureHeight,
                (bottomRight.x - topLeft.x) / textureWidth, (topLeft.y - bottomRight.y) / textureHeight);
        }

        private void AnimateEvening()
        {
            if (!Application.isPlaying || flipRoot == null) return;
            if (model != null && model.Evening == EveningStep.CakeResearch && !recipeTurning
                && !researchConfirmation.gameObject.activeSelf)
            {
#if ENABLE_INPUT_SYSTEM
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.leftArrowKey.wasPressedThisFrame) TurnRecipePage(-1);
                else if (keyboard != null && keyboard.rightArrowKey.wasPressedThisFrame) TurnRecipePage(1);
#elif ENABLE_LEGACY_INPUT_MANAGER
                if (Input.GetKeyDown(KeyCode.LeftArrow)) TurnRecipePage(-1);
                else if (Input.GetKeyDown(KeyCode.RightArrow)) TurnRecipePage(1);
#endif
            }
            if (!recipeTurning) return;
            var progress = Mathf.Clamp01((Time.unscaledTime - recipeTurnStarted) / RecipeTurnDuration);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (verificationTurnProgress.HasValue) progress = verificationTurnProgress.Value;
#endif
            ApplyRecipeTurn(progress);
        }

        private void ApplyRecipeTurn(float progress)
        {
            recipeTurnProgress = Mathf.Clamp01(progress);
            var eased = progress * progress * (3 - 2 * progress);
            turningPaper.Progress = turningShadow.Progress = eased;
            if (!recipeTurnRebound && progress >= .5f)
            {
                recipePage = nextRecipePage;
                recipeTurnRebound = true;
                RefreshRecipePage(model);
            }
            if (progress >= 1)
            {
                StopRecipeTurn();
                RefreshRecipePage(model);
            }
        }

        private void StopRecipeTurn()
        {
            recipeTurning = false;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            verificationTurnProgress = null;
#endif
            if (flipRoot != null) flipRoot.gameObject.SetActive(false);
            if (turnSnapshotRoot != null) turnSnapshotRoot.gameObject.SetActive(false);
            if (recipeStaticGroup != null) recipeStaticGroup.alpha = 1;
            if (recipePageGroup != null) { recipePageGroup.interactable = true; recipePageGroup.blocksRaycasts = true; }
            if (turningPaper != null) turningPaper.FrontTexture = turningPaper.BackTexture = null;
            if (turnUnderPage != null) turnUnderPage.texture = null;
            if (turnStationaryPage != null) turnStationaryPage.texture = null;
            DisposeVisual(turnOldSnapshot);
            DisposeVisual(turnNewSnapshot);
            turnOldSnapshot = turnNewSnapshot = null;
        }
    }

}
