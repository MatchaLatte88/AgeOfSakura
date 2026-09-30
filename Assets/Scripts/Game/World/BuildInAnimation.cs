using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// The style's construction animation: the model's groups drop in one after another (ground, body, roof, trees, props) within
    /// <see cref="ToonStyle.BuildInSeconds"/>, each with a bouncing drop-in. Only the groups move; the tap collider and all gameplay
    /// state are untouched. Models without those groups (replacement prefabs) simply skip the animation.
    /// </summary>
    public sealed class BuildInAnimation : MonoBehaviour
    {
        /// <summary>Screenshot tooling sets this to a large value to finish instantly.</summary>
        public static float SpeedMultiplier = 1f;

        private const float DropHeight = 1.6f;

        private Transform[] groups;
        private float age;
        private bool playing;

        private void Awake() => enabled = false;

        public void Play()
        {
            groups = new Transform[ToonStyle.BuildOrder.Length];
            bool any = false;
            for (int i = 0; i < groups.Length; i++)
            {
                groups[i] = transform.Find(ToonStyle.BuildOrder[i]);
                any |= groups[i] != null;
            }
            if (!any) return;
            age = 0f;
            playing = true;
            enabled = true;
            Apply(0f);
        }

        private void Update()
        {
            if (!playing) { enabled = false; return; }
            age += Time.unscaledDeltaTime * SpeedMultiplier;
            float p = Mathf.Clamp01(age / ToonStyle.BuildInSeconds);
            Apply(p);
            if (p >= 1f)
            {
                playing = false;
                enabled = false;
            }
        }

        private void Apply(float p)
        {
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g == null) continue;
                var window = ToonStyle.BuildWindows[i];
                float t = Mathf.Clamp01((p - window.x) / (window.y - window.x));
                bool visible = t > 0f;
                if (g.gameObject.activeSelf != visible) g.gameObject.SetActive(visible);
                float drop = (1f - BounceOut(t)) * DropHeight;
                g.localPosition = new Vector3(0f, drop, 0f);
                float s = Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(t * 3f));
                g.localScale = new Vector3(s, s, s);
            }
        }

        /// <summary>Standard bounce-out easing (the style's "drop-in").</summary>
        private static float BounceOut(float t)
        {
            const float n1 = 7.5625f, d1 = 2.75f;
            if (t < 1f / d1) return n1 * t * t;
            if (t < 2f / d1) { t -= 1.5f / d1; return n1 * t * t + 0.75f; }
            if (t < 2.5f / d1) { t -= 2.25f / d1; return n1 * t * t + 0.9375f; }
            t -= 2.625f / d1;
            return n1 * t * t + 0.984375f;
        }
    }
}
