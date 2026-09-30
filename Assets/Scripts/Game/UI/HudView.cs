using System.Collections.Generic;
using AgeOfSakura.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Top resource bar: dark lacquer chips with a gold rim, oversized glossy icons that break out of the chip's left edge,
    /// chunky outlined numbers that count up smoothly. Updated by <see cref="Wallet.CurrencyChanged"/> events (no polling).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private sealed class Chip
        {
            public CurrencyType Currency;
            public TMP_Text Text;
            public RectTransform Root;
            public RectTransform Icon;
            public Image Glow;
            public long Target;
            public float Shown;
            public int LastShownInt = int.MinValue;
            public float FlashUntil;
        }

        private const float ChipWidth = 290f;
        private const float ChipHeight = 98f;

        private readonly List<Chip> chips = new List<Chip>(4);
        private UiKit kit;

        public static HudView Create(UiKit kit, RectTransform parent, Wallet wallet)
        {
            var go = kit.Empty(parent, "Hud");
            var hud = go.AddComponent<HudView>();
            hud.Build(kit, go.GetComponent<RectTransform>(), wallet);
            return hud;
        }

        private void Build(UiKit kitRef, RectTransform root, Wallet wallet)
        {
            kit = kitRef;
            UiKit.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(34f, -26f), new Vector2(1400f, 110f));
            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 44f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            layout.padding = new RectOffset(28, 0, 0, 0); // room for the icons that overhang the chips

            foreach (CurrencyType currency in new[] { CurrencyType.Coins, CurrencyType.Wood, CurrencyType.Rice, CurrencyType.Diamonds })
            {
                var chip = new Chip { Currency = currency };
                var pill = kit.Sliced(root, currency + "Chip", "pill_lacquer", true);
                chip.Root = pill.rectTransform;
                UiKit.Size(pill.gameObject, ChipWidth, ChipHeight);
                UiKit.AddShadow(pill.gameObject, -7f, 0.34f);

                var glow = kit.Picture(pill.transform, "Glow", "glow", new Vector2(190f, 190f));
                glow.color = new Color(1f, 0.86f, 0.45f, 0.30f);
                glow.GetComponent<LayoutElement>().ignoreLayout = true;
                UiKit.Place(glow.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26f, 0f), new Vector2(190f, 190f));
                chip.Glow = glow;

                var icon = kit.Picture(pill.transform, "Icon", kit.Icons.ForCurrency(currency), new Vector2(122f, 122f));
                icon.GetComponent<LayoutElement>().ignoreLayout = true;
                UiKit.Place(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26f, 4f), new Vector2(122f, 122f));
                chip.Icon = icon.rectTransform;

                // an occasional twinkle on the icon keeps the bar alive
                var glint = kit.Picture(icon.transform, "Glint", "sparkle", new Vector2(56f, 56f));
                glint.GetComponent<LayoutElement>().ignoreLayout = true;
                UiKit.Place(glint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(26f, 30f), new Vector2(56f, 56f));
                var twinkle = pill.gameObject.AddComponent<UiGlint>();
                twinkle.Target = glint.rectTransform;
                twinkle.Image = glint;

                chip.Text = kit.Text(pill.transform, "0", TextStyle.Number, 58, Palette.Cream, TextAlignmentOptions.MidlineRight, FontStyles.Bold, "Amount");
                UiKit.Stretch(chip.Text.rectTransform, 96f, 2f, 34f, 4f);
                chip.Text.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;

                chip.Target = wallet.GetBalance(currency);
                chip.Shown = chip.Target;
                chips.Add(chip);
                SetText(chip);
            }

            wallet.CurrencyChanged += OnChanged;
        }

        private void OnChanged(CurrencyChange change)
        {
            foreach (var chip in chips)
            {
                if (chip.Currency != change.Currency) continue;
                chip.Target = change.NewValue;
                if (change.NewValue < change.OldValue)
                {
                    chip.Shown = change.NewValue; // spending shows immediately
                    SetText(chip);
                    Pop(chip, 1.12f, 0.28f);
                }
            }
        }

        public RectTransform IconOf(CurrencyType currency)
        {
            foreach (var chip in chips) if (chip.Currency == currency) return chip.Icon;
            return null;
        }

        /// <summary>Called when a flying reward icon lands: the icon bounces and the chip glows.</summary>
        public void Pulse(CurrencyType currency)
        {
            foreach (var chip in chips)
            {
                if (chip.Currency != currency) continue;
                Pop(chip, 1.34f, 0.42f);
                chip.FlashUntil = Time.unscaledTime + 0.35f;
            }
        }

        private void Pop(Chip chip, float peak, float duration)
        {
            if (kit.Motion == null) return;
            var icon = chip.Icon;
            kit.Motion.Cancel(icon);
            kit.Motion.Play(duration, Ease.OutBack, t => icon.localScale = Vector3.one * Mathf.LerpUnclamped(peak, 1f, t), 0f, () => icon.localScale = Vector3.one, icon);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            foreach (var chip in chips)
            {
                if (!Mathf.Approximately(chip.Shown, chip.Target))
                {
                    float step = Mathf.Max(1f, Mathf.Abs(chip.Target - chip.Shown) * 5f) * dt;
                    chip.Shown = Mathf.MoveTowards(chip.Shown, chip.Target, step);
                    SetText(chip);
                }
                float glow = now < chip.FlashUntil ? 0.85f : 0.30f;
                var c = chip.Glow.color;
                c.a = Mathf.MoveTowards(c.a, glow, dt * 3f);
                chip.Glow.color = c;
            }
        }

        private static void SetText(Chip chip)
        {
            int shown = Mathf.RoundToInt(chip.Shown);
            if (shown == chip.LastShownInt) return;
            chip.LastShownInt = shown;
            chip.Text.text = shown.ToString("N0");
        }
    }
}
