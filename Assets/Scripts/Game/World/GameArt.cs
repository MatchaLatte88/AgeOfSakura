using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Shared materials, meshes and icons for the procedural world art (3D-Toon style, see <see cref="ToonStyle"/>).
    /// Everything is cached so the whole game uses a few hundred materials of one toon shader (SRP-batcher friendly).
    /// The toon shader lives in Resources/Shaders, so builds always contain it; overlays, water and particles still use URP Unlit/Particles,
    /// which <c>ProjectSetup</c> keeps through template materials (Assets/Resources/ShaderVariants).
    /// </summary>
    public sealed class GameArt
    {
        public const string ToonShaderResource = "Shaders/AgeOfSakuraToon";

        public static readonly string[] RequiredShaderNames =
        {
            "Universal Render Pipeline/Lit",
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Unlit"
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int TexModeId = Shader.PropertyToID("_TexMode");
        private static readonly int TexScaleId = Shader.PropertyToID("_TexScale");
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private static readonly int UnlitId = Shader.PropertyToID("_Unlit");
        private static readonly int CutoffId = Shader.PropertyToID("_Cutoff");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");
        private const string OutlinePass = "SRPDefaultUnlit";

        private readonly Shader toon;
        private readonly Shader unlit;
        private readonly Shader particles;
        private readonly Dictionary<(int color, int texture, int flags, int scale, int emission), Material> toonCache =
            new Dictionary<(int, int, int, int, int), Material>();
        private readonly Dictionary<int, Material> unlitCache = new Dictionary<int, Material>();
        private readonly Dictionary<int, Material> ghostCache = new Dictionary<int, Material>();

        public Mesh Cube { get; }
        /// <summary>Smooth icosphere, diameter 1 (320 triangles).</summary>
        public Mesh Sphere { get; }
        /// <summary>Smooth icosphere, diameter 1 (80 triangles), for small details.</summary>
        public Mesh SphereLow { get; }
        public Mesh Cone6 { get; }
        public Mesh Cone8 { get; }
        public IconSet Icons { get; }

        public GameArt()
        {
            toon = Resources.Load<Shader>(ToonShaderResource);
            if (toon == null) throw new InvalidOperationException($"Toon shader Resources/{ToonShaderResource} is missing.");
            if (!toon.isSupported) throw new InvalidOperationException("The toon shader does not compile on this platform (see the shader errors in the console).");
            unlit = FindShader("Universal Render Pipeline/Unlit", "Unlit/Color");
            particles = FindShader("Universal Render Pipeline/Particles/Unlit", "Particles/Standard Unlit");

            Cube = PrimitiveMesh(PrimitiveType.Cube);
            Sphere = ToonMeshes.Sphere(ToonStyle.FoliageSubdivisionsNear);
            SphereLow = ToonMeshes.Sphere(ToonStyle.FoliageSubdivisionsFar);
            Cone6 = ToonMeshes.Cone(6);
            Cone8 = ToonMeshes.Cone(8);
            Icons = IconFactory.Create();
        }

        private static Shader FindShader(string preferred, string fallback)
        {
            var s = Shader.Find(preferred);
            if (s != null) return s;
            GameLogUnity.Warn($"Shader '{preferred}' not found (is a URP asset assigned?). Falling back to '{fallback}'.");
            s = Shader.Find(fallback);
            if (s == null) throw new InvalidOperationException($"Neither shader '{preferred}' nor '{fallback}' is available.");
            return s;
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) UnityEngine.Object.Destroy(go);
            else UnityEngine.Object.DestroyImmediate(go);
            return mesh;
        }

        // ------------------------------------------------------------------ toon materials

        /// <summary>Toon material with a flat colour and the dark outline. <paramref name="smoothness"/> is ignored (toon has no speculars).</summary>
        public Material Lit(Color color, float smoothness = 0.06f) => Toon(color, null, false, true, default);

        /// <summary>Toon material with a painted texture projected along the dominant axis (tile size from <see cref="PaintedTextures"/>).</summary>
        public Material Lit(Color color, PaintedTexture texture) => Toon(color, texture, false, true, default);

        /// <summary>Toon material without the outline (ground, tiny details, distant scenery).</summary>
        public Material NoOutline(Color color) => Toon(color, null, false, false, default);

        public Material NoOutline(Color color, PaintedTexture texture) => Toon(color, texture, false, false, default);

        /// <summary>Lit toon surface that also glows (windows, lantern flames). The glow ignores the light setup.</summary>
        public Material Glow(Color color, Color glow) => Toon(color, null, false, true, glow);

        /// <summary>Foliage: colour comes from the baked vertex colours multiplied by <paramref name="tint"/>; no realtime lighting.</summary>
        public Material Foliage(Color tint, bool outline = true) => Toon(tint, null, true, outline, default);

        /// <summary>Camera-facing leaf cards: painted cutout texture, double sided, no outline, no shadow.</summary>
        public Material LeafCards(Color tint)
        {
            var key = (Pack(tint), (int)PaintedTexture.LeafCard + 100, 0x8, 0, 0);
            if (toonCache.TryGetValue(key, out var cached)) return cached;
            var m = NewToon("LeafCards", tint);
            m.SetFloat(UnlitId, 1f);
            m.SetFloat(TexModeId, 2f);
            m.SetTexture(BaseMapId, PaintedTextures.Get(PaintedTexture.LeafCard));
            m.SetFloat(CutoffId, ToonStyle.LeafCardAlphaTest);
            m.SetFloat(CullId, (float)CullMode.Off);
            m.SetShaderPassEnabled(OutlinePass, false);
            toonCache[key] = m;
            return m;
        }

        /// <summary>Ground texture (uv0) on a toon surface: lit, receives shadows, no outline.</summary>
        public Material Lit(Color color, Texture texture, float smoothness = 0.02f)
        {
            var m = NewToon("GroundToon", color);
            m.SetFloat(TexModeId, 2f);
            m.SetTexture(BaseMapId, texture);
            m.SetShaderPassEnabled(OutlinePass, false);
            return m;
        }

        private Material Toon(Color color, PaintedTexture? texture, bool foliage, bool outline, Color glow)
        {
            int flags = (foliage ? 1 : 0) | (outline ? 2 : 0);
            int textureKey = texture.HasValue ? (int)texture.Value + 1 : 0;
            var key = (Pack(color), textureKey, flags, 0, Pack(glow));
            if (toonCache.TryGetValue(key, out var cached)) return cached;

            var m = NewToon(foliage ? "Foliage" : "Toon", color);
            if (foliage) m.SetFloat(UnlitId, 1f);
            if (texture.HasValue)
            {
                m.SetFloat(TexModeId, 1f);
                m.SetFloat(TexScaleId, PaintedTextures.DefaultScale(texture.Value));
                m.SetTexture(BaseMapId, PaintedTextures.Get(texture.Value));
            }
            if (glow.a > 0f) m.SetColor(EmissionId, glow);
            if (!outline) m.SetShaderPassEnabled(OutlinePass, false);
            m.name += "_" + ColorUtility.ToHtmlStringRGB(color);
            toonCache[key] = m;
            return m;
        }

        private Material NewToon(string name, Color color)
        {
            var m = new Material(toon) { name = name };
            m.SetColor(BaseColorId, color);
            m.SetColor(OutlineColorId, ToonStyle.OutlineColor);
            m.SetFloat(OutlineThicknessId, ToonStyle.OutlineThickness);
            return m;
        }

        // ------------------------------------------------------------------ unlit / transparent / particles (overlays, water, VFX, UI in the world)

        public Material Unlit(Color color)
        {
            int key = Pack(color);
            if (unlitCache.TryGetValue(key, out var cached)) return cached;
            var m = new Material(unlit) { name = "Unlit_" + ColorUtility.ToHtmlStringRGB(color), enableInstancing = true };
            SetColor(m, color);
            unlitCache[key] = m;
            return m;
        }

        /// <summary>Alpha-blended, unlit, no shadows. Optional texture (tiled by UV) multiplies the colour.</summary>
        public Material Transparent(Color color, Texture texture = null)
        {
            var m = new Material(unlit) { name = "Transparent" };
            SetColor(m, color);
            if (texture != null) SetTexture(m, texture);
            MakeTransparent(m);
            return m;
        }

        public Material Particle(Texture texture, Color tint)
        {
            var m = new Material(particles) { name = "Particle" };
            SetColor(m, tint);
            SetTexture(m, texture);
            MakeTransparent(m);
            return m;
        }

        /// <summary>Translucent copy of a material, used for the placement ghost.</summary>
        public Material Ghost(Material original)
        {
            int key = original.GetInstanceID();
            if (ghostCache.TryGetValue(key, out var cached)) return cached;
            // Copy the source material so authored textures survive ghost rendering.
            var m = new Material(original) { name = original.name + "_Ghost" };
            var c = original.HasProperty("_BaseColor") ? original.GetColor("_BaseColor") : original.color;
            c.a = 0.62f;
            SetColor(m, c);
            SetFloat(m, "_Smoothness", 0f);
            MakeTransparent(m);
            ghostCache[key] = m;
            return m;
        }

        /// <summary>Configures a URP (or toon) shader material for alpha blending (mirrors what the inspector's Surface Type does).</summary>
        public static void MakeTransparent(Material m)
        {
            SetFloat(m, "_Surface", 1f);
            SetFloat(m, "_Blend", 0f);
            SetFloat(m, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(m, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(m, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(m, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(m, "_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        public static void SetTexture(Material m, Texture t)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
        }

        private static void SetFloat(Material m, string property, float value)
        {
            if (m.HasProperty(property)) m.SetFloat(property, value);
        }

        private static int Pack(Color c)
        {
            Color32 c32 = c;
            return c32.r | (c32.g << 8) | (c32.b << 16) | (c32.a << 24);
        }
    }

    /// <summary>Unity-side logging shim so world code does not depend on the Core logger's console sink.</summary>
    public static class GameLogUnity
    {
        public static void Warn(string message) => Debug.LogWarning("[GAME] " + message);
        public static void Error(string message) => Debug.LogError("[GAME] " + message);
    }
}
