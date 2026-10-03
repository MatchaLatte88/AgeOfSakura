using System.Collections.Generic;
using System.Globalization;
using AgeOfSakura.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Top resource bar: one dark glass pill, one slot per currency (icon, then the amount; thin dividers in between). Coins, Wood
    /// and Diamonds are always there, the other goods appear the first time the player owns some. Amounts count up smoothly, show
    /// "1.2k" above 999 so seven goods fit in one line, and are updated by <see cref="Wallet.CurrencyChanged"/> events (no polling).
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        private sealed class Slot
        {
            public CurrencyType Currency;
            public RectTransform Root;
            public TMP_Text Text;
            public RectTransform Icon;
            public Image Flash;
            public long Target;
            public float Shown;
            public int LastShownInt = int.MinValue;
            public float FlashUntil;
            public bool Always;
            public bool Seen;
        }

        private const float SlotWidth = 172f;
        private const float IconSize = 52f;
        private const float FlashAlpha = 0.16f;

        // display order; the three that need no discovery come with the first game
        private static readonly CurrencyType[] Order =
        {
            CurrencyType.Coins, CurrencyType.Wood, CurrencyType.Iron, CurrencyType.Tools, CurrencyType.Rice, CurrencyType.Fish, CurrencyType.Diamonds
        };

        private readonly List<Slot> slots = new List<Slot>(7);
        private UiKit kit;
        private RectTransform root;

        public static HudView Create(UiKit kit, RectTransform parent, Wallet wallet)
        {
            var pill = kit.Glass(parent, "Hud", "pill_dark", UiTheme.BarHeight * 0.5f); // blocks input: a tap on the bar must not reach the world
            var hud = pill.gameObject.AddComponent<HudView>();
            hud.Build(kit, pill.rectTransform, wallet);
            return hud;
        }

        private void Build(UiKit kitRef, RectTransform rect, Wallet wallet)
        {
            kit = kitRef;
            root = rect;
            UiKit.Place(root, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(UiTheme.Gutter, -24f), new Vector2(0f, UiTheme.BarHeight));
            UiKit.AddShadow(gameObject, -6f, 0.3f);
            var layout = UiKit.AddGroup(gameObject, false, 0f, TextAnchor.MiddleLeft, 8, 0, 8, 0);
            layout.childForceExpandHeight = true;
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            for (int i = 0; i < Order.Length; i++)
            {
                var currency = Order[i];
                var slot = new Slot { Currency = currency, Always = currency == CurrencyType.Coins || currency == CurrencyType.Wood || currency == CurrencyType.Diamonds };
                slot.Target = wallet.GetBalance(currency);
                slot.Shown = slot.Target;
                slot.Seen = slot.Target > 0;

                var go = kit.Empty(root, currency + "Slot");
                slot.Root = UiKit.Rect(go);
                UiKit.AddGroup(go, false, 8f, TextAnchor.MiddleLeft, 16, 0, 10, 0);
                UiKit.Size(go, SlotWidth, UiTheme.BarHeight);

                var flash = kit.Sliced(go.transform, "Flash", "pill_flat", false);
                UiKit.Round(flash, UiTheme.BarHeight * 0.5f - 7f);
                flash.color = new Color(1f, 1f, 1f, 0f);
                flash.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                UiKit.Stretch(flash.rectTransform, 2f, 7f, 2f, 7f);
                slot.Flash = flash;

                if (i > 0)
                {
                    var divider = kit.Empty(go.transform, "Divider").AddComponent<Image>();
                    divider.color = new Color(1f, 1f, 1f, 0.16f);
                    divider.raycastTarget = false;
                    divider.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                    UiKit.Place(divider.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(2f, 38f));
                }

                var icon = kit.Picture(go.transform, "Icon", kit.Icons.ForCurrency(currency), new Vector2(IconSize, IconSize));
                slot.Icon = icon.rectTransform;

                slot.Text = kit.Text(go.transform, "0", UiTheme.FontNumber, Palette.Cream, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Amount");
                UiKit.Size(slot.Text.gameObject, height: UiTheme.BarHeight, flexWidth: 1f).minWidth = 0f;
                // seven goods share the bar: long amounts shrink instead of spilling over the rim
                slot.Text.fontSizeMin = 24f;

                slots.Add(slot);
                SetText(slot);
                go.SetActive(slot.Always || slot.Seen);
            }

            wallet.CurrencyChanged += OnChanged;
        }

        private void OnChanged(CurrencyChange change)
        {
            foreach (var slot in slots)
            {
                if (slot.Currency != change.Currency) continue;
                slot.Target = change.NewValue;
                if (change.NewValue > 0 && !slot.Seen)
                {
                    slot.Seen = true;
                    slot.Shown = change.OldValue; // the first goods count up from where they were
                    if (!slot.Root.gameObject.activeSelf)
                    {
                        slot.Root.gameObject.SetActive(true);
                        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
                    }
                }
                if (change.NewValue < change.OldValue)
                {
                    slot.Shown = change.NewValue; // spending shows immediately
                    SetText(slot);
                    Pop(slot, 1.12f, 0.28f);
                }
            }
        }

        /// <summary>The icon of a currency's slot, or null while that slot is not shown yet (nothing to fly to or from).</summary>
        public RectTransform IconOf(CurrencyType currency)
        {
            foreach (var slot in slots) if (slot.Currency == currency) return slot.Root.gameObject.activeInHierarchy ? slot.Icon : null;
            return null;
        }

        /// <summary>Called when a flying reward icon lands: the icon bounces and the slot lights up briefly.</summary>
        public void Pulse(CurrencyType currency)
        {
            foreach (var slot in slots)
            {
                if (slot.Currency != currency) continue;
                Pop(slot, 1.3f, 0.4f);
                slot.FlashUntil = Time.unscaledTime + 0.3f;
            }
        }

        private void Pop(Slot slot, float peak, float duration)
        {
            if (kit.Motion == null) return;
            var icon = slot.Icon;
            kit.Motion.Cancel(icon);
            kit.Motion.Play(duration, Ease.OutBack, t => icon.localScale = Vector3.one * Mathf.LerpUnclamped(peak, 1f, t), 0f, () => icon.localScale = Vector3.one, icon);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float now = Time.unscaledTime;
            foreach (var slot in slots)
            {
                if (!Mathf.Approximately(slot.Shown, slot.Target))
                {
                    float step = Mathf.Max(1f, Mathf.Abs(slot.Target - slot.Shown) * 5f) * dt;
                    slot.Shown = Mathf.MoveTowards(slot.Shown, slot.Target, step);
                    SetText(slot);
                }
                var c = slot.Flash.color;
                c.a = Mathf.MoveTowards(c.a, now < slot.FlashUntil ? FlashAlpha : 0f, dt * 1.2f);
                slot.Flash.color = c;
            }
        }

        private static void SetText(Slot slot)
        {
            int shown = Mathf.RoundToInt(slot.Shown);
            if (shown == slot.LastShownInt) return;
            slot.LastShownInt = shown;
            slot.Text.text = FormatAmount(shown);
        }

        /// <summary>Exact below 1000, then "1.2k" / "12k" / "1.2M" (rounded down, so "1.5k" always means at least 1500).</summary>
        public static string FormatAmount(long value)
        {
            var inv = CultureInfo.InvariantCulture;
            if (value < 1000) return value.ToString(inv);
            if (value < 10000) return (value / 100 / 10f).ToString("0.0", inv) + "k";
            if (value < 1000000) return (value / 1000).ToString(inv) + "k";
            return (value / 100000 / 10f).ToString("0.0", inv) + "M";
        }
    }
}
