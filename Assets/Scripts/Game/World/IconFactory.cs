using System;
using System.Collections.Generic;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// All UI sprites, loaded by name from Resources/UI. The PNGs are baked by the editor tool "Age of Sakura > Generate UI Art"
    /// (see UiArtRecipes) - but they are ordinary assets, so an artist can replace any file (same name) without touching code.
    /// </summary>
    public sealed class IconSet
    {
        // Ids the game code relies on. A missing one fails loudly at startup instead of showing an empty image later.
        private static readonly string[] Required =
        {
            "coin", "wood", "diamond", "hammer", "check", "cross", "rotate", "gear", "house", "woodcutter", "town_hall", "rice", "rice_paddy", "garden", "shrine", "beauty", "noise", "faith", "upgrade", "lock", "clock",
            "bubble", "bubble_tail", "badge", "alert", "medallion", "glow", "sparkle", "rays", "shine", "vignette", "fade_top", "fade_bottom",
            "paper_noise", "panel_paper", "panel_lacquer", "pill_lacquer", "pill_paper", "card_paper", "card_locked", "trough", "bar_green",
            "bar_gold", "divider", "tail_paper",
            "btn_green", "btn_green_depth", "btn_gold", "btn_gold_depth", "btn_red", "btn_red_depth", "btn_paper", "btn_paper_depth",
            "btn_dark", "btn_dark_depth", "btn_disabled", "btn_disabled_depth"
        };

        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static IconSet Load()
        {
            var set = new IconSet();
            foreach (var sprite in Resources.LoadAll<Sprite>("UI")) set.sprites[sprite.name] = sprite;
            set.sprites["soft"] = set.sprites.TryGetValue("glow", out var glow) ? glow : null;

            var missing = new List<string>();
            foreach (var id in Required) if (!set.sprites.ContainsKey(id) || set.sprites[id] == null) missing.Add(id);
            if (missing.Count > 0)
                throw new InvalidOperationException("Missing UI sprites in Resources/UI: " + string.Join(", ", missing) + ". Run 'Age of Sakura > Generate UI Art'.");
            return set;
        }

        public Sprite Get(string id)
        {
            if (!sprites.TryGetValue(id, out var s) || s == null) throw new KeyNotFoundException($"No UI sprite '{id}'.");
            return s;
        }

        public bool Has(string id) => sprites.ContainsKey(id) && sprites[id] != null;

        public Sprite ForCurrency(CurrencyType currency)
        {
            switch (currency)
            {
                case CurrencyType.Coins: return Get("coin");
                case CurrencyType.Wood: return Get("wood");
                case CurrencyType.Diamonds: return Get("diamond");
                case CurrencyType.Rice: return Get("rice");
                default: throw new ArgumentOutOfRangeException(nameof(currency));
            }
        }
    }

    public static class IconFactory
    {
        public static IconSet Create() => IconSet.Load();
    }
}
