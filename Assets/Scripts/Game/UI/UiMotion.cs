using System;
using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>Easing curves (t in 0..1). Overshooting curves (OutBack, OutElastic) give the UI its springy, tactile feel.</summary>
    public static class Ease
    {
        public static float Linear(float t) => t;

        public static float OutQuad(float t) => 1f - (1f - t) * (1f - t);

        public static float OutCubic(float t)
        {
            t = 1f - t;
            return 1f - t * t * t;
        }

        public static float InCubic(float t) => t * t * t;

        public static float InOutSine(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t);

        public static float OutBack(float t)
        {
            const float s = 1.70158f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        /// <summary>Gentler overshoot than <see cref="OutBack"/>; good for panels.</summary>
        public static float OutBackSoft(float t)
        {
            const float s = 0.9f;
            t -= 1f;
            return t * t * ((s + 1f) * t + s) + 1f;
        }

        public static float OutElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return Mathf.Pow(2f, -10f * t) * Mathf.Sin((t * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }
    }

    /// <summary>
    /// One central tween runner for all UI motion (unscaled time, so it works while the game is paused).
    /// A tween can be tied to an owner object; when that object is destroyed the tween silently ends.
    /// </summary>
    public sealed class UiMotion : MonoBehaviour
    {
        private sealed class Tween
        {
            public float Age;
            public float Delay;
            public float Duration;
            public Func<float, float> Ease;
            public Action<float> Apply;
            public Action Done;
            public UnityEngine.Object Owner;
            public bool HasOwner;
        }

        private readonly List<Tween> tweens = new List<Tween>(32);

        public void Play(float duration, Func<float, float> ease, Action<float> apply, float delay = 0f, Action onDone = null, UnityEngine.Object owner = null)
        {
            tweens.Add(new Tween
            {
                Duration = Mathf.Max(0.0001f, duration),
                Delay = delay,
                Ease = ease ?? Game.Ease.Linear,
                Apply = apply,
                Done = onDone,
                Owner = owner,
                HasOwner = owner != null
            });
            enabled = true;
        }

        /// <summary>Stops every tween tied to <paramref name="owner"/> without finishing it.</summary>
        public void Cancel(UnityEngine.Object owner)
        {
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                if (tweens[i].HasOwner && tweens[i].Owner == owner) tweens.RemoveAt(i);
            }
        }

        /// <summary>Scale + fade in with a soft overshoot. The target keeps its final scale of 1.</summary>
        public void PopIn(RectTransform target, CanvasGroup group, float delay = 0f, float duration = 0.36f, float fromScale = 0.84f)
        {
            target.localScale = Vector3.one * fromScale;
            if (group != null) group.alpha = 0f;
            Play(duration, Game.Ease.OutBack, t =>
            {
                target.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, 1f, t);
                if (group != null) group.alpha = Mathf.Clamp01(t * 2.2f);
            }, delay, () =>
            {
                target.localScale = Vector3.one;
                if (group != null) group.alpha = 1f;
            }, target);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = tweens.Count - 1; i >= 0; i--)
            {
                var tw = tweens[i];
                if (tw.HasOwner && tw.Owner == null) { tweens.RemoveAt(i); continue; }
                if (tw.Delay > 0f)
                {
                    tw.Delay -= dt;
                    if (tw.Delay > 0f) continue;
                    dt = Mathf.Max(dt, 0f);
                }
                tw.Age += dt;
                float t = Mathf.Clamp01(tw.Age / tw.Duration);
                tw.Apply(tw.Ease(t));
                if (t >= 1f)
                {
                    tweens.RemoveAt(i);
                    tw.Done?.Invoke();
                }
            }
            if (tweens.Count == 0) enabled = false;
        }
    }
}
