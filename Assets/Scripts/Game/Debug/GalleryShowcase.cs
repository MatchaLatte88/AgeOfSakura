using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Tooling for art review: builds actors, trees and shrubs on an empty lawn off the map and photographs them from the fixed isometric
    /// direction with a second orthographic camera (no UI, any zoom, so small details can be judged). Used by the UI showcase state "gallery".
    /// </summary>
    public sealed class GalleryShowcase
    {
        private readonly UiShowcase.Context ctx;
        private readonly string outputDirectory;
        private readonly List<string> log;
        private readonly Vector3 stageOrigin = new Vector3(400f, 0f, 400f);
        private Transform stage;

        public GalleryShowcase(UiShowcase.Context context, string outputDirectory, List<string> log)
        {
            ctx = context;
            this.outputDirectory = outputDirectory;
            this.log = log;
        }

        // The fixed camera looks along (1, -, 1): screen-right is (1, 0, -1), "away from the camera" is (1, 0, 1).
        private static readonly Vector3 Right = new Vector3(1f, 0f, -1f).normalized;
        private static readonly Vector3 Away = new Vector3(1f, 0f, 1f).normalized;
        private const float FaceCameraYaw = 225f;

        private Vector3 At(float right, float away) => stageOrigin + Right * right + Away * away;

        public IEnumerator Run()
        {
            BuildStage();
            yield return Actors();
            yield return Vegetation();
        }

        /// <summary>
        /// Photographs one building model at its looks, whole and in close-ups of the two faces the camera sees. A building that stands in
        /// the river (the dock) gets a patch of water on the far half of the lawn.
        /// </summary>
        public IEnumerator RunBuilding(string visualId, int levels = 3, float ortho = 1.15f, bool pierInWater = false)
        {
            BuildStage();
            if (pierInWater)
            {
                var water = new GameObject("Water");
                water.transform.SetParent(stage, false);
                water.transform.position = stageOrigin + new Vector3(-9f, 0.045f, 0f);
                water.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GroundQuad(18f, 9f);
                water.AddComponent<MeshRenderer>().sharedMaterial = ctx.Art.NoOutline(Palette.WaterShallow);
            }
            for (int level = 1; level <= levels; level++)
            {
                var model = ctx.Models.Create(visualId, level, 2, 2);
                model.transform.SetParent(stage, false);
                model.transform.position = At(0f, 0f);
                yield return Photo($"{visualId}_l{level}", At(0f, 0f) + Vector3.up * 0.55f, ortho);
                if (level == 2 || levels == 1)
                {
                    yield return Photo($"{visualId}_l2_front", At(0f, 0f) + Right * 0.1f + Away * -0.5f + Vector3.up * 0.4f, 0.62f);
                    yield return Photo($"{visualId}_l2_left", At(0f, 0f) + Right * -0.6f + Away * 0.1f + Vector3.up * 0.4f, 0.62f);
                    yield return Photo($"{visualId}_l2_roof", At(0f, 0f) + Vector3.up * 0.95f, 0.62f);
                }
                UnityEngine.Object.Destroy(model);
            }
        }

        /// <summary>Road paving in every shape that matters: single cell, straight, corner, T and crossing, next to each other.</summary>
        public IEnumerator RunRoads()
        {
            BuildStage();
            var masks = new[]
            {
                0, RoadSurface.East, RoadSurface.East | RoadSurface.West,
                RoadSurface.East | RoadSurface.North, RoadSurface.East | RoadSurface.West | RoadSurface.North,
                RoadSurface.East | RoadSurface.West | RoadSurface.North | RoadSurface.South
            };
            for (int i = 0; i < masks.Length; i++)
            {
                var model = ctx.Models.Create("road", 1, 1, 1);
                model.transform.SetParent(stage, false);
                model.transform.position = At((i - 2.5f) * 1.6f, 0f);
                model.GetComponentInChildren<RoadSurface>().SetMask(masks[i]);
            }
            // a longer run laid cell by cell, bending twice
            var run = new[] { (0, 0, 9), (1, 0, 13), (2, 0, 5), (2, 1, 10), (2, 2, 3), (3, 2, 5), (4, 2, 5) };
            foreach (var (x, z, mask) in run)
            {
                var model = ctx.Models.Create("road", 1, 1, 1);
                model.transform.SetParent(stage, false);
                model.transform.position = At(-1.5f + x, 2.5f + z);
                model.GetComponentInChildren<RoadSurface>().SetMask(mask);
            }
            yield return Photo("roads_a", At(0f, 1.2f), 2.6f);
            yield return Photo("roads_close", At(-0.8f, 0f), 1.1f);
        }

        /// <summary>The whole world and its four quarters (valley, east bank, southern meadow, lake) from the game's own angle.</summary>
        public IEnumerator RunWorld()
        {
            var grid = ctx.Session.Grid;
            float w = grid.Width * grid.CellSize, h = grid.Height * grid.CellSize;
            yield return Photo("world_all", new Vector3(w * 0.5f, 0f, h * 0.5f), Mathf.Max(w, h) * 0.52f);
            yield return Photo("world_valley", new Vector3(11f, 0f, 10f), 9f);
            yield return Photo("world_east", new Vector3(27f, 0f, 12f), 9f);
            yield return Photo("world_south", new Vector3(10f, 0f, 26f), 9f);
            yield return Photo("world_lake", new Vector3(28f, 0f, 29f), 10f);
        }

        private void BuildStage()
        {
            stage = new GameObject("Gallery").transform;
            var lawn = new GameObject("Lawn");
            lawn.transform.SetParent(stage, false);
            lawn.transform.position = stageOrigin + new Vector3(-9f, 0f, -9f);
            lawn.AddComponent<MeshFilter>().sharedMesh = ProceduralMeshes.GroundQuad(18f, 18f);
            lawn.AddComponent<MeshRenderer>().sharedMaterial = ctx.Art.NoOutline(Palette.GrassLight);
        }

        // ------------------------------------------------------------------ actors

        private IEnumerator Actors()
        {
            var villagers = new List<ActorVisual>();
            for (int i = 0; i < 6; i++)
            {
                var v = ctx.Actors.Villager(i);
                v.Root.transform.SetParent(stage, false);
                v.Root.transform.position = At((i - 2.5f) * 0.5f, 0f);
                v.Root.transform.rotation = Quaternion.Euler(0f, FaceCameraYaw, 0f);
                villagers.Add(v);
            }
            var idle = 0f;
            foreach (var v in villagers) v.Animate(NpcState.Idle, idle, 0f);
            yield return Photo("actors_front", At(0f, 0f) + Vector3.up * 0.32f, 0.8f);

            // a walking pose from the side, then from behind
            for (int i = 0; i < villagers.Count; i++)
            {
                villagers[i].Root.transform.rotation = Quaternion.Euler(0f, 135f, 0f);
                villagers[i].Animate(NpcState.Walking, 1f, 0.35f + i * 0.2f);
            }
            yield return Photo("actors_walk_side", At(0f, 0f) + Vector3.up * 0.32f, 0.8f);
            for (int i = 0; i < villagers.Count; i++) villagers[i].Root.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            yield return Photo("actors_back", At(0f, 0f) + Vector3.up * 0.32f, 0.8f);

            // close-ups of each face and of the walking cycle
            for (int i = 0; i < villagers.Count; i++) villagers[i].Root.transform.rotation = Quaternion.Euler(0f, FaceCameraYaw, 0f);
            foreach (var v in villagers) v.Animate(NpcState.Idle, 0.3f, 0f);
            yield return Photo("actors_faces_a", At(-0.5f, 0f) + Vector3.up * 0.5f, 0.24f);
            yield return Photo("actors_faces_b", At(0.5f, 0f) + Vector3.up * 0.5f, 0.24f);
            foreach (var v in villagers) UnityEngine.Object.Destroy(v.Root);

            // animals
            var dog = ctx.Actors.Dog();
            var hen = ctx.Actors.Chicken(true);
            var rooster = ctx.Actors.Chicken(false);
            var animals = new[] { dog, hen, rooster };
            for (int i = 0; i < animals.Length; i++)
            {
                animals[i].Root.transform.SetParent(stage, false);
                animals[i].Root.transform.position = At((i - 1) * 0.55f, 0f);
                animals[i].Root.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                animals[i].Animate(NpcState.Idle, 0.5f, 0f);
            }
            yield return Photo("animals_idle", At(0f, 0f) + Vector3.up * 0.16f, 0.5f);
            // four phases a quarter stride apart (dog cycle 0.52, hens 0.45)
            for (int step = 0; step < 4; step++)
            {
                for (int i = 0; i < animals.Length; i++) animals[i].Animate(NpcState.Walking, 1f + step * 0.1f, 0.4f + step * 0.12f);
                yield return Photo(step == 0 ? "animals_walk" : "animals_walk_" + step, At(0f, 0f) + Vector3.up * 0.16f, 0.5f);
            }
            foreach (var a in animals) UnityEngine.Object.Destroy(a.Root);
        }

        // ------------------------------------------------------------------ vegetation

        private IEnumerator Vegetation()
        {
            var rng = new System.Random(3);
            var props = ctx.Props;
            props.AnimateProps = true;
            props.MaxRadius = float.PositiveInfinity;
            var made = new List<GameObject>();

            made.Add(props.Pine(stage, At(-2.2f, 0f), rng));
            made.Add(props.RoundTree(stage, At(0f, 0f), rng));
            made.Add(props.Birch(stage, At(2.2f, 0f), rng));
            foreach (var m in made) m.transform.localScale = Vector3.one;
            yield return Photo("trees_a", At(0f, 0f) + Vector3.up * 1.05f, 1.55f);
            foreach (var m in made) UnityEngine.Object.Destroy(m);
            made.Clear();

            made.Add(props.Cherry(stage, At(-2.2f, 0f), rng));
            made.Add(props.OldTree(stage, At(0f, 0f), rng));
            made.Add(props.Bamboo(stage, At(2.2f, 0f), rng));
            yield return Photo("trees_b", At(0f, 0f) + Vector3.up * 1.05f, 1.55f);
            foreach (var m in made) UnityEngine.Object.Destroy(m);
            made.Clear();

            made.Add(props.Shrub(stage, At(-0.9f, 0f), rng));
            made.Add(props.BlossomShrub(stage, At(0f, 0f), rng));
            made.Add(props.Shrub(stage, At(0.9f, 0f), rng));
            made.Add(props.Rock(stage, At(1.8f, 0f), rng, true));
            yield return Photo("bushes", At(0.4f, 0f) + Vector3.up * 0.25f, 0.85f);
            foreach (var m in made) UnityEngine.Object.Destroy(m);
            made.Clear();

            // the coarse version used for far scenery
            props.AnimateProps = false;
            made.Add(props.Pine(stage, At(-1.5f, 0f), rng));
            made.Add(props.RoundTree(stage, At(0f, 0f), rng));
            made.Add(props.Birch(stage, At(1.5f, 0f), rng));
            yield return Photo("trees_far", At(0f, 0f) + Vector3.up * 0.95f, 1.45f);
            foreach (var m in made) UnityEngine.Object.Destroy(m);
            props.AnimateProps = true;
        }

        // ------------------------------------------------------------------ photography

        private IEnumerator Photo(string name, Vector3 target, float orthoSize)
        {
            const int width = 1400, height = 800;
            var go = new GameObject("GalleryCamera");
            var cam = go.AddComponent<Camera>();
            cam.CopyFrom(ctx.Camera.Camera);
            cam.orthographic = true;
            cam.orthographicSize = orthoSize;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.86f, 0.78f);
            cam.transform.rotation = ctx.Camera.Camera.transform.rotation;
            cam.transform.position = target - cam.transform.forward * 60f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 200f;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            cam.depth = ctx.Camera.Camera.depth + 5f;

            yield return new WaitForSecondsRealtime(0.3f);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = previous;

            string path = Path.Combine(outputDirectory, $"gallery_{name}.png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            log.Add($"{path} {width}x{height}");
            UnityEngine.Object.Destroy(tex);
            cam.targetTexture = null;
            rt.Release();
            UnityEngine.Object.Destroy(rt);
            UnityEngine.Object.Destroy(go);
        }
    }
}
