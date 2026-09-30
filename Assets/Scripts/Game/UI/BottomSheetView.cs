using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Reusable contextual bottom sheet: a lacquer-framed washi panel that springs up from the bottom edge. A lacquer title plaque with a
    /// gold medallion overhangs its top edge and a red close badge sits on the top-right corner. Presenters swap the content
    /// (build menu, building info, production choice/running/ready) through <see cref="SetHeader"/> and <see cref="Content"/>.
    /// No per-building UI classes.
    /// </summary>
    public sealed class BottomSheetView : MonoBehaviour
    {
        private const float Height = 440f;
        private const float MaxWidth = 1720f;
        private const float RestY = 14f;

        private UiKit kit;
        private RectTransform rect;
        private RectTransform parentRect;
        private CanvasGroup group;
        private TMP_Text titleText;
        private TMP_Text subtitleText;
        private Image medallionIcon;
        private float amount;     // 0 hidden .. 1 shown (may overshoot while springing)
        private bool wantShown;

        public RectTransform Content { get; private set; }
        public bool IsShown => wantShown;

        /// <summary>Raised when the close badge is tapped.</summary>
        public event Action CloseRequested;

        public static BottomSheetView Create(UiKit kit, RectTransform parent)
        {
            var panel = kit.Sliced(parent, "BottomSheet", "panel_paper", true);
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
            UiKit.AddShadow(gameObject, 14f, 0.32f);
            UiKit.Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -Height), new Vector2(MaxWidth, Height));

            // faint paper fibre over the washi surface
            var noise = kit.Sliced(rect, "PaperNoise", "paper_noise", false);
            noise.type = Image.Type.Tiled;
            noise.color = new Color(1f, 1f, 1f, 0.9f);
            UiKit.Stretch(noise.rectTransform, 26f, 26f, 26f, 26f);

            // the body sits below the overhanging plaque
            Content = kit.Column(rect, "Content", 4f, TextAnchor.MiddleCenter, 70, 78, 70, 40, fillWidth: true);
            UiKit.Stretch(Content);

            BuildPlaque();
            BuildCloseBadge();

            amount = 0f;
            Apply();
        }

        private void BuildPlaque()
        {
            var plaque = kit.Sliced(rect, "Plaque", "plaque", false);
            UiKit.AddShadow(plaque.gameObject, -8f, 0.34f);
            UiKit.Place(plaque.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(760f, 124f));
            UiKit.AddGroup(plaque.gameObject, true, 0f, TextAnchor.MiddleCenter, 190, 8, 78, 16, fill: true);
            var fitter = plaque.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UiKit.Size(plaque.gameObject, height: 124f).minWidth = 700f;

            titleText = kit.Text(plaque.transform, string.Empty, TextStyle.Light, 62, Palette.Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Title");
            UiKit.Size(titleText.gameObject, height: 70f);
            subtitleText = kit.Text(plaque.transform, string.Empty, TextStyle.Light, 34, new Color(0.93f, 0.85f, 0.68f), TextAlignmentOptions.MidlineLeft, FontStyles.Normal, "Subtitle");
            UiKit.Size(subtitleText.gameObject, height: 40f);

            var medallion = kit.Medallion(plaque.transform, kit.Icons.Get("hammer"), 148f);
            medallion.GetComponent<LayoutElement>().ignoreLayout = true;
            UiKit.Place(medallion, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(86f, 4f), new Vector2(148f, 148f));
            medallionIcon = medallion.Find("Icon").GetComponent<Image>();
        }

        private void BuildCloseBadge()
        {
            var close = kit.Button(rect, "Close", string.Empty, new Vector2(112f, 112f), UiButtonStyle.Danger, kit.Icons.Get("cross"), iconOnly: true);
            UiKit.Place(close.Rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 0.5f), new Vector2(-30f, -26f), new Vector2(112f, 112f));
            close.OnClick(() => CloseRequested?.Invoke());
        }

        /// <summary>Sets the plaque: medallion icon, title and a one-line subtitle.</summary>
        public void SetHeader(Sprite icon, string title, string subtitle)
        {
            if (icon != null) medallionIcon.sprite = icon;
            titleText.text = title;
            subtitleText.text = subtitle;
            LayoutRebuilder.ForceRebuildLayoutImmediate(titleText.transform.parent as RectTransform);
        }

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
            motion.Play(shown ? 0.5f : 0.22f, shown ? (Func<float, float>)Ease.OutBackSoft : Ease.InCubic,
                t => { amount = Mathf.LerpUnclamped(from, to, t); Apply(); }, 0f, () => { amount = to; Apply(); }, this);
        }

        private void Update()
        {
            // keep the width in step with the (safe-area) canvas: shrinks on narrow 4:3 screens
            float width = Mathf.Max(600f, Mathf.Min(MaxWidth, parentRect.rect.width - 24f));
            if (!Mathf.Approximately(rect.sizeDelta.x, width)) rect.sizeDelta = new Vector2(width, Height);
        }

        private void Apply()
        {
            // the plaque overhangs by ~70 units, so hide the sheet a bit further down
            rect.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(-Height - 110f, RestY, amount));
            group.alpha = Mathf.Clamp01(amount * 4f);
            bool interactive = wantShown && amount > 0.5f;
            group.blocksRaycasts = interactive;
            group.interactable = interactive;
        }
    }
}
