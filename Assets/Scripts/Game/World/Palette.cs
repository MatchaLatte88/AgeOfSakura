using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// world colours follow the 3D-Toon style (see ToonStyle); the UI colours below follow the compact frosted-glass UI (documented in the README).
    /// Colours live here (not scattered through builders) so the look can be tuned in one place.
    /// </summary>
    public static class Palette
    {
        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }

        // ---- world (3D-Toon style 1.0.0: values from the style palette or derived from it, see ToonStyle) ----
        public static readonly Color GrassLight = Hex("#92d85a");
        public static readonly Color GrassDark = Hex("#5cb547");
        public static readonly Color GrassFar = Hex("#72c44d");
        public static readonly Color Dirt = Hex("#b98e58");
        public static readonly Color DirtDark = Hex("#9a7144");
        public static readonly Color Mud = Hex("#6e5232");
        public static readonly Color WaterDeep = Hex("#2f86c4");
        public static readonly Color WaterShallow = Hex("#5fb8e0");
        public static readonly Color Sky = Hex("#d4e6cc");

        public static readonly Color Plaster = Hex("#dfcda6");      // cob[0]
        public static readonly Color PlasterShade = Hex("#cdb98f"); // cob[1]
        public static readonly Color Timber = Hex("#7c4c26");       // wood_dark
        public static readonly Color TimberLight = Hex("#a86f38");  // wood
        public static readonly Color Plank = Hex("#8b5a2f");        // beam
        public static readonly Color Straw = Hex("#e8bc5a");
        public static readonly Color StrawDark = Hex("#c9953c");    // thatch_dark
        public static readonly Color RoofTile = Hex("#414a5e");
        public static readonly Color RoofTileLight = Hex("#5d6a82");
        public static readonly Color Stone = Hex("#aeb2bb");        // stone_plain
        public static readonly Color StoneDark = Hex("#868b97");
        public static readonly Color BannerRed = Hex("#c7392f");
        public static readonly Color Gold = Hex("#e0b84a");
        public static readonly Color Bark = Hex("#6a4526");
        public static readonly Color LogEnd = Hex("#e3c08c");

        public static readonly Color PineDark = Hex("#2f7a3a");
        public static readonly Color PineMid = Hex("#3f9a45");
        public static readonly Color LeafGreen = Hex("#5cb547");    // leaf_greens[2]
        public static readonly Color LeafLight = Hex("#86d24e");    // leaf_greens[1]
        public static readonly Color Bamboo = Hex("#9bd257");
        public static readonly Color BambooLeaf = Hex("#b6e274");
        public static readonly Color Blossom = Hex("#ffbfd6");      // blossom_pinks[2]
        public static readonly Color BlossomDeep = Hex("#f8a3c1");  // blossom_pinks[1]
        public static readonly Color RockA = Hex("#9a9da6");
        public static readonly Color RockB = Hex("#7f838d");
        public static readonly Color RockC = Hex("#b3b6bd");
        public static readonly Color Moss = Hex("#66b040");

        public static readonly Color Skin = Hex("#EBC9A0");
        public static readonly Color Hair = Hex("#2A2320");
        public static readonly Color KimonoIndigo = Hex("#38507C");
        public static readonly Color KimonoRust = Hex("#B0553A");
        public static readonly Color KimonoMoss = Hex("#5F8248");
        public static readonly Color Sash = Hex("#E2D4B2");

        // ---- UI (compact frosted glass; the same palette family as the VoxelHaven HUD) ----
        public static readonly Color Cream = Hex("#FFF8EA");        // text on dark glass and on colour
        public static readonly Color CreamDeep = Hex("#F3EAD6");    // chips, tiles
        public static readonly Color CreamLine = Hex("#E6DAC0");    // hairlines, empty tracks
        public static readonly Color Ink = Hex("#4A3426");
        public static readonly Color InkSoft = Hex("#7A624E");
        public static readonly Color UiGreen = Hex("#5E9E4A");
        public static readonly Color UiGreenDark = Hex("#3F7A33");
        public static readonly Color UiGold = Hex("#E2B23C");
        public static readonly Color UiRed = Hex("#D9604C");
        public static readonly Color UiRedSoft = Hex("#F6D6CE");

        // ---- placement ----
        public static readonly Color ValidFill = new Color(0.36f, 0.78f, 0.36f, 0.55f);
        public static readonly Color InvalidFill = new Color(0.85f, 0.25f, 0.22f, 0.55f);
    }
}
