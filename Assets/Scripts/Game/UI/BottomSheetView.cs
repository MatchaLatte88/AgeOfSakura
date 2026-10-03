using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Reusable contextual bottom sheet: a frosted light panel that slides up from the bottom edge and takes its height from its
    /// content, so the settlement stays visible. A header row (icon, title, one-line subtitle, close button) sits above a hairline;
    /// presenters swap the content below it (build menu, building info, production choice/running/ready) through
    /// <see cref="SetHeader"/> and <see cref="Content"/>. No per-building UI classes.
    /// </summary>
    public sealed class BottomSheetView : MonoBehaviour
    {
        private const float MaxWidth = 1500f;
        private const float RestY = 16f;
        private const float HeaderHeight = 92f;

        private UiKit kit;
        private RectTransform rect;
        private RectTransform parentRect;
        private CanvasGroup group;
        private TMP_Text titleText;
        private TMP_Text subtitleText;
        private Image icon;
        private float amount;     // 0 hidden .. 1 shown (may overshoot while springing)
        private bool wantShown;

        public RectTransform Content { get; private set; }
        public bool IsShown => wantShown;

        /// <summary>Raised when the close button is tapped.</summary>
        public event Action CloseRequested;

        public static BottomSheetView Create(UiKit kit, RectTransform parent)
        {
            var panel = kit.Glass(parent, "BottomSheet", "panel_light", UiTheme.PanelRadius);
            var view = panel.gameObject.AddComponent<BottomSheetView>();
            view.Build(kit, panel, parent);
            return view;
        }

        private void Build(UiKit kitRef, Image panel, RectTransform parent)
        {
            kit = kitRef;
            rect = panel.rectTransform;
            parentRect = parent;
            group = gameObject.AddComponent<CanvasGroup>();
            UiKit.AddShadow(gameObject, 8f, 0.3f);
            UiKit.Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -400f), new Vector2(MaxWidth, 0f));

            var column = UiKit.AddGroup(gameObject, true, 12f, TextAnchor.UpperCenter, 32, 18, 32, 26, fill: true);
            column.childForceExpandHeight = false;
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            BuildHeader();
            kit.Line(rect);
            Content = kit.Column(rect, "Content", 0f, TextAnchor.UpperCenter, fillWidth: true);

            amount = 0f;
            Apply();
        }

        private void BuildHeader()
        {
            var header = kit.Row(rect, "Header", 16f, TextAnchor.MiddleLeft);
            UiKit.Size(header.gameObject, height: HeaderHeight);

            icon = kit.Picture(header, "Icon", kit.Icons.Get("hammer"), new Vector2(68f, 68f));

            var titles = kit.Column(header, "Titles", 0f, TextAnchor.MiddleLeft);
            UiKit.Size(titles.gameObject, flexWidth: 1f);
            titleText = kit.Text(titles, string.Empty, UiTheme.FontHeading + 2, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Title");
            UiKit.Size(titleText.gameObject, height: 52f);
            subtitleText = kit.Text(titles, string.Empty, UiTheme.FontCaption, Palette.InkSoft, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, "Subtitle");
            UiKit.Size(subtitleText.gameObject, height: 36f);

            var close = kit.IconButton(header, "Close", "cross", 84f, UiButtonStyle.Secondary);
            close.OnClick(() => CloseRequested?.Invoke());
        }

        /// <summary>Sets the header: icon, title and a one-line subtitle.</summary>
        public void SetHeader(Sprite headerIcon, string title, string subtitle)
        {
            icon.enabled = headerIcon != null;
            if (headerIcon != null) icon.sprite = headerIcon;
            titleText.text = title;
            subtitleText.text = subtitle;
            subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle)); // without one the title centres on the icon
        }

        /// <summary>Resolves the layout now (after the presenter swapped the content) so nothing flashes at the wrong size.</summary>
        public void Relayout() => LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

        public void Show(bool shown)
        {
            if (wantShown == shown && (shown ? amount > 0.99f : amount < 0.01f)) return;
            wantShown = shown;
            var motion = kit.Motion;
            if (motion == null)
            {
                amount = shown ? 1f : 0f;
                Apply();
                return;
            }
            motion.Cancel(this);
            float from = amount;
            float to = shown ? 1f : 0f;
            motion.Play(shown ? 0.38f : 0.2f, shown ? (Func<float, float>)Ease.OutBackSoft : Ease.InCubic,
                t => { amount = Mathf.LerpUnclamped(from, to, t); Apply(); }, 0f, () => { amount = to; Apply(); }, this);
        }

        private void Update()
        {
            // keep the width in step with the (safe-area) canvas: shrinks on narrow 4:3 screens
            float width = Mathf.Max(600f, Mathf.Min(MaxWidth, parentRect.rect.width - 2f * UiTheme.Gutter));
            if (!Mathf.Approximately(rect.sizeDelta.x, width)) rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        private void Apply()
        {
            float hidden = -Mathf.Max(rect.rect.height, 300f) - 40f;
            rect.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(hidden, RestY, amount));
            group.alpha = Mathf.Clamp01(amount * 4f);
            bool interactive = wantShown && amount > 0.5f;
            group.blocksRaycasts = interactive;
            group.interactable = interactive;
        }
    }
}
