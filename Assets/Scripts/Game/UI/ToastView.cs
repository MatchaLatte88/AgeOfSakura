using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Short, non-blocking message ("Not enough Wood") on a dark glass pill with an alert icon. Slides down a little from under the
    /// resource bar, waits, then fades. Never opens a store or a modal.
    /// </summary>
    public sealed class ToastView : MonoBehaviour
    {
        private const float Height = 76f;
        private const float RestY = -24f - UiTheme.BarHeight - 20f;
        private const float SlideDistance = 36f;
        private const float SlideSeconds = 0.3f;
        private const float FadeSeconds = 0.4f;

        private UiKit kit;
        private RectTransform rect;
        private CanvasGroup group;
        private TMP_Text text;
        private float age;
        private float lifetime;

        public static ToastView Create(UiKit kit, RectTransform parent)
        {
            var panel = kit.Glass(parent, "Toast", "pill_dark", Height * 0.5f, false);
            var view = panel.gameObject.AddComponent<ToastView>();
            view.Build(kit, panel);
            return view;
        }

        private void Build(UiKit kitRef, Image panel)
        {
            kit = kitRef;
            rect = panel.rectTransform;
            group = gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            UiKit.AddShadow(gameObject, -6f, 0.3f);

            UiKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, RestY), new Vector2(200f, Height));
            UiKit.AddGroup(gameObject, false, 14f, TextAnchor.MiddleCenter, 18, 0, 34, 0);
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            UiKit.Size(gameObject, height: Height);

            kit.Picture(transform, "Icon", "alert", new Vector2(44f, 44f));
            text = kit.Text(transform, string.Empty, UiTheme.FontBody, Palette.Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Message");
            UiKit.Size(text.gameObject, height: Height);
            enabled = false;
        }

        public void Show(string message, float seconds = 2.8f)
        {
            if (string.IsNullOrEmpty(message)) { age = lifetime = 0f; group.alpha = 0f; enabled = false; return; }
            text.text = message;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            age = 0f;
            lifetime = seconds;
            group.alpha = 1f;
            rect.anchoredPosition = new Vector2(0f, RestY + SlideDistance);
            enabled = true;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float slide = Mathf.Clamp01(age / SlideSeconds);
            rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(RestY + SlideDistance, RestY, Ease.OutCubic(slide)));
            float fade = Mathf.Clamp01((lifetime - age) / FadeSeconds);
            group.alpha = Mathf.Min(fade, Mathf.Clamp01(age * 8f));
            if (age >= lifetime) enabled = false;
        }
    }
}
