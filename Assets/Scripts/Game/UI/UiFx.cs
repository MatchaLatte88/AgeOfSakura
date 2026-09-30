using System.Collections.Generic;
using AgeOfSakura.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Collection juice: a sparkle burst where the reward appears, a chunky "+N" pop-up, and resource icons that arc up to the HUD
    /// with a glowing trail and make the counter bounce on arrival. Short, no external assets, all placed on one UI layer.
    /// </summary>
    public sealed class UiFx : MonoBehaviour
    {
        private const int TrailLength = 3;

        private sealed class Flight
        {
            public Image Main;
            public Image[] Trail = new Image[TrailLength];
            /// <summary>Goods travelling from the store to a building (supply chain): no HUD pulse on arrival.</summary>
            public bool Deliver;
            public Vector2 From;
            public Vector2 Mid;
            public Vector2 To;
            public float Delay;
            public float Duration;
            public float Age;
            public CurrencyType Currency;
        }

        private sealed class Popup
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Vector2 Start;
            public float Age;
        }

        private sealed class Spark
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Start;
            public Vector2 Velocity;
            public float Spin;
            public float Age;
            public float Life;
            public float Size;
        }

        private RectTransform layer;
        private UiKit kit;
        private HudView hud;
        private readonly List<Flight> flights = new List<Flight>(16);
        private readonly List<Popup> popups = new List<Popup>(4);
        private readonly List<Spark> sparks = new List<Spark>(24);

        public static UiFx Create(UiKit kit, RectTransform canvasRoot, HudView hud)
        {
            var go = kit.Empty(canvasRoot, "FxLayer");
            var fx = go.AddComponent<UiFx>();
            fx.layer = UiKit.Rect(go);
            UiKit.Stretch(fx.layer);
            fx.kit = kit;
            fx.hud = hud;
            fx.enabled = false;
            return fx;
        }

        /// <summary>Plays the collect effect for rewards paid out at <paramref name="screenPosition"/>.</summary>
        public void PlayCollect(Vector2 screenPosition, IReadOnlyList<CurrencyAmount> rewards)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenPosition, null, out var start);
            float yOffset = 110f;
            foreach (var reward in rewards)
            {
                AddPopup(start + new Vector2(0f, yOffset), reward);
                yOffset -= 96f;

                var target = hud.IconOf(reward.Currency);
                if (target == null) continue;
                Vector2 end = layer.InverseTransformPoint(target.position);
                int count = (int)Mathf.Clamp(reward.Amount, 3, 8);
                for (int i = 0; i < count; i++) AddFlight(start, end, reward.Currency, 0.12f + i * 0.06f);
            }
            AddBurst(start, 12);
            enabled = true;
        }

        /// <summary>Goods leave the HUD counter and fly to a building: the visible half of the supply chain.</summary>
        public void PlayDelivery(Vector2 screenTarget, IReadOnlyList<CurrencyAmount> goods)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layer, screenTarget, null, out var end);
            foreach (var good in goods)
            {
                var source = hud.IconOf(good.Currency);
                if (source == null) continue;
                Vector2 start = layer.InverseTransformPoint(source.position);
                int count = (int)Mathf.Clamp(good.Amount, 2, 5);
                for (int i = 0; i < count; i++) AddFlight(start, end, good.Currency, 0.05f + i * 0.08f, deliver: true);
            }
            enabled = true;
        }

        // ------------------------------------------------------------------ spawners

        private void AddBurst(Vector2 origin, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var go = kit.Empty(layer, "Spark");
                var image = go.AddComponent<Image>();
                image.sprite = kit.Icons.Get("sparkle");
                image.raycastTarget = false;
                image.color = i % 3 == 0 ? Palette.Cream : Palette.GoldInlay;
                var rt = UiKit.Rect(go);
                UiKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), origin, new Vector2(64f, 64f));
                float angle = (i / (float)count + Random.Range(-0.04f, 0.04f)) * Mathf.PI * 2f;
                float speed = Random.Range(170f, 330f);
                sparks.Add(new Spark
                {
                    Rect = rt, Image = image, Start = origin, Velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                    Spin = Random.Range(-240f, 240f), Life = Random.Range(0.55f, 0.85f), Size = Random.Range(0.6f, 1.25f)
                });
            }
        }

        private void AddPopup(Vector2 position, CurrencyAmount reward)
        {
            var root = kit.Empty(layer, "Popup");
            var rt = UiKit.Rect(root);
            UiKit.Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, new Vector2(340f, 110f));
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            UiKit.AddGroup(root, false, 10f, TextAnchor.MiddleCenter);

            var text = kit.Text(root.transform, "+" + reward.Amount, TextStyle.Number, 92, Palette.Cream, TextAlignmentOptions.Midline, FontStyles.Bold, "Amount");
            UiKit.Size(text.gameObject, height: 104f);
            kit.Picture(root.transform, "Icon", kit.Icons.ForCurrency(reward.Currency), new Vector2(96f, 96f));
            popups.Add(new Popup { Root = rt, Group = group, Start = position });
        }

        private void AddFlight(Vector2 from, Vector2 to, CurrencyType currency, float delay, bool deliver = false)
        {
            var flight = new Flight
            {
                Deliver = deliver,
                From = from,
                Mid = from + Random.insideUnitCircle * 150f + new Vector2(0f, 90f),
                To = to,
                Delay = delay,
                Duration = Random.Range(0.6f, 0.82f),
                Currency = currency
            };
            for (int i = 0; i < TrailLength; i++)
            {
                flight.Trail[i] = MakeFlyImage("Trail", "glow", 70f - i * 12f);
                flight.Trail[i].color = new Color(1f, 0.86f, 0.45f, 0f);
            }
            flight.Main = MakeFlyImage("Fly", null, 78f);
            flight.Main.sprite = kit.Icons.ForCurrency(currency);
            flights.Add(flight);
        }

        private Image MakeFlyImage(string name, string spriteId, float size)
        {
            var go = kit.Empty(layer, name);
            var image = go.AddComponent<Image>();
            if (spriteId != null) image.sprite = kit.Icons.Get(spriteId);
            image.preserveAspect = true;
            image.raycastTarget = false;
            UiKit.Place(UiKit.Rect(go), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            go.SetActive(false);
            return image;
        }

        // ------------------------------------------------------------------ update

        private static Vector2 Bezier(Flight f, float t)
        {
            var a = Vector2.Lerp(f.From, f.Mid, t);
            var b = Vector2.Lerp(f.Mid, f.To, t);
            return Vector2.Lerp(a, b, t);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var s = sparks[i];
                s.Age += dt;
                float t = s.Age / s.Life;
                if (t >= 1f) { Destroy(s.Rect.gameObject); sparks.RemoveAt(i); continue; }
                float drag = 1f - Ease.OutCubic(t);
                s.Rect.anchoredPosition = s.Start + s.Velocity * (1f - drag) * 0.6f;
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, s.Spin * s.Age);
                s.Rect.localScale = Vector3.one * (s.Size * Mathf.Sin(Mathf.Clamp01(t * 1.4f) * Mathf.PI * 0.5f) * (1f - t * 0.5f));
                var c = s.Image.color;
                c.a = 1f - Ease.InCubic(t);
                s.Image.color = c;
            }

            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.Age += dt;
                float t = p.Age / 1.3f;
                if (t >= 1f) { Destroy(p.Root.gameObject); popups.RemoveAt(i); continue; }
                float pop = Ease.OutBack(Mathf.Clamp01(p.Age / 0.28f));
                p.Root.localScale = Vector3.one * pop;
                p.Root.anchoredPosition = p.Start + new Vector2(0f, 120f * Ease.OutCubic(t));
                p.Group.alpha = t < 0.68f ? 1f : 1f - (t - 0.68f) / 0.32f;
            }

            for (int i = flights.Count - 1; i >= 0; i--)
            {
                var f = flights[i];
                f.Age += dt;
                float local = f.Age - f.Delay;
                if (local < 0f) continue;
                float t = Mathf.Clamp01(local / f.Duration);
                float eased = Ease.InOutSine(t);
                if (!f.Main.gameObject.activeSelf) f.Main.gameObject.SetActive(true);
                var pos = Bezier(f, eased);
                f.Main.rectTransform.anchoredPosition = pos;
                f.Main.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.62f, eased);
                for (int k = 0; k < TrailLength; k++)
                {
                    float tt = Mathf.Clamp01(eased - 0.055f * (k + 1));
                    var trail = f.Trail[k];
                    if (!trail.gameObject.activeSelf) trail.gameObject.SetActive(true);
                    trail.rectTransform.anchoredPosition = Bezier(f, tt);
                    var c = trail.color;
                    c.a = (0.55f - 0.15f * k) * Mathf.Clamp01(1f - t * 1.1f);
                    trail.color = c;
                }
                if (t >= 1f)
                {
                    if (!f.Deliver) hud.Pulse(f.Currency);
                    Destroy(f.Main.gameObject);
                    foreach (var trail in f.Trail) Destroy(trail.gameObject);
                    flights.RemoveAt(i);
                }
            }

            if (popups.Count == 0 && flights.Count == 0 && sparks.Count == 0) enabled = false;
        }
    }
}
