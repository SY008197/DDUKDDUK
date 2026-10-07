using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using T = DdukDduk.Shop.ShopTheme;

namespace DdukDduk.Shop
{
    /// <summary>
    /// Editable uGUI shop screen. BuildUI materializes the hierarchy in the editor;
    /// Bind/Initialize rebuilds transient drawing primitives and connects the runtime model.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed partial class ShopView : MonoBehaviour
    {
        [SerializeField] private bool soundEnabled = true;
        private ShopModel model;
        private RectTransform canvasRect;
        private RectTransform stage;
        private RectTransform productionLayer, customerLayer, customerEntrance, openingNotice;
        private CanvasGroup customerFade;
        private Text customerDialogue, customerGuide, customerFeedback, customerReward, resultCountdown;
        private readonly List<CustomerTrayWidgets> customerTrays = new List<CustomerTrayWidgets>();
        private readonly List<Button> tasteButtons = new List<Button>();
        private CustomerStep lastCustomerStep = CustomerStep.None;
        private string fullDialogue = "";
        private float dialogueStarted, entranceStarted, resultSecondsRemaining;
        private int shownResultDay = -1;
        private RectTransform modalLayer;
        private RectTransform toast;
        private Text toastText;
        private float toastUntil;
        private Text reputationText, gradeText, capacityNavText, dateText, dateHint, moneyText;
        private Text capacityText, capacityHint, selectionText, costText, phaseText, salesHint;
        private Image capacityFill;
        private Button clearButton, autoButton, primaryButton, soundButton, declineRecommendation;
        private Text primaryCaption;
        private readonly List<CardWidgets> cards = new List<CardWidgets>();
        private readonly List<TrayWidgets> trays = new List<TrayWidgets>();
        private readonly List<Sprite> generatedAtlasSprites = new List<Sprite>();
        private Texture2D riceCakeAtlas;
        private readonly Sprite[] riceCakeSprites = new Sprite[9];
        private Material grayscaleMaterial;
        private ScrollRect catalogScroll;

        private sealed class CardWidgets
        {
            public Image Border, Paper, CountWell;
            public Text Name, Description, Cost, Count, CountLabel;
            public Button Minus, Plus;
            public RectTransform Art;
            public bool? LastUnlocked;
        }

        private sealed class TrayWidgets
        {
            public Image Frame, Well, Label, Edge;
            public Text Name, Count, Status, Description;
            public RectTransform Art;
            public bool? LastUnlocked;
        }

        private sealed class CustomerTrayWidgets
        {
            public Image Frame, Well, Label;
            public Text Name, Count, Hint;
            public RectTransform Art;
            public Button Recommend;
            public bool? LastColored;
        }

        public ShopModel Model { get { return model; } }
        public event Action OpenDayRequested;
        public event Action SkipResultRequested;

        public void SetFlowTiming(float secondsRemaining)
        {
            resultSecondsRemaining = Mathf.Max(0, secondsRemaining);
            if (resultCountdown != null)
                resultCountdown.text = "연구 선택까지 " + Mathf.CeilToInt(resultSecondsRemaining) + "초";
        }

        public void Initialize(ShopModel shopModel) { Bind(shopModel); }

        public void Bind(ShopModel shopModel)
        {
            if (shopModel == null) throw new ArgumentNullException(nameof(shopModel));
            if (model != null) model.Changed -= Refresh;
            model = shopModel;
            BuildUI();
            model.Changed += Refresh;
            Refresh();
            if (model.GetPhase() == GamePhase.Closed && model.Evening == EveningStep.None && model.Customer == null && model.LastSale != null)
            {
                shownResultDay = model.GetDay();
                ShowSaleResult();
            }
        }

        public void BuildUI()
        {
            StopRecipeTurn();
            cards.Clear();
            trays.Clear();
            customerTrays.Clear();
            tasteButtons.Clear();
            lastCustomerStep = CustomerStep.None;
            fullDialogue = "";
            shownResultDay = -1;
            foreach (var sprite in generatedAtlasSprites) DisposeVisual(sprite);
            generatedAtlasSprites.Clear();
            Array.Clear(riceCakeSprites, 0, riceCakeSprites.Length);
            DisposeVisual(grayscaleMaterial);
            var grayscaleShader = Resources.Load<Shader>("Shop/Grayscale");
            if (grayscaleShader != null)
                grayscaleMaterial = new Material(grayscaleShader) { name = "Locked recipe grayscale", hideFlags = HideFlags.HideAndDontSave };
            for (var i = transform.childCount - 1; i >= 0; i--) DisposeVisual(transform.GetChild(i).gameObject);

            riceCakeAtlas = Resources.Load<Texture2D>("Shop/RiceCakeAtlas");
            canvasRect = T.Rect("Shop Canvas", transform);
            var canvas = canvasRect.gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;
            var scaler = canvasRect.gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            scaler.referencePixelsPerUnit = 100;
            canvasRect.gameObject.AddComponent<GraphicRaycaster>();

            BuildBackground();
            stage = T.Rect("Shop Screen · 1920 × 1080", canvasRect);
            stage.anchorMin = stage.anchorMax = new Vector2(.5f, .5f);
            stage.pivot = new Vector2(.5f, .5f);
            stage.sizeDelta = new Vector2(1920, 1080);
            BuildNavigation();
            productionLayer = T.Rect("Production Screen", stage);
            T.Fill(productionLayer);
            BuildCatalog();
            BuildDisplay();
            customerLayer = T.Rect("Customer Screen", stage);
            T.Fill(customerLayer);
            BuildCustomerScreen();
            customerLayer.gameObject.SetActive(false);
            BuildEveningScreens();

            modalLayer = T.Rect("Dialogs", stage);
            T.Fill(modalLayer);
            toast = T.At("Notice", stage, 548, 1004, 824, 54);
            T.Paint(toast, new Color(.19f, .23f, .17f, .96f), 12);
            toastText = T.Label("Message", toast, 18, 0, 788, 54, "", 21, T.LightPaper, TextAnchor.MiddleCenter);
            toast.gameObject.SetActive(false);
            Canvas.ForceUpdateCanvases();
            FitStage();
            Refresh();
        }

        private void OnDestroy()
        {
            StopRecipeTurn();
            if (model != null) model.Changed -= Refresh;
            foreach (var sprite in generatedAtlasSprites) DisposeVisual(sprite);
            DisposeVisual(grayscaleMaterial);
        }

        private void LateUpdate()
        {
            FitStage();
            AnimateCustomer();
            AnimateEvening();
            if (toast != null && toast.gameObject.activeSelf && Time.unscaledTime >= toastUntil)
                toast.gameObject.SetActive(false);
        }

        private void FitStage()
        {
            if (stage == null || canvasRect == null) return;
            var available = canvasRect.rect.size;
            if (available.x <= 0 || available.y <= 0) return;
            var scale = Mathf.Min(available.x / 1920f, available.y / 1080f);
            stage.localScale = Vector3.one * scale;
        }

        private void BuildBackground()
        {
            var backdrop = T.Rect("Shop Background", canvasRect);
            T.Fill(backdrop);
            var image = T.Paint(backdrop, T.Hex("A37C4C"), 0);
            var texture = Resources.Load<Texture2D>("Shop/ShopBackground");
            if (texture != null)
            {
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f, .5f));
                sprite.name = "Shop background";
                generatedAtlasSprites.Add(sprite);
                image.sprite = sprite;
                image.type = Image.Type.Simple;
                image.color = Color.white;
                var fitter = backdrop.gameObject.AddComponent<AspectRatioFitter>();
                fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fitter.aspectRatio = texture.width / (float)texture.height;
            }
            var wash = T.Rect("Warm backdrop wash", canvasRect);
            T.Fill(wash);
            T.Paint(wash, new Color(.16f, .1f, .055f, .17f), 0);
        }

        private void BuildNavigation()
        {
            var nav = T.At("Top Navigation", stage, 52, 33, 1816, 106);
            Board("Shop Sign", nav, 0, 0, 306, 106, T.DarkWood, T.Green, 8);
            T.Label("Shop name", nav, 23, 8, 262, 54, "떡방앗간", 39, T.LightPaper, TextAnchor.MiddleCenter, true, true);
            T.Label("Shop motto", nav, 13, 64, 280, 27, "오늘도 정성으로 빚은 떡", 17, T.Hex("E4D6B8"), TextAnchor.MiddleCenter);

            var rep = NavCard(nav, "Reputation", 318, 254);
            T.Label("Label", rep, 20, 12, 214, 23, "평판", 17, T.MutedInk);
            reputationText = T.Label("Score", rep, 20, 34, 56, 47, "0", 34, T.Green, TextAnchor.MiddleLeft, true);
            gradeText = T.Label("Grade", rep, 79, 39, 153, 36, "동네 떡집", 22, T.Ink, TextAnchor.MiddleLeft, true);

            var capacity = NavCard(nav, "Capacity Summary", 584, 312);
            var capacityButton = capacity.gameObject.AddComponent<Button>();
            var capacityImage = capacity.GetComponent<Image>();
            capacityImage.raycastTarget = true;
            capacityButton.targetGraphic = capacityImage;
            capacityButton.onClick.AddListener(ShowCapacityInfo);
            T.Label("Title", capacity, 20, 10, 272, 30, "가판대", 24, T.Ink, TextAnchor.MiddleLeft, true);
            capacityNavText = T.Label("Capacity", capacity, 20, 43, 272, 34, "수용량 30개", 25, T.Green, TextAnchor.MiddleLeft, true);
            T.Label("Open hint", capacity, 20, 77, 272, 17, "매대 정보 보기", 14, T.MutedInk);

            var day = NavCard(nav, "Date", 908, 237);
            T.Label("Day label", day, 20, 10, 197, 23, "오늘의 영업", 17, T.MutedInk);
            dateText = T.Label("Day", day, 20, 34, 197, 40, "1일차", 31, T.Ink, TextAnchor.MiddleLeft, true);
            dateHint = T.Label("Phase", day, 20, 76, 197, 18, "떡을 준비할 시간", 14, T.MutedInk);

            var cash = NavCard(nav, "Money", 1157, 287);
            T.Label("Money label", cash, 20, 12, 247, 23, "보유 금액", 17, T.MutedInk);
            moneyText = T.Label("Amount", cash, 20, 37, 247, 45, "100,000원", 30, T.Ink, TextAnchor.MiddleLeft, true);
            soundButton = T.Button("Sound", nav, 1456, 0, 174, 106, "소리\n켜짐", T.LightPaper, ToggleSound, 22);
            T.Button("Settings", nav, 1642, 0, 174, 106, "가게\n설정", T.LightPaper, ShowSettings, 22);
        }

        private RectTransform NavCard(Transform parent, string name, float x, float width)
        {
            Board(name + " Shadow", parent, x, 4, width, 104, new Color(.18f, .1f, .04f, .3f), T.DarkWood, 8);
            var image = T.Panel(name, parent, x, 0, width, 102, T.Paper, 8);
            return image.rectTransform;
        }

        private void BuildCatalog()
        {
            var board = Board("Available Rice Cakes", productionLayer, 52, 169, 610, 850, T.DarkWood, T.Paper, 12);
            T.Label("Title", board, 29, 18, 550, 46, "배치 가능한 떡", 34, T.Ink, TextAnchor.MiddleLeft, true, true);
            T.Label("Help", board, 29, 66, 550, 28, "가판대에 진열할 떡의 수량을 골라주세요.", 19, T.MutedInk);
            T.Panel("Header rule", board, 25, 104, 559, 2, T.Gold, 0);

            var scrollRoot = T.At("Rice Cake Catalog · Scroll", board, 19, 115, 573, 712);
            catalogScroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            catalogScroll.horizontal = false;
            catalogScroll.vertical = true;
            catalogScroll.movementType = ScrollRect.MovementType.Clamped;
            catalogScroll.inertia = true;
            catalogScroll.decelerationRate = .12f;
            catalogScroll.scrollSensitivity = 42;
            var viewport = T.At("Viewport", scrollRoot, 0, 0, 552, 712);
            viewport.gameObject.AddComponent<RectMask2D>();
            var viewportImage = T.Paint(viewport, new Color(1, 1, 1, .001f), 0);
            viewportImage.raycastTarget = true;
            var content = T.Rect("All Nine Rice Cakes", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0, ShopModel.Catalog.Count * 182 - 12);
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            catalogScroll.viewport = viewport;
            catalogScroll.content = content;

            var track = T.Panel("Catalog Scrollbar", scrollRoot, 562, 0, 11, 712, T.Hex("D7C5A5"), 5);
            track.raycastTarget = true;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            var slide = T.Rect("Sliding Area", track.transform);
            T.Fill(slide, 1, 1, 1, 1);
            var handle = T.Rect("Handle", slide);
            T.Fill(handle);
            var handleImage = T.Paint(handle, T.Green, 5);
            handleImage.raycastTarget = true;
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.size = .43f;
            scrollbar.value = 1;
            catalogScroll.verticalScrollbar = scrollbar;
            catalogScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            for (var index = 0; index < ShopModel.Catalog.Count; index++) BuildCard(content, index);
            catalogScroll.verticalNormalizedPosition = 1;
        }

        private void BuildCard(Transform parent, int index)
        {
            var product = ShopModel.Catalog[index];
            var rect = T.Rect(product.Name + " · Quantity Card", parent);
            rect.sizeDelta = new Vector2(552, 170);
            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 170;
            var card = new CardWidgets();
            card.Border = T.Paint(rect, T.Hex("CFAE7D"), 11);
            var paper = T.Rect("Parchment", rect);
            T.Fill(paper, 2, 2, 2, 2);
            card.Paper = T.Paint(paper, T.LightPaper, 10);
            card.Art = T.At("Product Artwork", rect, 14, 18, 118, 134);
            card.Name = T.Label("Name", rect, 147, 13, 245, 42, product.Name, 27, T.Ink, TextAnchor.MiddleLeft, true, true);
            card.Description = T.Label("Description", rect, 147, 57, 235, 54, product.Description, 18, T.MutedInk);
            card.Cost = T.Label("Production Cost", rect, 147, 115, 243, 39, "생산비 " + Won(product.Cost), 18, T.MutedInk);
            card.CountLabel = T.Label("Quantity Label", rect, 399, 23, 139, 25, "진열 수량", 16, T.MutedInk, TextAnchor.MiddleCenter);
            card.Minus = T.Button("Decrease", rect, 397, 62, 42, 54, "−", T.Hex("E5C79C"), () => ChangeCount(index, -1), 31);
            card.CountWell = T.Panel("Count Well", rect, 441, 62, 52, 54, T.Paper, 3);
            card.Count = T.Label("Count", rect, 441, 62, 52, 54, "0", 27, T.Ink, TextAnchor.MiddleCenter, true);
            card.Count.resizeTextForBestFit = true;
            card.Count.resizeTextMinSize = 12;
            card.Count.resizeTextMaxSize = 27;
            card.Plus = T.Button("Increase", rect, 495, 62, 42, 54, "+", T.Wood, () => ChangeCount(index, 1), 31, T.LightPaper);
            cards.Add(card);
        }

        private void BuildDisplay()
        {
            var board = Board("Wooden Display Stand", productionLayer, 690, 169, 1178, 850, T.DarkWood, T.Wood, 11);
            for (var row = 0; row < 17; row++)
                T.Panel("Wood grain " + row, board, 12, 16 + row * 48, 1154, 2, new Color(.24f, .13f, .065f, .25f), 0);
            var header = Board("Stand Header", board, 13, 12, 1152, 84, T.DarkWood, T.Paper, 6);
            T.Label("Title", header, 25, 8, 392, 54, "가판대", 39, T.Ink, TextAnchor.MiddleLeft, true, true);
            phaseText = T.Label("Stand Phase", header, 25, 57, 650, 22, "오늘의 떡을 준비해 주세요.", 16, T.MutedInk);
            T.Label("Capacity Label", header, 775, 10, 140, 26, "총 진열", 21, T.Ink, TextAnchor.MiddleLeft, true);
            capacityText = T.Label("Capacity Count", header, 911, 9, 214, 31, "0 / 30", 26, T.Green, TextAnchor.MiddleRight, true);
            T.Panel("Capacity track", header, 775, 47, 350, 12, T.Hex("D4C0A0"), 6);
            var fill = T.Panel("Capacity fill", header, 775, 47, 350, 12, T.Green, 6);
            capacityFill = fill;
            capacityHint = T.Label("Capacity Hint", header, 775, 62, 350, 18, "30개를 더 진열할 수 있어요.", 14, T.MutedInk, TextAnchor.MiddleRight);

            const float trayWidth = 373.3333f;
            const float trayHeight = 204;
            for (var index = 0; index < ShopModel.Catalog.Count; index++)
            {
                var col = index % 3;
                var row = index / 3;
                BuildTray(board, index, 17 + col * (trayWidth + 12), 108 + row * (trayHeight + 12), trayWidth, trayHeight);
            }

            var footer = Board("Production and Selling Controls", board, 13, 765, 1152, 72, T.DarkWood, T.Paper, 7);
            selectionText = T.Label("Selected Quantity", footer, 21, 10, 255, 30, "선택한 수량 0개", 25, T.Ink, TextAnchor.MiddleLeft, true);
            costText = T.Label("Production Cost", footer, 21, 41, 510, 24, "예상 생산비 0원", 18, T.MutedInk);
            salesHint = T.Label("Sales Range", footer, 288, 12, 287, 28, "평판에 따른 판매율 75–80%", 17, T.MutedInk);
            clearButton = T.Button("Clear", footer, 580, 11, 137, 50, "초기화", T.Hex("E4CDAA"), ClearSelection, 22);
            autoButton = T.Button("Auto Distribute", footer, 729, 11, 168, 50, "자동 배치", T.Hex("E4CDAA"), AutoDistribute, 22);
            primaryButton = T.Button("Confirm Production or Sell", footer, 909, 10, 225, 52, "배치 완료", T.Red, PrimaryAction, 24, T.LightPaper);
            primaryCaption = primaryButton.GetComponentInChildren<Text>();
        }

        private void BuildTray(Transform parent, int index, float x, float y, float width, float height)
        {
            var product = ShopModel.Catalog[index];
            var frame = T.Panel(product.Name + " · Wooden Tray", parent, x, y, width, height, T.Hex("9A6138"), 7);
            var tray = new TrayWidgets { Frame = frame };
            tray.Edge = T.Panel("Tray top edge", frame.transform, 5, 4, width - 10, 5, T.Hex("BE8653"), 2);
            tray.Well = T.Panel("Inner Wood", frame.transform, 10, 13, width - 20, 145, T.Hex("563B28"), 4);
            for (var grain = 0; grain < 3; grain++)
                T.Panel("Well grain " + grain, tray.Well.transform, 4, 20 + grain * 41, width - 28, 1, new Color(1, 1, 1, .08f), 0);
            tray.Art = T.At("Rice Cake Artwork", frame.transform, 21, 20, 150, 132);
            tray.Count = T.Label("Counted Quantity", frame.transform, 178, 37, width - 194, 53, "0개", 37, T.LightPaper, TextAnchor.MiddleCenter, true);
            tray.Count.resizeTextForBestFit = true;
            tray.Count.resizeTextMinSize = 16;
            tray.Count.resizeTextMaxSize = 37;
            tray.Status = T.Label("Quantity Status", frame.transform, 177, 19, width - 194, 22, "생산 예정", 15, T.Hex("D8C1A0"), TextAnchor.MiddleCenter);
            tray.Description = T.Label("Tray Hint", frame.transform, 177, 100, width - 194, 42, "수량을 선택해 주세요", 17, T.Hex("D8C1A0"), TextAnchor.MiddleCenter);
            tray.Label = T.Panel("Name Plaque", frame.transform, 61, 166, width - 122, 32, T.Hex("E5C395"), 5);
            tray.Name = T.Label("Product Name", tray.Label.transform, 2, 0, width - 126, 32, product.Name, 23, T.Ink, TextAnchor.MiddleCenter, true, true);
            trays.Add(tray);
        }

        private void BuildCustomerScreen()
        {
            var tasteBoard = Board("Taste Choices", customerLayer, 52, 169, 1178, 176, T.DarkWood, T.Paper, 12);
            T.Label("Taste Heading", tasteBoard, 25, 12, 1128, 36, "손님에게 어떤 맛을 추천할까요?", 27, T.Ink, TextAnchor.MiddleLeft, true, true);
            BuildTasteButton(tasteBoard, "Taste Mild", RiceCakeTaste.Mild, 25, "담백한 떡", "은은하고 담백한 맛");
            BuildTasteButton(tasteBoard, "Taste Sweet", RiceCakeTaste.Sweet, 409, "달콤한 떡", "입안에 퍼지는 달콤한 맛");
            BuildTasteButton(tasteBoard, "Taste Nutty", RiceCakeTaste.Nutty, 793, "고소한 떡", "든든하고 고소한 맛");

            var display = Board("Recommendation Display Stand", customerLayer, 52, 365, 1178, 654, T.DarkWood, T.Wood, 11);
            for (var grain = 0; grain < 12; grain++)
                T.Panel("Wood grain " + grain, display, 12, 20 + grain * 51, 1154, 2, new Color(.24f, .13f, .065f, .25f), 0);
            var header = Board("Recommendation Stand Header", display, 13, 12, 1152, 67, T.DarkWood, T.Paper, 6);
            T.Label("Title", header, 25, 6, 390, 48, "오늘의 떡 가판대", 31, T.Ink, TextAnchor.MiddleLeft, true, true);
            customerGuide = T.Label("Recommendation Guide", header, 427, 8, 694, 47,
                "맛을 고르고 마음에 드는 떡을 추천해 주세요.", 20, T.MutedInk, TextAnchor.MiddleRight);
            declineRecommendation = T.Button("Decline Empty Display", header, 812, 10, 310, 46,
                "추천 없이 마치기", T.Hex("E4CDAA"), DeclineEmptyDisplay, 22);
            declineRecommendation.gameObject.SetActive(false);
            const float width = 373.3333f;
            for (var index = 0; index < ShopModel.Catalog.Count; index++)
            {
                var product = ShopModel.Catalog[index];
                var frame = T.Panel("Recommendation Tray " + product.Id, display,
                    17 + index % 3 * (width + 12), 91 + index / 3 * 182, width, 170, T.Hex("9A6138"), 7);
                var tray = new CustomerTrayWidgets { Frame = frame };
                tray.Well = T.Panel("Inner Wood", frame.transform, 9, 10, width - 18, 121, T.Hex("563B28"), 4);
                tray.Art = T.At("Rice Cake Artwork", frame.transform, 16, 16, 142, 111);
                tray.Count = T.Label("Display Quantity", frame.transform, 166, 14, 190, 25, "진열 0개", 16, T.Hex("D8C1A0"), TextAnchor.MiddleCenter);
                tray.Hint = T.Label("Taste Restriction", frame.transform, 161, 40, 197, 38, "맛을 선택해 주세요", 17, T.LightPaper, TextAnchor.MiddleCenter);
                var capturedIndex = index;
                tray.Recommend = T.Button("Recommend " + product.Id, frame.transform, 175, 82, 178, 41,
                    "추천하기", T.Green, () => RecommendProduct(capturedIndex), 21, T.LightPaper);
                tray.Label = T.Panel("Name Plaque", frame.transform, 48, 139, width - 96, 27, T.Hex("E5C395"), 5);
                tray.Name = T.Label("Product Name", tray.Label.transform, 2, 0, width - 100, 27,
                    product.Name, 21, T.Ink, TextAnchor.MiddleCenter, true, true);
                customerTrays.Add(tray);
            }

            customerEntrance = T.At("Customer Entrance", customerLayer, 1268, 169, 600, 850);
            customerFade = customerEntrance.gameObject.AddComponent<CanvasGroup>();
            var speech = Board("Customer Speech Bubble", customerEntrance, 0, 0, 600, 257, T.DarkWood, T.LightPaper, 18);
            T.Label("Speech Heading", speech, 27, 19, 546, 31, "손님의 이야기", 24, T.Green, TextAnchor.MiddleLeft, true, true);
            T.Panel("Speech Rule", speech, 27, 61, 546, 2, T.Hex("DAC8A8"), 0);
            customerDialogue = T.Label("Customer Dialogue", speech, 29, 75, 542, 153, "", 27, T.Ink, TextAnchor.MiddleLeft);
            customerDialogue.resizeTextForBestFit = true;
            customerDialogue.resizeTextMinSize = 18;
            customerDialogue.resizeTextMaxSize = 27;
            T.Panel("Speech Tail", customerEntrance, 47, 236, 34, 34, T.LightPaper, 0).rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

            var portraitBoard = Board("Customer Portrait Board", customerEntrance, 0, 286, 600, 564, T.DarkWood, new Color(.91f, .85f, .72f, .93f), 12);
            var portraitTexture = Resources.Load<Texture2D>("Shop/CustomerPortrait");
            var portraitRect = T.At("Customer Portrait", portraitBoard, 11, 8, 578, 548);
            if (portraitTexture != null)
            {
                var sprite = Sprite.Create(portraitTexture, new Rect(0, 0, portraitTexture.width, portraitTexture.height), new Vector2(.5f, .5f));
                sprite.name = "Customer portrait";
                generatedAtlasSprites.Add(sprite);
                var portrait = portraitRect.gameObject.AddComponent<Image>();
                portrait.sprite = sprite;
                portrait.preserveAspect = true;
                portrait.raycastTarget = false;
            }
            else DrawCustomerSilhouette(portraitRect);

            var feedback = T.At("Customer Feedback", customerEntrance, 12, 732, 576, 104);
            T.Paint(feedback, new Color(.98f, .94f, .84f, .97f), 9);
            customerFeedback = T.Label("Satisfaction", feedback, 16, 10, 544, 47, "", 29, T.Green, TextAnchor.MiddleCenter, true);
            customerReward = T.Label("Reputation Reward", feedback, 16, 61, 544, 28, "", 20, T.MutedInk, TextAnchor.MiddleCenter);
            feedback.gameObject.SetActive(false);

            openingNotice = T.Rect("Sales Opening Notice", customerLayer);
            T.Fill(openingNotice);
            var veil = T.Paint(openingNotice, new Color(.13f, .09f, .045f, .62f), 0);
            veil.raycastTarget = true;
            var openingPanel = Board("Sales Started", openingNotice, 477, 417, 966, 215, T.DarkWood, T.LightPaper, 16);
            T.Label("Opening Message", openingPanel, 30, 31, 906, 73, "판매가 시작되었습니다", 43, T.Green, TextAnchor.MiddleCenter, true, true);
            T.Label("Opening Hint", openingPanel, 30, 117, 906, 48, "오늘의 손님을 맞이할 시간이에요.", 25, T.MutedInk, TextAnchor.MiddleCenter);
            openingNotice.gameObject.SetActive(false);
        }

        private void BuildTasteButton(Transform parent, string name, RiceCakeTaste taste, float x, string title, string hint)
        {
            var button = T.Button(name, parent, x, 59, 360, 92, title + "\n" + hint,
                T.Hex("E5D0AD"), () => { if (model != null) model.SelectCustomerTaste(taste); }, 25);
            tasteButtons.Add(button);
        }

        private void DrawCustomerSilhouette(RectTransform parent)
        {
            var head = T.Panel("Customer head", parent, 193, 73, 192, 218, T.Hex("E8C7AA"), 24);
            head.sprite = T.Circle;
            T.Panel("Customer hair", parent, 183, 44, 212, 133, T.Hex("51453E"), 24).sprite = T.Circle;
            T.Panel("Customer coat", parent, 126, 280, 326, 268, T.Hex("718476"), 24);
            T.Panel("Customer shirt", parent, 244, 286, 91, 262, T.LightPaper, 9);
            T.Panel("Left eye", parent, 245, 178, 10, 11, T.Ink, 5).sprite = T.Circle;
            T.Panel("Right eye", parent, 322, 178, 10, 11, T.Ink, 5).sprite = T.Circle;
        }

        private void RecommendProduct(int index)
        {
            if (model != null && !model.TryRecommendProduct(index, out var error)) Notify(error);
        }

        private void DeclineEmptyDisplay()
        {
            if (model != null && !model.TryDeclineRecommendation(out var error)) Notify(error);
        }

        private void AnimateCustomer()
        {
            if (!Application.isPlaying || model == null || model.Customer == null || !customerLayer.gameObject.activeSelf) return;
            var visit = model.Customer;
            if (visit.Step == CustomerStep.AwaitingRecommendation || visit.Step == CustomerStep.Feedback || visit.Step == CustomerStep.Result)
            {
                var progress = Mathf.Clamp01((Time.unscaledTime - entranceStarted) / .65f);
                var eased = 1 - Mathf.Pow(1 - progress, 3);
                customerFade.alpha = eased;
                customerEntrance.anchoredPosition = new Vector2(1268 + 62 * (1 - eased), -169);
                if (visit.Step == CustomerStep.AwaitingRecommendation && customerDialogue != null)
                {
                    var visible = Mathf.Min(fullDialogue.Length, Mathf.FloorToInt((Time.unscaledTime - dialogueStarted) * 42));
                    customerDialogue.text = fullDialogue.Substring(0, visible);
                }
                else customerDialogue.text = fullDialogue;
            }
        }

        private RectTransform Board(string name, Transform parent, float x, float y, float width, float height,
            Color border, Color inner, float radius)
        {
            T.Panel(name + " Shadow", parent, x + 3, y + 6, width, height, new Color(.15f, .09f, .035f, .28f), radius);
            var panel = T.Panel(name, parent, x, y, width, height, border, radius);
            var inside = T.Rect("Surface", panel.transform);
            T.Fill(inside, 6, 6, 6, 6);
            T.Paint(inside, inner, radius - 2);
            return panel.rectTransform;
        }

        public void Refresh()
        {
            if (stage == null || cards.Count != ShopModel.Catalog.Count) return;
            var state = model ?? new ShopModel();
            var arranging = state.GetPhase() == GamePhase.Arrangement;
            var count = arranging ? state.TotalSelected : state.TotalProduced;
            reputationText.text = state.GetReputation().ToString(CultureInfo.InvariantCulture);
            gradeText.text = ReputationGrade(state.GetReputation());
            capacityNavText.text = "수용량 " + Number(state.Capacity) + "개";
            dateText.text = Number(state.GetDay()) + "일차";
            moneyText.text = Won(state.GetMoney());
            var visit = state.Customer;
            dateHint.text = arranging ? "떡을 준비할 시간" : visit != null && visit.Step == CustomerStep.AwaitingRecommendation ? "손님에게 떡 추천하기" : state.GetPhase() == GamePhase.ReadyToSell ? "손님을 맞이할 시간" : "오늘의 영업 마감";
            capacityText.text = Number(count) + " / " + Number(state.Capacity);
            capacityFill.rectTransform.sizeDelta = new Vector2(350 * Mathf.Clamp01(count / (float)state.Capacity), 12);
            capacityHint.text = arranging ? Number(Mathf.Max(0, state.Capacity - count)) + "개를 더 진열할 수 있어요." : "생산된 떡 " + Number(state.TotalProduced) + "개";
            selectionText.text = (arranging ? "선택한 수량 " : "생산한 수량 ") + Number(count) + "개";
            costText.text = (arranging ? "예상 생산비 " : "사용한 생산비 ") + Won(arranging ? state.EstimatedProductionCost : state.CommittedProductionCost);
            var band = ShopModel.GetSalesBand(state.GetReputation());
            salesHint.text = "평판에 따른 판매율 " + Percent(band.Minimum) + (band.Minimum == band.Maximum ? "" : "–" + Percent(band.Maximum));
            phaseText.text = arranging ? "수량을 고른 뒤 배치를 완료해 주세요." : state.GetPhase() == GamePhase.ReadyToSell ? "생산 완료 · 이제 가게를 열 수 있어요." : "오늘의 영업을 마쳤습니다.";
            clearButton.interactable = model != null && arranging && count > 0;
            autoButton.interactable = model != null && arranging;
            primaryCaption.text = arranging ? "배치 완료" : state.GetPhase() == GamePhase.ReadyToSell ? "판매 시작" : "판매 결과";
            primaryButton.GetComponent<Image>().color = arranging ? T.Red : T.Green;
            primaryButton.interactable = model != null;
            soundButton.GetComponentInChildren<Text>().text = soundEnabled ? "소리\n켜짐" : "소리\n꺼짐";

            for (var index = 0; index < cards.Count; index++)
            {
                var unlocked = state.UnlockedProducts[index];
                var selected = arranging ? state.SelectedCounts[index] : state.ProducedCounts[index];
                RefreshCard(index, unlocked, selected, arranging);
                RefreshTray(index, unlocked, selected, state.GetPhase(), state.LastSale);
            }
            RefreshCustomer(state);
            RefreshEvening(state);
        }

        private void RefreshCustomer(ShopModel state)
        {
            if (customerLayer == null) return;
            var visit = state.Customer;
            var step = visit == null ? CustomerStep.None : visit.Step;
            var visiting = step != CustomerStep.None;
            productionLayer.gameObject.SetActive(!visiting);
            customerLayer.gameObject.SetActive(visiting);
            openingNotice.gameObject.SetActive(step == CustomerStep.Opening);
            if (step != lastCustomerStep)
            {
                CloseDialog();
                if (toast != null) toast.gameObject.SetActive(false);
                if (step == CustomerStep.AwaitingRecommendation)
                {
                    entranceStarted = dialogueStarted = Time.unscaledTime;
                    customerFade.alpha = Application.isPlaying ? 0 : 1;
                    customerEntrance.anchoredPosition = new Vector2(1268 + (Application.isPlaying ? 62 : 0), -169);
                }
                if (step == CustomerStep.None)
                {
                    shownResultDay = -1;
                    if (catalogScroll != null) catalogScroll.verticalNormalizedPosition = 1;
                }
                lastCustomerStep = step;
            }
            if (!visiting) return;
            if (fullDialogue != (visit.Dialogue ?? ""))
            {
                fullDialogue = visit.Dialogue ?? "";
                dialogueStarted = Time.unscaledTime;
            }
            if (!Application.isPlaying || step != CustomerStep.AwaitingRecommendation) customerDialogue.text = fullDialogue;
            var awaiting = step == CustomerStep.AwaitingRecommendation;
            var noRecommendation = awaiting && !state.HasRecommendableProduct;
            declineRecommendation.gameObject.SetActive(noRecommendation);
            declineRecommendation.interactable = model != null && noRecommendation;
            customerGuide.gameObject.SetActive(!noRecommendation);
            for (var index = 0; index < tasteButtons.Count; index++)
            {
                var taste = (RiceCakeTaste)(index + 1);
                var selected = visit.SelectedTaste == taste;
                tasteButtons[index].interactable = model != null && awaiting;
                tasteButtons[index].GetComponent<Image>().color = selected ? T.Green : visit.SelectedTaste == RiceCakeTaste.Unspecified ? T.Hex("E5D0AD") : T.GrayPaper;
                tasteButtons[index].GetComponentInChildren<Text>().color = selected ? T.LightPaper : T.Ink;
            }
            customerGuide.text = awaiting
                ? visit.SelectedTaste == RiceCakeTaste.Unspecified ? "맛을 고르고 마음에 드는 떡을 추천해 주세요." : TasteCaption(visit.SelectedTaste) + "을 골랐어요. 아래에서 떡을 추천해 주세요."
                : step == CustomerStep.Opening ? "손님을 맞이하고 있어요." : "오늘의 추천을 마쳤어요.";
            for (var index = 0; index < customerTrays.Count; index++)
                RefreshCustomerTray(state, index, visit.SelectedTaste, awaiting);
            var feedbackVisible = step == CustomerStep.Feedback || step == CustomerStep.Result;
            customerFeedback.transform.parent.gameObject.SetActive(feedbackVisible);
            customerFeedback.text = visit.WasSatisfied ? "손님이 만족했습니다" : "손님이 만족하지 못하였습니다";
            customerFeedback.fontSize = visit.WasSatisfied ? 29 : 26;
            customerFeedback.color = visit.WasSatisfied ? T.Green : T.Red;
            customerReward.text = visit.WasSatisfied ? "평판 +1 · 좋은 추천에 감사해요!" : "평판은 그대로 · 다음 손님을 기대해요.";
            if (step == CustomerStep.Result && state.Evening == EveningStep.None && shownResultDay != state.GetDay() && state.LastSale != null)
            {
                shownResultDay = state.GetDay();
                ShowSaleResult();
            }
        }

        private void RefreshCustomerTray(ShopModel state, int index, RiceCakeTaste selectedTaste, bool awaiting)
        {
            var product = ShopModel.Catalog[index];
            var tray = customerTrays[index];
            var unlocked = state.UnlockedProducts[index];
            var classified = product.Taste != RiceCakeTaste.Unspecified;
            var stocked = state.ProducedCounts[index] > 0;
            var matches = selectedTaste != RiceCakeTaste.Unspecified && selectedTaste == product.Taste;
            var colored = unlocked && classified && stocked && (selectedTaste == RiceCakeTaste.Unspecified || matches);
            tray.Frame.color = colored ? T.Hex("9A6138") : T.Hex("8B8B82");
            tray.Well.color = colored ? T.Hex("563B28") : T.Hex("575A55");
            tray.Label.color = colored ? T.Hex("E5C395") : T.Hex("BDBDB4");
            tray.Name.color = colored ? T.Ink : T.Hex("62655F");
            tray.Count.color = colored ? T.Hex("D8C1A0") : T.Hex("BEC1B8");
            tray.Count.text = "진열 " + Number(state.ProducedCounts[index]) + "개";
            tray.Hint.color = colored ? T.LightPaper : T.Hex("DEE0D9");
            tray.Hint.text = !unlocked ? product.Name + "은 연구가 필요합니다."
                : !classified ? "맛 분류 미정"
                : !stocked ? "진열한 떡이 없습니다"
                : selectedTaste == RiceCakeTaste.Unspecified ? "맛을 선택해 주세요"
                : !matches ? TasteCaption(selectedTaste) + "이 아닙니다."
                : "추천할 수 있어요";
            tray.Hint.fontSize = !unlocked ? 15 : 17;
            tray.Recommend.interactable = model != null && awaiting && unlocked && classified && stocked && matches;
            tray.Recommend.GetComponent<Image>().color = colored && matches ? T.Green : T.Hex("8A8D80");
            if (tray.LastColored != colored)
            {
                RebuildArt(tray.Art, index, colored);
                tray.LastColored = colored;
            }
        }

        private static string TasteCaption(RiceCakeTaste taste)
        {
            return taste == RiceCakeTaste.Mild ? "담백한 떡" : taste == RiceCakeTaste.Sweet ? "달콤한 떡" : taste == RiceCakeTaste.Nutty ? "고소한 떡" : "맛";
        }

        private void RefreshCard(int index, bool unlocked, int count, bool arranging)
        {
            var card = cards[index];
            var product = ShopModel.Catalog[index];
            card.Count.text = Number(count);
            card.CountLabel.text = arranging ? "진열 수량" : "생산 수량";
            card.Minus.interactable = model != null && unlocked && arranging && count > 0;
            card.Plus.interactable = model != null && unlocked && arranging && model.TotalSelected < model.Capacity;
            card.Border.color = unlocked ? T.Hex("CFAE7D") : T.Hex("A4A49B");
            card.Paper.color = unlocked ? T.LightPaper : T.GrayPaper;
            card.Name.color = unlocked ? T.Ink : T.GrayInk;
            card.Description.color = unlocked ? T.MutedInk : T.GrayInk;
            card.Cost.color = unlocked ? T.MutedInk : T.GrayInk;
            card.Count.color = unlocked ? T.Ink : T.GrayInk;
            card.CountWell.color = unlocked ? T.Paper : T.Hex("CACBC3");
            card.CountLabel.color = unlocked ? T.MutedInk : T.GrayInk;
            card.Description.text = unlocked ? product.Description : product.Name + "은 연구가 필요합니다.";
            card.Description.fontSize = unlocked ? 18 : 17;
            card.Cost.text = unlocked ? "생산비 " + Won(product.Cost) : "연구 후 생산할 수 있어요";
            card.Minus.GetComponent<Image>().color = unlocked ? T.Hex("E5C79C") : T.Hex("B8B8B0");
            card.Plus.GetComponent<Image>().color = unlocked ? T.Wood : T.Hex("99998F");
            card.Minus.GetComponentInChildren<Text>().color = unlocked ? T.Ink : T.GrayInk;
            card.Plus.GetComponentInChildren<Text>().color = unlocked ? T.LightPaper : T.Hex("D3D5CD");
            if (card.LastUnlocked != unlocked)
            {
                RebuildArt(card.Art, index, unlocked);
                card.LastUnlocked = unlocked;
            }
        }

        private void RefreshTray(int index, bool unlocked, int count, GamePhase phase, SaleResult sale)
        {
            var tray = trays[index];
            var product = ShopModel.Catalog[index];
            tray.Frame.color = unlocked ? T.Hex("9A6138") : T.Hex("8B8B82");
            tray.Edge.color = unlocked ? T.Hex("BE8653") : T.Hex("AAACA3");
            tray.Well.color = unlocked ? T.Hex("563B28") : T.Hex("575A55");
            tray.Label.color = unlocked ? T.Hex("E5C395") : T.Hex("BDBDB4");
            tray.Name.color = unlocked ? T.Ink : T.Hex("62655F");
            tray.Count.color = unlocked ? T.LightPaper : T.Hex("C7C9C1");
            tray.Status.color = unlocked ? T.Hex("D8C1A0") : T.Hex("BEC1B8");
            tray.Description.color = unlocked ? T.Hex("D8C1A0") : T.Hex("BEC1B8");
            if (!unlocked)
            {
                tray.Count.text = "연구 필요";
                tray.Count.fontSize = 24;
                tray.Status.text = "아직 준비되지 않은 떡";
                tray.Description.text = product.Name + "은 연구가 필요합니다.";
                tray.Description.fontSize = 15;
            }
            else
            {
                tray.Count.fontSize = 37;
                tray.Count.text = Number(phase == GamePhase.Closed && sale != null ? sale.UnsoldCounts[index] : count) + "개";
                tray.Status.text = phase == GamePhase.Arrangement ? "생산 예정" : phase == GamePhase.ReadyToSell ? "진열 완료" : "남은 떡";
                tray.Description.fontSize = 17;
                tray.Description.text = phase == GamePhase.Closed && sale != null ? "오늘 " + Number(sale.SoldCounts[index]) + "개 판매" : count == 0 ? "수량을 선택해 주세요" : "판매가 " + Won(product.SellPrice);
            }
            if (tray.LastUnlocked != unlocked)
            {
                RebuildArt(tray.Art, index, unlocked);
                tray.LastUnlocked = unlocked;
            }
        }

        private void RebuildArt(RectTransform parent, int index, bool unlocked)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) DisposeVisual(parent.GetChild(i).gameObject);
            if (riceCakeAtlas != null && (unlocked || grayscaleMaterial != null))
            {
                var rect = T.Rect("Product image", parent);
                T.Fill(rect);
                var image = rect.gameObject.AddComponent<Image>();
                image.raycastTarget = false;
                var cellWidth = riceCakeAtlas.width / 3f;
                var cellHeight = riceCakeAtlas.height / 3f;
                var col = index % 3;
                var row = index / 3;
                var sprite = riceCakeSprites[index];
                if (sprite == null)
                {
                    // The painted mugwort cell needs a little vertical padding to exclude its neighbouring artwork.
                    var verticalInset = index == 5 ? cellHeight * .055f : 0f;
                    sprite = Sprite.Create(riceCakeAtlas,
                        new Rect(col * cellWidth, (2 - row) * cellHeight + verticalInset, cellWidth, cellHeight - verticalInset * 2), new Vector2(.5f, .5f), 100,
                        0, SpriteMeshType.FullRect);
                    sprite.name = ShopModel.Catalog[index].Name + " artwork";
                    riceCakeSprites[index] = sprite;
                    generatedAtlasSprites.Add(sprite);
                }
                image.sprite = sprite;
                image.material = unlocked ? null : grayscaleMaterial;
                image.color = unlocked ? Color.white : T.Hex("BFC2B8");
                image.preserveAspect = true;
                return;
            }
            DrawRiceCake(parent, index, unlocked);
        }

        private void DrawLock(RectTransform parent)
        {
            var width = parent.rect.width;
            var height = parent.rect.height;
            var disc = T.Panel("Monochrome seal", parent, width * .15f, height * .18f, width * .7f, height * .65f, T.Hex("AAAFA4"), 20);
            disc.sprite = T.Circle;
            var loop = T.Panel("Lock arch", parent, width * .35f, height * .28f, width * .30f, height * .29f, T.Hex("5C6259"), 15);
            T.Panel("Arch opening", loop.transform, width * .05f, height * .055f, width * .20f, height * .25f, T.Hex("AAAFA4"), 11);
            T.Panel("Lock", parent, width * .28f, height * .47f, width * .44f, height * .29f, T.Hex("5C6259"), 5);
            T.Panel("Keyhole", parent, width * .48f, height * .56f, width * .04f, height * .11f, T.Hex("CED1C7"), 4);
        }

        private void DrawRiceCake(RectTransform parent, int index, bool unlocked)
        {
            var width = parent.rect.width;
            var height = parent.rect.height;
            var plate = T.Panel("Bamboo plate", parent, 1, height * .13f, width - 2, height * .76f, unlocked ? T.Hex("C49B61") : T.Hex("ACAEA5"), 20);
            plate.sprite = T.Circle;
            T.Panel("Plate inset", parent, width * .06f, height * .19f, width * .88f, height * .64f, unlocked ? T.Hex("9E7847") : T.Hex("858980"), 18).sprite = T.Circle;
            var colors = new[] { T.Hex("FFF6E6"), T.Hex("E0DBCA"), T.Hex("FFFCF0"), T.Hex("DDB977"), T.Hex("E9AEAD"), T.Hex("779467"), T.Hex("F4DEB0"), T.Hex("C3AF82"), T.Hex("EBC59B") };
            for (var cake = 0; cake < 4; cake++)
            {
                var x = width * (.14f + (cake % 2) * .34f);
                var y = height * (.27f + (cake / 2) * .22f);
                var w = width * .33f;
                var h = height * .20f;
                var color = index == 1 && cake % 2 == 0 ? T.Hex("79915F") : index == 4 && cake % 2 == 0 ? T.Hex("8CA375") : colors[index];
                if (!unlocked) color = T.Hex("C3C6BB");
                T.Panel("Cake shadow " + cake, parent, x + 2, y + 4, w, h, unlocked ? new Color(.2f, .12f, .05f, .3f) : new Color(.2f, .2f, .2f, .3f), index == 0 ? 12 : 7);
                var image = T.Panel("Rice cake " + cake, parent, x, y, w, h, color, index == 0 ? 12 : 7);
                if (index == 4 || index == 6) image.sprite = T.Circle;
                T.Panel("Cake light " + cake, parent, x + w * .12f, y + h * .12f, w * .68f, h * .12f, new Color(1, 1, 1, .36f), 2);
                if (index == 2 || index == 7)
                    for (var sesame = 0; sesame < 3; sesame++)
                        T.Panel("Sesame " + cake + " " + sesame, parent, x + w * (.24f + sesame * .20f), y + h * .34f, 3, 4, unlocked ? T.Ink : T.GrayInk, 1);
            }
        }

        private void ChangeCount(int index, int delta)
        {
            if (model == null) return;
            if (!model.TrySetCount(index, model.SelectedCounts[index] + delta, out var error)) Notify(error);
        }

        private void ClearSelection()
        {
            if (model == null || model.GetPhase() != GamePhase.Arrangement) return;
            model.ClearSelection();
        }

        private void AutoDistribute()
        {
            if (model == null || model.GetPhase() != GamePhase.Arrangement) return;
            model.DistributeToCapacity();
            Notify("진열 가능한 떡을 고르게 배치했어요.");
        }

        private void PrimaryAction()
        {
            if (model == null) return;
            if (model.GetPhase() == GamePhase.Arrangement)
            {
                if (!model.TryConfirmProduction(out var error)) Notify(error);
                else RequestOpening();
            }
            else if (model.GetPhase() == GamePhase.ReadyToSell)
                RequestOpening();
            else ShowSaleResult();
        }

        private void RequestOpening()
        {
            if (OpenDayRequested != null) OpenDayRequested.Invoke();
            else model.OpenDayWithCustomer();
        }

        private RectTransform OpenDialog(string name, string title, float width, float height, bool canClose = true)
        {
            CloseDialog();
            var dimmer = T.Rect(name + " · Modal", modalLayer);
            T.Fill(dimmer);
            var dimmerImage = T.Paint(dimmer, new Color(.10f, .085f, .06f, .72f), 0);
            dimmerImage.raycastTarget = true;
            var panel = Board(name, dimmer, (1920 - width) / 2, (1080 - height) / 2, width, height, T.DarkWood, T.Paper, 14);
            T.Label("Dialog Title", panel, 34, 20, width - 130, 61, title, 34, T.Ink, TextAnchor.MiddleLeft, true, true);
            T.Panel("Title Rule", panel, 34, 90, width - 68, 2, T.Gold, 0);
            if (canClose) T.Button("Close Dialog", panel, width - 83, 28, 49, 43, "닫기", T.Hex("E7D2B1"), CloseDialog, 17);
            return panel;
        }

        private void CloseDialog()
        {
            if (modalLayer == null) return;
            for (var i = modalLayer.childCount - 1; i >= 0; i--) DisposeVisual(modalLayer.GetChild(i).gameObject);
        }

        private void ShowCapacityInfo()
        {
            var state = model ?? new ShopModel();
            var panel = OpenDialog("Capacity Information", "우리 가판대", 690, 486);
            T.Label("Capacity", panel, 38, 113, 614, 63, "최대 " + Number(state.Capacity) + "개 진열", 37, T.Green, TextAnchor.MiddleCenter, true);
            T.Label("Info", panel, 42, 187, 606, 137,
                "1차 · 평판 15 이상 · 회당 150,000원\n30 → 40 → 50팩\n2차 · 평판 35 이상 · 회당 400,000원\n50 → 60 → 70팩 · 1차 완료 필요",
                23, T.Ink, TextAnchor.MiddleCenter);
            T.Label("Evening upgrade hint", panel, 42, 337, 606, 44,
                "영업을 마친 뒤 연구를 신청하면 다음 날 넓어져요.", 21, T.MutedInk, TextAnchor.MiddleCenter);
            T.Button("Return to Shop", panel, 38, 402, 614, 57, "확인", T.Hex("E4CDAA"), CloseDialog, 22);
        }

        private void ToggleSound()
        {
            soundEnabled = !soundEnabled;
            AudioListener.pause = !soundEnabled;
            Refresh();
        }

        private void ShowSettings()
        {
            var panel = OpenDialog("Shop Settings", "가게 설정", 690, 405);
            T.Label("Audio Label", panel, 40, 112, 300, 36, "소리", 25, T.Ink, TextAnchor.MiddleLeft, true);
            T.Button("Toggle Sound", panel, 416, 108, 226, 48, soundEnabled ? "소리 끄기" : "소리 켜기", T.Hex("E7D2B1"), () => { ToggleSound(); ShowSettings(); }, 20);
            T.Label("Volume Label", panel, 40, 177, 120, 35, "음량", 22, T.Ink);
            var sliderRoot = T.At("Volume", panel, 183, 180, 459, 33);
            var slider = sliderRoot.gameObject.AddComponent<Slider>();
            slider.minValue = 0;
            slider.maxValue = 1;
            var track = T.Rect("Track", sliderRoot);
            T.Fill(track, 0, 12, 0, 12);
            T.Paint(track, T.Hex("D3BA92"), 4);
            var fillArea = T.Rect("Fill Area", sliderRoot);
            T.Fill(fillArea, 10, 12, 10, 12);
            var fill = T.Rect("Fill", fillArea);
            T.Fill(fill);
            T.Paint(fill, T.Green, 4);
            var handleArea = T.Rect("Handle Area", sliderRoot);
            T.Fill(handleArea, 14, 0, 14, 0);
            var handle = T.Rect("Handle", handleArea);
            handle.sizeDelta = new Vector2(28, 28);
            var handleImage = T.Paint(handle, T.Green, 14);
            handleImage.sprite = T.Circle;
            handleImage.raycastTarget = true;
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImage;
            slider.direction = Slider.Direction.LeftToRight;
            slider.SetValueWithoutNotify(AudioListener.volume);
            slider.onValueChanged.AddListener(value => AudioListener.volume = value);
            T.Label("Audio value", panel, 40, 235, 600, 38, "소리 설정은 언제든 바꿀 수 있어요.", 20, T.MutedInk);
            T.Button("Done", panel, 218, 307, 254, 56, "설정 완료", T.Green, CloseDialog, 22, T.LightPaper);
        }

        private void ShowSaleResult()
        {
            if (model == null || model.LastSale == null) return;
            var sale = model.LastSale;
            var customerResult = model.Customer != null && model.Customer.Step == CustomerStep.Result;
            var automaticResult = model.GetPhase() == GamePhase.Closed && (customerResult || model.Customer == null);
            var panel = OpenDialog("Daily Sales Result", sale.Day + "일차 · 판매 결과", 938, 902, !automaticResult);
            T.Label("Reputation and Rate", panel, 36, 105, 866, 31,
                ReputationGrade(model.GetReputation()) + " · 평판 " + Number(model.GetReputation()) + " · 오늘의 판매율 " + Percent(sale.Rate)
                    + (customerResult && model.Customer.WasSatisfied ? " · 추천 성공 +1" : ""),
                22, T.MutedInk, TextAnchor.MiddleCenter);

            Metric(panel, 37, "생산한 떡", Number(sale.TotalProduced) + "개", T.Ink);
            Metric(panel, 257, "판매한 떡", Number(sale.TotalSold) + "개", T.Green);
            Metric(panel, 477, "남은 떡", Number(sale.TotalUnsold) + "개", T.Red);
            Metric(panel, 697, "판매율", Percent(sale.Rate), T.Ink);

            T.Panel("Table header", panel, 37, 259, 864, 43, T.Hex("E5D0AD"), 4);
            T.Label("Name heading", panel, 54, 263, 400, 35, "떡 종류", 19, T.Ink, TextAnchor.MiddleLeft, true);
            T.Label("Production heading", panel, 447, 263, 136, 35, "생산", 19, T.Ink, TextAnchor.MiddleCenter, true);
            T.Label("Sold heading", panel, 594, 263, 136, 35, "판매", 19, T.Ink, TextAnchor.MiddleCenter, true);
            T.Label("Unsold heading", panel, 742, 263, 136, 35, "미판매", 19, T.Ink, TextAnchor.MiddleCenter, true);
            for (var i = 0; i < ShopModel.Catalog.Count; i++)
            {
                var y = 309 + i * 31;
                if (i % 2 == 0) T.Panel("Row " + i, panel, 37, y, 864, 31, new Color(1, 1, 1, .27f), 0);
                var foreground = sale.ProducedCounts[i] > 0 ? T.Ink : T.MutedInk;
                T.Label("Product " + i, panel, 55, y, 367, 31, ShopModel.Catalog[i].Name, 18, foreground);
                T.Label("Produced " + i, panel, 447, y, 136, 31, Number(sale.ProducedCounts[i]), 18, foreground, TextAnchor.MiddleCenter);
                T.Label("Sold " + i, panel, 594, y, 136, 31, Number(sale.SoldCounts[i]), 18, foreground, TextAnchor.MiddleCenter);
                T.Label("Unsold " + i, panel, 742, y, 136, 31, Number(sale.UnsoldCounts[i]), 18, foreground, TextAnchor.MiddleCenter);
            }

            T.Panel("Accounting Rule", panel, 37, 600, 864, 2, T.Gold, 0);
            AccountingLine(panel, 612, "총 판매 수익", sale.Revenue, T.Ink, 24);
            AccountingLine(panel, 656, "사용한 생산비", sale.ProductionCost, T.MutedInk, 22);
            T.Panel("Net profit well", panel, 37, 706, 864, 70, T.Hex("E5D9BA"), 7);
            AccountingLine(panel, 716, "오늘의 순이익", sale.NetProfit, sale.NetProfit >= 0 ? T.Green : T.Red, 29);
            if (automaticResult)
            {
                resultCountdown = T.Label("Automatic Next Day", panel, 37, 804, 281, 58,
                    "잠시 후 저녁 연구를 선택해요", 22, T.MutedInk, TextAnchor.MiddleCenter);
                SetFlowTiming(resultSecondsRemaining);
            }
            else T.Button("Keep Viewing", panel, 37, 804, 281, 58, "가판대 보기", T.Hex("E4CDAA"), CloseDialog, 23);
            T.Button("Next Day", panel, 335, 804, 566, 58, "저녁 연구 선택", T.Green, () =>
            {
                CloseDialog();
                if (SkipResultRequested != null) SkipResultRequested.Invoke();
                else model.BeginEvening();
                catalogScroll.verticalNormalizedPosition = 1;
            }, 25, T.LightPaper);
        }

        private void Metric(Transform panel, float x, string label, string value, Color color)
        {
            T.Panel(label + " Well", panel, x, 153, 204, 87, T.LightPaper, 7);
            T.Label(label, panel, x + 9, 158, 186, 27, label, 18, T.MutedInk, TextAnchor.MiddleCenter);
            T.Label(label + " Value", panel, x + 9, 183, 186, 48, value, 32, color, TextAnchor.MiddleCenter, true);
        }

        private void AccountingLine(Transform panel, float y, string label, long amount, Color color, int size)
        {
            T.Label(label, panel, 55, y, 420, 42, label, size, color, TextAnchor.MiddleLeft, true);
            T.Label(label + " Value", panel, 452, y, 427, 42, Won(amount), size, color, TextAnchor.MiddleRight, true);
        }

        private void Notify(string message)
        {
            if (toast == null || string.IsNullOrEmpty(message)) return;
            toastText.text = message;
            toastUntil = Time.unscaledTime + 4;
            toast.gameObject.SetActive(true);
            toast.SetAsLastSibling();
        }

        private static string ReputationGrade(int reputation)
        {
            return reputation < 15 ? "동네 떡집" : reputation < 35 ? "소문난 떡집" : "유명한 떡집";
        }

        private static string Number(long value) { return value.ToString("N0", CultureInfo.InvariantCulture); }
        private static string Won(long value) { return Number(value) + "원"; }
        private static string Percent(double value) { return (value * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%"; }

        private static void DisposeVisual(UnityEngine.Object value)
        {
            if (value == null) return;
            if (value is GameObject go) go.SetActive(false);
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
