using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Temporary visual-validation provider for the "Cozy Detailed Japan" direction. It presents three authored
    /// isometric renders as camera-facing world sprites while every gameplay concern remains on <see cref="BuildingView"/>.
    /// Removing Resources/Buildings/cozy_buildings_atlas.png automatically restores the procedural 3D fallback.
    /// Production assets should replace this provider with real 3D prefabs that implement <see cref="IBuildingModelProvider"/>.
    /// </summary>
    public sealed class CozyBuildingSpriteModels : IBuildingModelProvider
    {
        private const string AtlasResourcePath = "Buildings/cozy_buildings_atlas";

        private readonly struct Slice
        {
            public readonly float NormalizedXMin;
            public readonly float NormalizedXMax;
            public readonly float WorldWidth;
            public readonly float IndicatorHeight;
            public readonly float ColliderHeight;

            public Slice(float normalizedXMin, float normalizedXMax, float worldWidth, float indicatorHeight, float colliderHeight)
            {
                NormalizedXMin = normalizedXMin;
                NormalizedXMax = normalizedXMax;
                WorldWidth = worldWidth;
                IndicatorHeight = indicatorHeight;
                ColliderHeight = colliderHeight;
            }
        }

        private static readonly Dictionary<string, Slice> Slices = new Dictionary<string, Slice>(StringComparer.Ordinal)
        {
            // Boundaries sit in the transparent gutters of the authored 2172 px atlas. Normalized values keep
            // the crops correct when Unity applies a platform max-size import and resamples the texture.
            { "town_hall", new Slice(0f, 885f / 2172f, 5.0f, 4.25f, 3.1f) },
            { "woodcutter", new Slice(885f / 2172f, 1600f / 2172f, 4.0f, 3.2f, 2.15f) },
            { "house", new Slice(1600f / 2172f, 1f, 3.55f, 2.85f, 1.9f) }
        };

        private readonly IBuildingModelProvider fallback;
        private readonly Quaternion cameraRotation;
        private readonly Texture2D atlas;
        private readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public CozyBuildingSpriteModels(IBuildingModelProvider fallback, Quaternion cameraRotation)
        {
            this.fallback = fallback ?? throw new ArgumentNullException(nameof(fallback));
            this.cameraRotation = cameraRotation;
            atlas = Resources.Load<Texture2D>(AtlasResourcePath);
            if (atlas == null)
                Debug.LogWarning($"[ART] Cozy building atlas not found at Resources/{AtlasResourcePath}; using procedural 3D models.");
            else
                Debug.Log($"[ART] Cozy Detailed Japan test atlas active ({atlas.width}x{atlas.height}).");
        }

        public bool Has(string visualId) => fallback.Has(visualId);

        public GameObject Create(string visualId, int visualLevel, int footprintWidth, int footprintHeight)
        {
            if (atlas == null || !Slices.TryGetValue(visualId, out var slice))
                return fallback.Create(visualId, visualLevel, footprintWidth, footprintHeight);

            var root = new GameObject($"{visualId}_CozySpriteTest");
            root.transform.localRotation = cameraRotation;

            // the flat test sprite has one look; upgrades show as a larger building (plus the lanterns/banners the view adds)
            float levelScale = 1f + 0.12f * Mathf.Max(0, visualLevel - 1);
            root.transform.localScale = Vector3.one * levelScale;
            var model = root.AddComponent<BuildingModel>();
            model.IndicatorHeight = slice.IndicatorHeight * levelScale;
            model.ColliderHeight = slice.ColliderHeight * levelScale;
            model.IgnorePlacementRotation = true;

            var renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = GetSprite(visualId, slice);
            renderer.color = Color.white;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 2;
            return root;
        }

        private Sprite GetSprite(string visualId, Slice slice)
        {
            if (sprites.TryGetValue(visualId, out var existing)) return existing;

            int xMin = Mathf.RoundToInt(slice.NormalizedXMin * atlas.width);
            int xMax = Mathf.RoundToInt(slice.NormalizedXMax * atlas.width);
            int cellWidth = Mathf.Max(1, xMax - xMin);
            float pixelsPerUnit = cellWidth / slice.WorldWidth;
            var rect = new Rect(xMin, 0f, cellWidth, atlas.height);
            var sprite = Sprite.Create(atlas, rect, new Vector2(0.5f, 0f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = $"Cozy_{visualId}";
            sprites.Add(visualId, sprite);
            return sprite;
        }
    }
}
