using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Grafikstil "3D-Toon" 1.0.0 (Docs/grafikstil-3d-toon.md). Mirrors the YAML block of the style document: that block is the
    /// single source of truth, so change values here only when it changes there (and only with Fred's approval).
    /// Values marked ASSUMPTION are not in the YAML and were chosen here; they are listed in README.md.
    /// </summary>
    public static class ToonStyle
    {
        public const string Version = "1.0.0";

        // ---- render ----
        public const int GradientSteps = 3;
        /// <summary>ASSUMPTION: brightness of the three toon bands (dark, mid, lit). The YAML only fixes the step count.</summary>
        public static readonly float[] GradientValues = { 0.42f, 0.72f, 1.0f };
        /// <summary>Outline width in NDC units, exactly like three.js OutlineEffect (0.0058 is about 2 px on a 720 px tall screen).</summary>
        public const float OutlineThickness = 0.0058f;
        /// <summary>Dark brown, never pure black.</summary>
        public static readonly Color OutlineColor = new Color(0.16f, 0.09f, 0.04f, 1f);

        // ---- camera (fixed, no rotation) ----
        public const float CameraElevationDegrees = 30f;
        public const float CameraAzimuthDegrees = 45f;

        // ---- light ----
        public static readonly Color DaySky = Palette.Hex("#ffffff");
        public static readonly Color DayGround = Palette.Hex("#8aa86a");
        public const float DayHemisphereIntensity = 1.7f;
        public static readonly Color DaySun = Palette.Hex("#fff0d0");
        public const float DaySunIntensity = 2.2f;

        public static readonly Color NightSky = Palette.Hex("#3a4c9a");
        public static readonly Color NightGround = Palette.Hex("#1c2440");
        public const float NightHemisphereIntensity = 1.25f;
        public static readonly Color NightSun = Palette.Hex("#7f95ea");
        public const float NightSunIntensity = 1.0f;

        /// <summary>Direction towards the sun in the style document's (three.js, right-handed) space.</summary>
        public static readonly Vector3 SunDirForBakedFoliageStyleSpace = new Vector3(-0.35f, 0.72f, 0.6f);
        /// <summary>
        /// The same direction in Unity's world. The style camera sits at +x,+z looking at the origin; Unity's camera (yaw 45) sits at
        /// -x,-z, so the style's world is rotated 180 degrees around Y: (x, y, z) -> (-x, y, -z).
        /// </summary>
        public static Vector3 SunDirTowardsSun => new Vector3(0.35f, 0.72f, -0.6f).normalized;

        public static readonly Color WindowGlowDay = Palette.Hex("#ffe6a4");
        public static readonly Color WindowGlowNight = Palette.Hex("#ffd35e");

        // ---- palette (world) ----
        public static readonly Color[] LeafGreens = { Palette.Hex("#c4f083"), Palette.Hex("#86d24e"), Palette.Hex("#5cb547") };
        public static readonly Color[] BlossomPinks = { Palette.Hex("#ffd0e0"), Palette.Hex("#f8a3c1"), Palette.Hex("#ffbfd6") };
        public static readonly Color Moss = Palette.Hex("#66b040");
        public static readonly Color Wood = Palette.Hex("#a86f38");
        public static readonly Color WoodDark = Palette.Hex("#7c4c26");
        public static readonly Color Beam = Palette.Hex("#8b5a2f");
        public static readonly Color ThatchDark = Palette.Hex("#c9953c");
        public static readonly Color StonePlain = Palette.Hex("#aeb2bb");
        public static readonly Color[] Cob = { Palette.Hex("#dfcda6"), Palette.Hex("#cdb98f"), Palette.Hex("#e8d9b2") };

        // ---- vegetation ----
        /// <summary>The YAML's foliage ramp stops (vertex colours are baked along them).</summary>
        public static readonly float[] FoliageRamp = { 0f, 0.38f, 0.72f, 1f };
        /// <summary>
        /// DEVIATION (needs Fred's approval): the YAML says icosahedron detail 9 (2000 triangles per clump), which is a desktop demo setting.
        /// Hundreds of trees on a phone need far fewer; these are icosahedron subdivision levels of the crown clumps (20 * 4^n triangles:
        /// 2 = 320, 1 = 80). Normals are smooth either way, so the silhouette stays round.
        /// </summary>
        public const int FoliageSubdivisionsNear = 2;
        public const int FoliageSubdivisionsFar = 1;
        public const float LeafCardAlphaTest = 0.45f;

        // ---- animation ----
        public const float BuildInSeconds = 4.4f;
        public static readonly string[] BuildOrder = { "Ground", "Body", "Roof", "Trees", "Props" };
        public static readonly Vector2[] BuildWindows =
        {
            new Vector2(0f, 0.22f), new Vector2(0.2f, 0.5f), new Vector2(0.42f, 0.72f), new Vector2(0.5f, 0.8f), new Vector2(0.66f, 1f)
        };

        // ---- shared shader globals ----
        private static readonly int HemiSky = Shader.PropertyToID("_AoS_HemiSky");
        private static readonly int HemiGround = Shader.PropertyToID("_AoS_HemiGround");
        private static readonly int FoliageTint = Shader.PropertyToID("_AoS_FoliageTint");
        private static readonly int GradientId = Shader.PropertyToID("_AoS_Gradient");
        private static readonly int ParamsId = Shader.PropertyToID("_AoS_Params");

        /// <summary>Colour as a shader vector in the space the toon shader computes lighting in (linear).</summary>
        public static Vector4 ToLinearVector(Color c, float scale = 1f)
        {
            var l = c.linear;
            return new Vector4(l.r * scale, l.g * scale, l.b * scale, 1f);
        }

        /// <summary>
        /// Pushes the light setup to the toon shader. three.js divides light intensity by pi inside the Lambert BRDF and Unity does not,
        /// so every intensity is divided by pi here.
        /// </summary>
        public static void ApplyLighting(Light sun, bool night)
        {
            var sky = night ? NightSky : DaySky;
            var ground = night ? NightGround : DayGround;
            float hemi = (night ? NightHemisphereIntensity : DayHemisphereIntensity) / Mathf.PI;
            Shader.SetGlobalVector(HemiSky, ToLinearVector(sky, hemi));
            Shader.SetGlobalVector(HemiGround, ToLinearVector(ground, hemi));
            Shader.SetGlobalVector(GradientId, new Vector4(GradientValues[0], GradientValues[1], GradientValues[2], 0f));
            // foliage is lit by baked vertex colours; night only tints them
            Shader.SetGlobalVector(ParamsId, new Vector4(night ? 1.9f : 1f, 0f, 0f, 0f));
            Shader.SetGlobalVector(FoliageTint, night ? new Vector4(0.42f, 0.5f, 0.85f, 1f) : Vector4.one);

            if (sun == null) return;
            sun.color = night ? NightSun : DaySun;
            sun.intensity = (night ? NightSunIntensity : DaySunIntensity) / Mathf.PI;
            sun.transform.rotation = Quaternion.LookRotation(-SunDirTowardsSun);
        }

        public static Color WindowGlow(bool night) => night ? WindowGlowNight : WindowGlowDay;

        /// <summary>Colour as stored in vertex colours (the shader treats them as linear).</summary>
        public static Color32 ToVertexColor(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
    }
}
