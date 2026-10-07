using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DdukDduk.Shop
{
    /// <summary>Small, code-native uGUI primitives. No external font files are bundled.</summary>
    internal static class ShopTheme
    {
        internal static readonly Color Ink = Hex("47352A");
        internal static readonly Color MutedInk = Hex("82705D");
        internal static readonly Color Paper = Hex("F9EBD0");
        internal static readonly Color LightPaper = Hex("FFF5E3");
        internal static readonly Color Green = Hex("3F6545");
        internal static readonly Color Red = Hex("973F3A");
        internal static readonly Color Wood = Hex("7C4B2E");
        internal static readonly Color DarkWood = Hex("452B1B");
        internal static readonly Color Gold = Hex("CDA36A");
        internal static readonly Color GrayPaper = Hex("D3D2CB");
        internal static readonly Color GrayInk = Hex("77776F");

        private static readonly Dictionary<int, Sprite> RoundedSprites = new Dictionary<int, Sprite>();
        private static Sprite circle;
        private static Font bodyFont;
        private static Font titleFont;

        internal static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        internal static Font BodyFont
        {
            get
            {
                if (bodyFont == null)
                    bodyFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 32);
                return bodyFont;
            }
        }

        internal static Font TitleFont
        {
            get
            {
                if (titleFont == null)
                    titleFont = Font.CreateDynamicFontFromOSFont(new[] { "Batang", "바탕", "Malgun Gothic", "Arial" }, 38);
                return titleFont;
            }
        }

        internal static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        internal static RectTransform At(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }

        internal static void Fill(RectTransform rect, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(.5f, .5f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        internal static Image Paint(RectTransform rect, Color color, float radius = 8)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            image.sprite = Rounded(Mathf.RoundToInt(radius));
            image.type = Image.Type.Sliced;
            return image;
        }

        internal static Image Panel(string name, Transform parent, float x, float y, float width, float height, Color color, float radius = 8)
        {
            return Paint(At(name, parent, x, y, width, height), color, radius);
        }

        internal static Text Label(string name, Transform parent, float x, float y, float width, float height,
            string value, int size, Color color, TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false, bool title = false)
        {
            var rect = At(name, parent, x, y, width, height);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = title ? TitleFont : BodyFont;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            text.supportRichText = false;
            return text;
        }

        internal static Button Button(string name, Transform parent, float x, float y, float width, float height,
            string caption, Color color, Action onClick, int size = 24, Color? foreground = null)
        {
            var image = Panel(name, parent, x, y, width, height, color, 10);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.05f, 1.04f, 1.02f);
            colors.pressedColor = new Color(.82f, .8f, .75f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(.6f, .6f, .58f, .7f);
            colors.fadeDuration = .12f;
            button.colors = colors;
            if (onClick != null) button.onClick.AddListener(() => onClick());
            var label = Label("Caption", image.transform, 4, 0, width - 8, height, caption, size,
                foreground ?? Ink, TextAnchor.MiddleCenter, true);
            Fill(label.rectTransform, 4, 0, 4, 0);
            return button;
        }

        internal static Sprite Rounded(int radius)
        {
            radius = Mathf.Clamp(radius, 0, 24);
            if (RoundedSprites.TryGetValue(radius, out var cached) && cached != null) return cached;
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "Shop UI rounded rectangle";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;
            var pixels = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius));
                var dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius));
                var distance = dx > 0 && dy > 0 ? Mathf.Sqrt(dx * dx + dy * dy) : 0;
                var alpha = Mathf.Clamp01(radius + .5f - distance);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255));
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100,
                0, SpriteMeshType.FullRect, Vector4.one * (radius + 1));
            sprite.name = "Shop UI radius " + radius;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            RoundedSprites[radius] = sprite;
            return sprite;
        }

        internal static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int size = 96;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(size / 2f, size / 2f));
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(size / 2f - distance) * 255));
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
                circle.hideFlags = HideFlags.HideAndDontSave;
                return circle;
            }
        }
    }
}
