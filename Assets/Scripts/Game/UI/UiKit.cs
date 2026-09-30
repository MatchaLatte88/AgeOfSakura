using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    public enum UiButtonStyle
    {
        Primary,    // matcha green: the one main action on screen
        Secondary,  // washi paper
        Premium,    // gold, used only for Diamond actions
        Danger,     // vermilion
        Dark        // lacquer
    }

    public enum UiButtonState
    {
        Normal,
        /// <summary>Looks disabled but stays tappable so the player gets an explanation (toast).</summary>
        Unaffordable,
        Disabled
    }

    public enum ButtonLayout
    {
        /// <summary>Icon and label side by side, centred as a group.</summary>
        Row,
        /// <summary>Children stacked.</summary>
        Column
    }

    public enum TextStyle
    {
        /// <summary>Dark ink on paper, letterpress edge.</summary>
        Ink,
        /// <summary>Cream with dark outline: labels on coloured buttons and over the world.</summary>
        Outlined,
        /// <summary>Chunky outlined numbers.</summary>
        Number,
        /// <summary>Cream on dark lacquer.</summary>
        Light
    }

    /// <summary>Shared sizes so every screen uses the same rhythm. Values are canvas units (reference height 1080).</summary>
    public static class UiTheme
    {
        /// <summary>Smallest tappable size (about 8-9 mm on a phone).</summary>
        public const float TouchMin = 120f;
        public const float ButtonDepth = 12f;
        public const float Gap = 20f;

        public const int FontHero = 92;
        public const int FontTitle = 56;
        public const int FontHeading = 46;
        public const int FontBody = 36;
        public const int FontCaption = 30;
    }

    /// <summary>
    /// Springy press feedback for 3D buttons: the face sinks into its depth lip and the whole button squashes slightly;
    /// releasing overshoots a little. Idle buttons cost nothing (Update is off).
    /// </summary>
    public sealed class UiPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform Face;
        public RectTransform Root;
        public float Depth;

        private float value;    // 0 = rest, 1 = fully pressed
        private float velocity;
        private float target;

        private void Awake() => enabled = false;

        public void OnPointerDown(PointerEventData eventData) => Press(1f);
        public void OnPointerUp(PointerEventData eventData) => Press(0f);
        public void OnPointerExit(PointerEventData eventData) => Press(0f);

        private void Press(float to)
        {
            target = to;
            enabled = true;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.033f);
            // damped spring: quick on press, a little overshoot on release
            velocity += ((target - value) * 720f - velocity * 27f) * dt;
            value += velocity * dt;
            Apply();
            if (Mathf.Abs(velocity) < 0.02f && Mathf.Abs(target - value) < 0.002f)
            {
                value = target;
                Apply();
                enabled = false;
            }
        }

        private void Apply()
        {
            if (Face != null) Face.anchoredPosition = new Vector2(0f, -Depth * 0.85f * value);
            if (Root != null) Root.localScale = new Vector3(1f + 0.025f * value, 1f - 0.035f * value, 1f);
        }

        private void OnDisable()
        {
            value = 0f;
            velocity = 0f;
            target = 0f;
            if (Face != null) Face.anchoredPosition = Vector2.zero;
            if (Root != null) Root.localScale = Vector3.one;
        }
    }

    /// <summary>A soft highlight that sweeps across a button now and then. Runs only while the button is visible.</summary>
    public sealed class UiShineSweep : MonoBehaviour
    {
        public RectTransform Shine;
        public RectTransform Clip;
        public float Period = 3.2f;
        public float SweepSeconds = 0.75f;

        private float clock;

        private void OnEnable() => clock = UnityEngine.Random.Range(0f, Period * 0.5f);

        private void Update()
        {
            clock += Time.unscaledDeltaTime;
            float t = (clock % Period) / SweepSeconds;
            if (t >= 1f)
            {
                if (Shine.gameObject.activeSelf) Shine.gameObject.SetActive(false);
                return;
            }
            if (!Shine.gameObject.activeSelf) Shine.gameObject.SetActive(true);
            float w = Clip.rect.width;
            float x = Mathf.Lerp(-w * 0.6f, w * 0.6f, Ease.InOutSine(t));
            Shine.anchoredPosition = new Vector2(x, 0f);
        }
    }

    /// <summary>Gentle breathing scale for calls to action (Update runs only while enabled and visible).</summary>
    public sealed class UiPulse : MonoBehaviour
    {
        public RectTransform Target;
        public float Amount = 0.03f;
        public float Speed = 2.4f;

        private void OnDisable()
        {
            if (Target != null) Target.localScale = Vector3.one;
        }

        private void Update()
        {
            if (Target == null) return;
            float s = 1f + Mathf.Sin(Time.unscaledTime * Speed) * Amount;
            Target.localScale = new Vector3(s, s, 1f);
        }
    }

    /// <summary>Random, brief twinkle of a sparkle sprite (scale + spin + fade), e.g. on currency icons.</summary>
    public sealed class UiGlint : MonoBehaviour
    {
        public RectTransform Target;
        public Image Image;
        public float MinDelay = 2.6f;
        public float MaxDelay = 6f;
        public float Duration = 0.7f;

        private float wait;
        private float age = -1f;

        private void OnEnable()
        {
            wait = UnityEngine.Random.Range(0.4f, MaxDelay);
            age = -1f;
            Hide();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (age < 0f)
            {
                wait -= dt;
                if (wait <= 0f) age = 0f;
                return;
            }
            age += dt;
            float t = age / Duration;
            if (t >= 1f)
            {
                age = -1f;
                wait = UnityEngine.Random.Range(MinDelay, MaxDelay);
                Hide();
                return;
            }
            float s = Mathf.Sin(t * Mathf.PI);
            Target.localScale = Vector3.one * s;
            Target.localRotation = Quaternion.Euler(0f, 0f, t * 90f);
            var c = Image.color;
            c.a = s;
            Image.color = c;
        }

        private void Hide()
        {
            if (Target != null) Target.localScale = Vector3.zero;
        }
    }

    /// <summary>Wraps one 3D button: face, depth lip, label, icon and visual states.</summary>
    public sealed class UiButton
    {
        public GameObject GameObject;
        public RectTransform Rect;
        public RectTransform FaceRect;
        public Image Face;
        public Image Depth;
        public Button Button;
        public TMP_Text Label;
        public Image Icon;
        /// <summary>Layout container inside the face; extra content (e.g. a price chip) is added here.</summary>
        public RectTransform Content;
        public UiButtonStyle Style;
        internal UiKit Kit;

        private UiButtonState state = UiButtonState.Normal;

        public UiButtonState State => state;

        public void SetState(UiButtonState newState)
        {
            state = newState;
            Button.interactable = newState != UiButtonState.Disabled;
            string id = newState == UiButtonState.Normal ? UiKit.SpriteName(Style) : "btn_disabled";
            Face.sprite = Kit.Icons.Get(id);
            Depth.sprite = Kit.Icons.Get(id + "_depth");
            if (Label != null) Label.color = newState == UiButtonState.Normal ? UiKit.LabelColor(Style) : Palette.InkSoft;
            if (Icon != null) Icon.color = newState == UiButtonState.Normal ? UiKit.IconTint(Style) : new Color(0.32f, 0.27f, 0.2f, 0.7f);
        }

        public void SetStyle(UiButtonStyle style)
        {
            Style = style;
            SetState(state);
            if (Label != null) Label.fontSharedMaterial = Kit.MaterialFor(UiKit.LabelStyle(style));
        }

        public void SetLabel(string text)
        {
            if (Label != null) Label.text = text;
        }

        public void OnClick(Action action) => Button.onClick.AddListener(() => action());
    }

    /// <summary>
    /// Builds uGUI elements in code (no prefabs). Reference resolution 1920x1080, matched on height. Sprites come from
    /// <see cref="IconSet"/> (baked art), text is TextMeshPro with material presets. Anything holding text or icons is laid out
    /// with layout groups, so content stays centred and inside its container at any size, aspect ratio or language.
    /// </summary>
    public sealed class UiKit
    {
        private readonly TMP_FontAsset font;
        private readonly Material plain;
        private readonly Material outlined;
        private readonly Material number;
        private readonly Material light;

        public readonly IconSet Icons;
        public UiMotion Motion;

        /// <summary>Invoked on every button tap (wired to the audio hook by <see cref="GameUi"/>).</summary>
        public Action ClickFeedback;

        public UiKit(IconSet icons)
        {
            Icons = icons;
            font = TMP_Settings.defaultFontAsset;
            if (font == null) throw new InvalidOperationException("TextMeshPro default font missing. Run 'Age of Sakura > Import TextMeshPro Essentials'.");
            plain = LoadMaterial("UI_Plain");
            outlined = LoadMaterial("UI_Outlined");
            number = LoadMaterial("UI_Number");
            light = LoadMaterial("UI_Light");
        }

        private static Material LoadMaterial(string name)
        {
            var m = Resources.Load<Material>("UI/Fonts/" + name);
            if (m == null) throw new InvalidOperationException($"Font material '{name}' missing. Run 'Age of Sakura > Generate UI Art'.");
            return m;
        }

        public Material MaterialFor(TextStyle style)
        {
            switch (style)
            {
                case TextStyle.Outlined: return outlined;
                case TextStyle.Number: return number;
                case TextStyle.Light: return light;
                default: return plain;
            }
        }

        // ------------------------------------------------------------------ style tables

        public static string SpriteName(UiButtonStyle style)
        {
            switch (style)
            {
                case UiButtonStyle.Primary: return "btn_green";
                case UiButtonStyle.Premium: return "btn_gold";
                case UiButtonStyle.Danger: return "btn_red";
                case UiButtonStyle.Dark: return "btn_dark";
                default: return "btn_paper";
            }
        }

        public static TextStyle LabelStyle(UiButtonStyle style) =>
            style == UiButtonStyle.Secondary || style == UiButtonStyle.Premium ? TextStyle.Ink : TextStyle.Outlined;

        public static Color LabelColor(UiButtonStyle style) =>
            style == UiButtonStyle.Secondary || style == UiButtonStyle.Premium ? Palette.Ink : Palette.Cream;

        public static Color IconTint(UiButtonStyle style) =>
            style == UiButtonStyle.Secondary ? Palette.Ink : Color.white;

        // ------------------------------------------------------------------ rect / layout helpers

        public static RectTransform Rect(GameObject go) => go.GetComponent<RectTransform>();

        public static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = size;
        }

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>Sets minimum + preferred size (fixed inside layout groups) and/or flexible weights. -1 leaves a value unchanged.</summary>
        public static LayoutElement Size(GameObject go, float width = -1f, float height = -1f, float flexWidth = -1f, float flexHeight = -1f)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null) element = go.AddComponent<LayoutElement>();
            if (width >= 0f) { element.minWidth = width; element.preferredWidth = width; }
            if (height >= 0f) { element.minHeight = height; element.preferredHeight = height; }
            if (flexWidth >= 0f) element.flexibleWidth = flexWidth;
            if (flexHeight >= 0f) element.flexibleHeight = flexHeight;
            return element;
        }

        /// <summary>Horizontal container. Children take their preferred size; <paramref name="align"/> positions them.</summary>
        public RectTransform Row(Transform parent, string name, float spacing = UiTheme.Gap, TextAnchor align = TextAnchor.MiddleLeft,
            int padLeft = 0, int padTop = 0, int padRight = 0, int padBottom = 0, bool fillHeight = false)
        {
            var go = Empty(parent, name);
            var group = go.AddComponent<HorizontalLayoutGroup>();
            Configure(group, spacing, align, padLeft, padTop, padRight, padBottom);
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = fillHeight;
            return Rect(go);
        }

        /// <summary>Vertical container. By default children are stretched to the full width.</summary>
        public RectTransform Column(Transform parent, string name, float spacing = 6f, TextAnchor align = TextAnchor.MiddleLeft,
            int padLeft = 0, int padTop = 0, int padRight = 0, int padBottom = 0, bool fillWidth = true)
        {
            var go = Empty(parent, name);
            var group = go.AddComponent<VerticalLayoutGroup>();
            Configure(group, spacing, align, padLeft, padTop, padRight, padBottom);
            group.childForceExpandWidth = fillWidth;
            group.childForceExpandHeight = false;
            return Rect(go);
        }

        /// <summary>Adds a layout group to an existing object (so an Image can lay out its own children and report its size).</summary>
        public static HorizontalOrVerticalLayoutGroup AddGroup(GameObject go, bool vertical, float spacing, TextAnchor align, int l = 0, int t = 0, int r = 0, int b = 0, bool fill = false)
        {
            HorizontalOrVerticalLayoutGroup group = vertical ? (HorizontalOrVerticalLayoutGroup)go.AddComponent<VerticalLayoutGroup>() : go.AddComponent<HorizontalLayoutGroup>();
            Configure(group, spacing, align, l, t, r, b);
            group.childForceExpandWidth = fill;
            group.childForceExpandHeight = false;
            return group;
        }

        /// <summary>A tappable card: sliced sprite + button with squash feedback and a soft shadow. Add content via AddGroup.</summary>
        public Image Tile(Transform parent, string name, string spriteId, Vector2 size, out Button button)
        {
            var image = Sliced(parent, name, spriteId, true);
            Size(image.gameObject, size.x, size.y);
            AddShadow(image.gameObject, -8f, 0.26f);
            button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.03f, 1.03f, 1.03f, 1f);
            colors.pressedColor = new Color(0.9f, 0.88f, 0.84f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.04f;
            button.colors = colors;
            button.onClick.AddListener(() => ClickFeedback?.Invoke());
            var feedback = image.gameObject.AddComponent<UiPressFeedback>();
            feedback.Root = image.rectTransform;
            feedback.Depth = 0f;
            return image;
        }

        private static void Configure(HorizontalOrVerticalLayoutGroup group, float spacing, TextAnchor align, int l, int t, int r, int b)
        {
            group.spacing = spacing;
            group.childAlignment = align;
            group.padding = new RectOffset(l, r, t, b);
            group.childControlWidth = true;
            group.childControlHeight = true;
        }

        // ------------------------------------------------------------------ elements

        public GameObject Empty(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>A 9-slice image from the baked art (panels, pills, cards, bars).</summary>
        public Image Sliced(Transform parent, string name, string spriteId, bool blocksInput = true)
        {
            var go = Empty(parent, name);
            var image = go.AddComponent<Image>();
            image.sprite = Icons.Get(spriteId);
            image.type = Image.Type.Sliced;
            image.raycastTarget = blocksInput;
            return image;
        }

        /// <summary>Soft drop shadow for depth.</summary>
        public static void AddShadow(GameObject go, float offsetY = -8f, float alpha = 0.3f)
        {
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0.08f, 0.04f, 0.02f, alpha);
            shadow.effectDistance = new Vector2(0f, offsetY);
        }

        public Image Picture(Transform parent, string name, Sprite sprite, Vector2 size)
        {
            var go = Empty(parent, name);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;
            Rect(go).sizeDelta = size;
            Size(go, size.x, size.y);
            return image;
        }

        public Image Picture(Transform parent, string name, string spriteId, Vector2 size) => Picture(parent, name, Icons.Get(spriteId), size);

        /// <summary>TextMeshPro label that shrinks to fit (down to ~55%) instead of overflowing.</summary>
        public TMP_Text Text(Transform parent, string text, TextStyle style, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center,
            FontStyles fontStyle = FontStyles.Normal, string name = "Text", bool wrap = false)
        {
            var go = Empty(parent, name);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSharedMaterial = MaterialFor(style);
            t.text = text;
            t.fontSize = size;
            t.fontStyle = fontStyle;
            t.color = color;
            t.alignment = align;
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = Mathf.Max(14f, size * 0.55f);
            t.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Ellipsis;
            t.raycastTarget = false;
            t.richText = false;
            return t;
        }

        /// <summary>The bold heading style used for titles.</summary>
        public TMP_Text Title(Transform parent, string text, float size = UiTheme.FontTitle, string name = "Title") =>
            Text(parent, text, TextStyle.Ink, size, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, name);

        public TMP_Text Body(Transform parent, string text, float size = UiTheme.FontBody, string name = "Body") =>
            Text(parent, text, TextStyle.Ink, size, Palette.InkSoft, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, name);

        public UiButton Button(Transform parent, string name, string label, Vector2 size, UiButtonStyle style, Sprite icon = null,
            int fontSize = 48, bool iconOnly = false, ButtonLayout layout = ButtonLayout.Row)
        {
            float depth = UiTheme.ButtonDepth;
            var root = Empty(parent, name);
            var rootRect = Rect(root);
            rootRect.sizeDelta = size;
            Size(root, size.x, size.y);

            string spriteId = SpriteName(style);
            var depthImage = Sliced(root.transform, "Depth", spriteId + "_depth", false);
            depthImage.rectTransform.anchorMin = Vector2.zero;
            depthImage.rectTransform.anchorMax = Vector2.one;
            depthImage.rectTransform.offsetMin = Vector2.zero;
            depthImage.rectTransform.offsetMax = new Vector2(0f, -depth);
            AddShadow(depthImage.gameObject, -8f, 0.34f);

            var face = Sliced(root.transform, "Face", spriteId, true);
            face.rectTransform.anchorMin = Vector2.zero;
            face.rectTransform.anchorMax = Vector2.one;
            face.rectTransform.offsetMin = new Vector2(0f, depth);
            face.rectTransform.offsetMax = Vector2.zero;

            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.04f, 1.04f, 1.04f, 1f);
            colors.pressedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.04f;
            button.colors = colors;
            button.onClick.AddListener(() => ClickFeedback?.Invoke());

            var feedback = face.gameObject.AddComponent<UiPressFeedback>();
            feedback.Face = face.rectTransform;
            feedback.Root = rootRect; // the whole button squashes, only the face sinks
            feedback.Depth = depth;

            var result = new UiButton
            {
                GameObject = root, Rect = rootRect, FaceRect = face.rectTransform, Face = face, Depth = depthImage,
                Button = button, Style = style, Kit = this
            };

            result.Content = layout == ButtonLayout.Row
                ? Row(face.transform, "Content", 14f, TextAnchor.MiddleCenter, 26, 6, 26, 10)
                : Column(face.transform, "Content", 4f, TextAnchor.MiddleCenter, 16, 6, 16, 10, fillWidth: false);
            Stretch(result.Content);

            if (icon != null)
            {
                float iconSize = iconOnly ? Mathf.Min(size.x, size.y - depth) * 0.6f : Mathf.Min(size.y * 0.56f, 92f);
                result.Icon = Picture(result.Content, "Icon", icon, new Vector2(iconSize, iconSize));
                result.Icon.color = IconTint(style);
            }
            if (!string.IsNullOrEmpty(label))
            {
                result.Label = Text(result.Content, label, LabelStyle(style), fontSize, LabelColor(style), TextAlignmentOptions.Center, FontStyles.Bold, "Label");
            }
            return result;
        }

        /// <summary>Adds a periodic light sweep across a button face (used on Premium and the main call to action).</summary>
        public void AddShine(UiButton button, float period = 3.2f)
        {
            var clip = Empty(button.FaceRect, "ShineClip");
            var clipRect = Rect(clip);
            Stretch(clipRect, 34f, 22f, 34f, 20f);
            clip.AddComponent<RectMask2D>();
            clip.transform.SetSiblingIndex(1); // above the face gloss, below the content

            var shineGo = Empty(clip.transform, "Shine");
            var shine = shineGo.AddComponent<Image>();
            shine.sprite = Icons.Get("shine");
            shine.raycastTarget = false;
            shine.color = new Color(1f, 1f, 1f, 0.75f);
            var rt = Rect(shineGo);
            Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120f, 200f));
            shineGo.SetActive(false);

            var sweep = clip.AddComponent<UiShineSweep>();
            sweep.Shine = rt;
            sweep.Clip = clipRect;
            sweep.Period = period;
        }

        /// <summary>Small dark lacquer chip: currency icon + amount (prices and rewards). Reports its own size to layout groups.</summary>
        public RectTransform PriceChip(Transform parent, Sprite icon, string amount, Color? amountColor = null, float height = 68f, int fontSize = 40)
        {
            var chip = Sliced(parent, "PriceChip", "pill_lacquer", false);
            var group = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            Configure(group, 8f, TextAnchor.MiddleCenter, 10, 2, 22, 2);
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            var layout = Size(chip.gameObject, height: height);
            layout.minWidth = height * 1.9f;
            Picture(chip.transform, "Icon", icon, new Vector2(height * 0.9f, height * 0.9f));
            Text(chip.transform, amount, TextStyle.Number, fontSize, amountColor ?? Palette.Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Amount");
            return Rect(chip.gameObject);
        }

        /// <summary>Gold-ringed medallion with an icon inside (headers, cards).</summary>
        public RectTransform Medallion(Transform parent, Sprite icon, float size)
        {
            var go = Empty(parent, "Medallion");
            var ring = go.AddComponent<Image>();
            ring.sprite = Icons.Get("medallion");
            ring.preserveAspect = true;
            ring.raycastTarget = false;
            Rect(go).sizeDelta = new Vector2(size, size);
            Size(go, size, size);
            var inner = Picture(go.transform, "Icon", icon, new Vector2(size * 0.68f, size * 0.68f));
            Place(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.01f), new Vector2(size * 0.68f, size * 0.68f));
            inner.GetComponent<LayoutElement>().ignoreLayout = true;
            return Rect(go);
        }

        /// <summary>Ornamental gold divider line.</summary>
        public Image Divider(Transform parent, float height = 22f)
        {
            var image = Sliced(parent, "Divider", "divider", false);
            Size(image.gameObject, height: height);
            return image;
        }

        /// <summary>Gives a group a CanvasGroup (for fades) and returns it.</summary>
        public static CanvasGroup Fadeable(GameObject go)
        {
            var group = go.GetComponent<CanvasGroup>();
            return group != null ? group : go.AddComponent<CanvasGroup>();
        }
    }
}
