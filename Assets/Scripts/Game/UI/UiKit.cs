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
        Secondary,  // cream
        Premium,    // gold, used only for Diamond actions
        Danger,     // red
        Dark        // dark glass
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

    /// <summary>Surface a small chip sits on: a light panel (cream chip), a cream card (white chip) or dark glass.</summary>
    public enum ChipTone
    {
        Light,
        Card,
        Dark
    }

    /// <summary>
    /// Shared sizes so every screen uses the same rhythm. Values are canvas units (reference height 1080, so about 2.7 units per dp
    /// on a phone held sideways). Deliberately compact: the settlement stays visible, the HUD stays out of the way.
    /// </summary>
    public static class UiTheme
    {
        /// <summary>Margin to the safe-area edge.</summary>
        public const float Gutter = 28f;
        public const float Gap = 16f;
        /// <summary>Smallest tappable size (about 37 dp, a little under the usual 40 dp icon button).</summary>
        public const float TouchMin = 100f;
        public const float ButtonHeight = 108f;
        public const float BarHeight = 84f;
        public const float ChipHeight = 48f;

        public const float PanelRadius = 40f;
        public const float CardRadius = 30f;

        public const int FontTitle = 48;
        public const int FontHeading = 40;
        public const int FontBody = 34;
        public const int FontCaption = 28;
        public const int FontNumber = 38;
    }

    /// <summary>
    /// Springy press feedback: the whole element squashes a little on press and overshoots slightly on release.
    /// Idle elements cost nothing (Update is off).
    /// </summary>
    public sealed class UiPressFeedback : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public RectTransform Root;
        public float PressedScale = 0.95f;

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
            velocity += ((target - value) * 900f - velocity * 32f) * dt;
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
            if (Root != null) Root.localScale = Vector3.one * Mathf.LerpUnclamped(1f, PressedScale, value);
        }

        private void OnDisable()
        {
            value = 0f;
            velocity = 0f;
            target = 0f;
            if (Root != null) Root.localScale = Vector3.one;
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

    /// <summary>Wraps one button: face, label, icon and visual states.</summary>
    public sealed class UiButton
    {
        public GameObject GameObject;
        public RectTransform Rect;
        public Image Face;
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
            Face.sprite = Kit.Icons.Get(newState == UiButtonState.Normal ? UiKit.SpriteName(Style) : "btn_disabled");
            if (Label != null) Label.color = newState == UiButtonState.Normal ? UiKit.LabelColor(Style) : Palette.InkSoft;
            if (Icon != null) Icon.color = newState == UiButtonState.Normal ? UiKit.IconTint(Style) : new Color(Palette.InkSoft.r, Palette.InkSoft.g, Palette.InkSoft.b, 0.7f);
        }

        public void SetStyle(UiButtonStyle style)
        {
            Style = style;
            SetState(state); // face sprite, label and icon colours follow the style
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
        /// <summary>Nunito: SemiBold for running text, ExtraBold wherever the text style asks for bold (static SDF atlases baked by the editor tool).</summary>
        private readonly TMP_FontAsset regularFont;
        private readonly TMP_FontAsset boldFont;

        public readonly IconSet Icons;
        public UiMotion Motion;

        /// <summary>Invoked on every button tap (wired to the audio hook by <see cref="GameUi"/>).</summary>
        public Action ClickFeedback;

        public UiKit(IconSet icons)
        {
            Icons = icons;
            regularFont = LoadFont("Nunito-SemiBold SDF");
            boldFont = LoadFont("Nunito-ExtraBold SDF");
        }

        private static TMP_FontAsset LoadFont(string name)
        {
            var font = Resources.Load<TMP_FontAsset>("UI/Fonts/" + name);
            if (font == null) throw new InvalidOperationException($"Font asset '{name}' missing. Run 'Age of Sakura > Generate UI Art'.");
            return font;
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

        public static Color LabelColor(UiButtonStyle style) =>
            style == UiButtonStyle.Secondary || style == UiButtonStyle.Premium ? Palette.Ink : Palette.Cream;

        public static Color IconTint(UiButtonStyle style) =>
            style == UiButtonStyle.Secondary || style == UiButtonStyle.Premium ? Palette.Ink : Palette.Cream;

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

        /// <summary>
        /// Sets the corner radius (canvas units) of a 9-sliced glass sprite. The sprite's border is its corner radius in pixels, so
        /// the same pill sprite serves every height: a pill is <c>Round(image, height / 2)</c>.
        /// </summary>
        public static void Round(Image image, float radius)
        {
            image.type = Image.Type.Sliced;
            float unitsPerPixel = 100f / image.sprite.pixelsPerUnit; // 100 = the canvas' reference pixels per unit
            image.pixelsPerUnitMultiplier = Mathf.Max(0.01f, image.sprite.border.x * unitsPerPixel / radius);
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

        /// <summary>A tappable card: sliced sprite + button with a gentle press squash. Add content via AddGroup.</summary>
        public Image Tile(Transform parent, string name, string spriteId, Vector2 size, out Button button)
        {
            var image = Sliced(parent, name, spriteId, true);
            Round(image, UiTheme.CardRadius);
            Size(image.gameObject, size.x, size.y);
            button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.02f, 1.02f, 1.02f, 1f);
            colors.pressedColor = new Color(0.92f, 0.9f, 0.86f, 1f);
            colors.selectedColor = Color.white;
            colors.fadeDuration = 0.04f;
            button.colors = colors;
            button.onClick.AddListener(() => ClickFeedback?.Invoke());
            var feedback = image.gameObject.AddComponent<UiPressFeedback>();
            feedback.Root = image.rectTransform;
            feedback.PressedScale = 0.97f;
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

        /// <summary>Frosted-glass surface with a corner radius in canvas units (<paramref name="spriteId"/>: panel_light, pill_light, pill_dark).</summary>
        public Image Glass(Transform parent, string name, string spriteId, float radius, bool blocksInput = true)
        {
            var image = Sliced(parent, name, spriteId, blocksInput);
            Round(image, radius);
            return image;
        }

        /// <summary>
        /// Soft drop shadow. Two offset copies of different strength fake a blurred edge; a negative offset casts it downwards,
        /// a small positive one lifts a panel that touches the bottom edge of the screen.
        /// </summary>
        public static void AddShadow(GameObject go, float offsetY = -8f, float alpha = 0.3f)
        {
            var near = go.AddComponent<Shadow>();
            near.effectColor = new Color(0.12f, 0.07f, 0.03f, alpha * 0.55f);
            near.effectDistance = new Vector2(0f, offsetY * 0.4f);
            var far = go.AddComponent<Shadow>();
            far.effectColor = new Color(0.12f, 0.07f, 0.03f, alpha * 0.3f);
            far.effectDistance = new Vector2(0f, offsetY);
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
        public TMP_Text Text(Transform parent, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center,
            FontStyles fontStyle = FontStyles.Normal, string name = "Text", bool wrap = false)
        {
            var go = Empty(parent, name);
            var t = go.AddComponent<TextMeshProUGUI>();
            // bold comes from the ExtraBold face, not from synthetic emboldening
            t.font = (fontStyle & FontStyles.Bold) != 0 ? boldFont : regularFont;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = fontStyle & ~FontStyles.Bold;
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
        public TMP_Text Title(Transform parent, string text, float size = UiTheme.FontHeading, string name = "Title") =>
            Text(parent, text, size, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, name);

        public TMP_Text Body(Transform parent, string text, float size = UiTheme.FontBody, string name = "Body") =>
            Text(parent, text, size, Palette.InkSoft, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, name);

        public UiButton Button(Transform parent, string name, string label, Vector2 size, UiButtonStyle style, Sprite icon = null,
            int fontSize = UiTheme.FontHeading, bool iconOnly = false, ButtonLayout layout = ButtonLayout.Row)
        {
            var face = Sliced(parent, name, SpriteName(style), true);
            var root = face.gameObject;
            var rootRect = face.rectTransform;
            rootRect.sizeDelta = size;
            Size(root, size.x, size.y);
            Round(face, layout == ButtonLayout.Row ? size.y * 0.5f : UiTheme.CardRadius);
            AddShadow(root, -7f, 0.24f);

            var button = root.AddComponent<Button>();
            button.targetGraphic = face;
            button.transition = Selectable.Transition.ColorTint;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.03f, 1.03f, 1.03f, 1f);
            colors.pressedColor = new Color(0.92f, 0.92f, 0.92f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.04f;
            button.colors = colors;
            button.onClick.AddListener(() => ClickFeedback?.Invoke());

            var feedback = root.AddComponent<UiPressFeedback>();
            feedback.Root = rootRect;

            var result = new UiButton { GameObject = root, Rect = rootRect, Face = face, Button = button, Style = style, Kit = this };

            int pad = Mathf.RoundToInt(size.y * 0.3f);
            result.Content = layout == ButtonLayout.Row
                ? Row(root.transform, "Content", 12f, TextAnchor.MiddleCenter, pad, 0, pad, 0)
                : Column(root.transform, "Content", 4f, TextAnchor.MiddleCenter, 12, 8, 12, 8, fillWidth: false);
            Stretch(result.Content);

            if (icon != null)
            {
                float iconSize = iconOnly ? Mathf.Min(size.x, size.y) * 0.5f : Mathf.Min(size.y * 0.5f, 64f);
                result.Icon = Picture(result.Content, "Icon", icon, new Vector2(iconSize, iconSize));
                result.Icon.color = IconTint(style);
            }
            if (!string.IsNullOrEmpty(label))
            {
                result.Label = Text(result.Content, label, fontSize, LabelColor(style), TextAlignmentOptions.Center, FontStyles.Bold, "Label");
            }
            return result;
        }

        /// <summary>Round (square) icon-only button.</summary>
        public UiButton IconButton(Transform parent, string name, string iconId, float size, UiButtonStyle style) =>
            Button(parent, name, string.Empty, new Vector2(size, size), style, Icons.Get(iconId), iconOnly: true);

        /// <summary>
        /// Small chip: currency icon + amount (prices, rewards). <paramref name="lacking"/> tints a light chip red (the player is short).
        /// Reports its own size to layout groups.
        /// </summary>
        public RectTransform PriceChip(Transform parent, Sprite icon, string amount, ChipTone tone = ChipTone.Light, bool lacking = false, float height = UiTheme.ChipHeight)
        {
            bool dark = tone == ChipTone.Dark;
            var chip = Sliced(parent, "PriceChip", dark ? "pill_dark" : "pill_flat", false);
            if (!dark) chip.color = lacking ? Palette.UiRedSoft : (tone == ChipTone.Card ? Color.white : Palette.CreamDeep);
            Round(chip, height * 0.5f);
            var group = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            Configure(group, height * 0.12f, TextAnchor.MiddleCenter, Mathf.RoundToInt(height * 0.14f), 0, Mathf.RoundToInt(height * 0.36f), 0);
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;
            Size(chip.gameObject, height: height).minWidth = height * 1.6f;
            float iconSize = height * 0.78f;
            Picture(chip.transform, "Icon", icon, new Vector2(iconSize, iconSize));
            Color textColor = dark ? Palette.Cream : (lacking ? Palette.UiRed : Palette.Ink);
            var text = Text(chip.transform, amount, height * 0.64f, textColor, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Amount");
            Size(text.gameObject, height: height);
            return Rect(chip.gameObject);
        }

        /// <summary>Thin hairline.</summary>
        public Image Line(Transform parent, float thickness = 2f, Color? color = null)
        {
            var go = Empty(parent, "Line");
            var image = go.AddComponent<Image>();
            image.color = color ?? Palette.CreamLine;
            image.raycastTarget = false;
            Size(go, height: thickness);
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
