using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Short, non-blocking message ("Not enough Wood") on a lacquer pill with an alert icon. Slides down from the top edge with a
    /// soft overshoot, waits, then fades. Never opens a store or a modal.
    /// </summary>
    public sealed class ToastView : MonoBehaviour
    {
        private const float RestY = -150f;
        private const float SlideSeconds = 0.42f;
        private const float FadeSeconds = 0.45f;

        private UiKit kit;
        private RectTransform rect;
        private CanvasGroup group;
        private TMP_Text text;
        private float age;
        private float lifetime;

        public static ToastView Create(UiKit kit, RectTransform parent)
        {
            var panel = kit.Sliced(parent, "Toast", "pill_lacquer", false);
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
            UiKit.AddShadow(gameObject, -8f, 0.34f);

            UiKit.Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, RestY), new Vector2(200f, 108f));
            UiKit.AddGroup(gameObject, false, 18f, TextAnchor.MiddleCenter, 26, 8, 46, 10);
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;
            UiKit.Size(gameObject, height: 108f);

            kit.Picture(transform, "Icon", "alert", new Vector2(70f, 70f));
            text = kit.Text(transform, string.Empty, TextStyle.Light, 42, Palette.Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Message");
            UiKit.Size(text.gameObject, height: 70f);
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
            rect.anchoredPosition = new Vector2(0f, RestY + 90f);
            enabled = true;
        }

        private void Update()
        {
            age += Time.unscaledDeltaTime;
            float slide = Mathf.Clamp01(age / SlideSeconds);
            rect.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(RestY + 90f, RestY, Ease.OutBack(slide)));
            float fade = Mathf.Clamp01((lifetime - age) / FadeSeconds);
            group.alpha = Mathf.Min(fade, Mathf.Clamp01(age * 8f));
            if (age >= lifetime) enabled = false;
        }
    }
}
