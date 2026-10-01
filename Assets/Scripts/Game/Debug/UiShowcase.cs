using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Development tool: walks through every UI state at several resolutions and writes PNG screenshots, so UI design
    /// can be reviewed without touching a device. Started with command line flags, e.g.
    /// <c>AgeOfSakuraPreview.exe -uishowcase C:\out -uilang en -uires 1560x720,1280x720,960x720</c>.
    /// It uses an in-memory save, so it never touches (or depends on) a real save file.
    /// </summary>
    public sealed class UiShowcase : MonoBehaviour
    {
        public sealed class Options
        {
            public string OutputDirectory;
            public Language Language = Language.English;
            public List<Vector2Int> Resolutions = new List<Vector2Int> { new Vector2Int(1560, 720) };
            /// <summary>"all" or "key" (a shorter list for extra resolutions/languages).</summary>
            public string States = "all";
        }

        public sealed class Context
        {
            public GameSession Session;
            public GameUi Ui;
            public SelectionController Selection;
            public PlacementController Placement;
            public IsoCameraController Camera;
            public BuildingViewManager Views;
            public GameArt Art;
            public Prim Prim;
            public PropFactory Props;
            public ActorFactory Actors;
        }

        public static bool TryParse(out Options options)
        {
            options = null;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "-uishowcase", StringComparison.OrdinalIgnoreCase) || i + 1 >= args.Length) continue;
                options = new Options { OutputDirectory = args[i + 1] };
                for (int j = 0; j + 1 < args.Length; j++)
                {
                    if (args[j] == "-uilang") options.Language = args[j + 1].StartsWith("de", StringComparison.OrdinalIgnoreCase) ? Language.German : Language.English;
                    if (args[j] == "-uistates") options.States = args[j + 1];
                    if (args[j] == "-uires")
                    {
                        options.Resolutions.Clear();
                        foreach (var token in args[j + 1].Split(','))
                        {
                            var parts = token.Split('x');
                            if (parts.Length == 2 && int.TryParse(parts[0], out int w) && int.TryParse(parts[1], out int h)) options.Resolutions.Add(new Vector2Int(w, h));
                        }
                    }
                }
                return true;
            }
            return false;
        }

        private Options options;
        private Context ctx;
        private string tag;
        private string woodcutterId;
        private string houseId;
        private string hallId;
        private readonly List<string> log = new List<string>();

        public static void Begin(GameObject host, Options options, Context context)
        {
            var showcase = host.AddComponent<UiShowcase>();
            // a thrown exception in tooling must end the run (and keep the log) instead of hanging the player
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (type != LogType.Exception) return;
                Directory.CreateDirectory(options.OutputDirectory);
                File.WriteAllText(Path.Combine(options.OutputDirectory, "showcase-error.log"), message + Environment.NewLine + stack);
                Application.Quit(1);
            };
            showcase.options = options;
            showcase.ctx = context;
            showcase.StartCoroutine(showcase.Run());
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(options.OutputDirectory);
            yield return new WaitForSecondsRealtime(1.0f);
            // screenshots must not catch buildings half-way through their construction animation (the "buildin" walkthrough does on purpose)
            BuildInAnimation.SpeedMultiplier = options.States == "style" || options.States == "hall" ? 1000f : 1f;
            if (options.States == "game") PrepareGameplayWorld();
            else if (options.States == "style") PrepareStyleWorld();
            else if (options.States == "hall") PrepareHallWorld();
            else if (options.States == "gallery") { }
            else PrepareWorld();

            foreach (var res in options.Resolutions)
            {
                Screen.SetResolution(res.x, res.y, FullScreenMode.Windowed);
                yield return new WaitForSecondsRealtime(0.8f);
                tag = $"{(options.Language == Language.German ? "de" : "en")}_{res.x}x{res.y}";
                if (options.States == "game") yield return RunGameplay();
                else if (options.States == "style") yield return RunStyle();
                else if (options.States == "hall") yield return RunHall();
                else if (options.States == "gallery") yield return new GalleryShowcase(ctx, options.OutputDirectory, log).Run();
                else yield return RunStates();
            }

            File.WriteAllLines(Path.Combine(options.OutputDirectory, "showcase.log"), log);
            Application.Quit();
        }

        private void PrepareWorld()
        {
            var s = ctx.Session;
            woodcutterId = s.Buildings.PlaceFree("woodcutter", new GridPos(5, 6), 0).InstanceId;
            s.Buildings.PlaceFree("woodcutter", new GridPos(12, 6), 0);
            houseId = s.Buildings.PlaceFree("house", new GridPos(7, 8), 0).InstanceId;
            foreach (var b in s.Buildings.All) if (b.DefinitionId == s.Definitions.Map.TownHallId) hallId = b.InstanceId;
            s.Wallet.Restore(new Dictionary<CurrencyType, long> { { CurrencyType.Coins, 1250 }, { CurrencyType.Wood, 34 }, { CurrencyType.Diamonds, 18 } });
        }

        private bool Key => options.States == "key";

        // ------------------------------------------------------------------ style review (3D-Toon world close-ups)

        private readonly List<string> styleBuildings = new List<string>();

        private void PrepareStyleWorld()
        {
            var s = ctx.Session;
            // one row of each building at level 1, 2, 3 (levels are set directly: this is tooling, not gameplay)
            var plan = new (string def, int x, int z, int level)[]
            {
                ("woodcutter", 4, 4, 1), ("woodcutter", 6, 4, 2), ("woodcutter", 8, 4, 3),
                ("house", 10, 4, 1), ("house", 12, 4, 2), ("house", 12, 6, 3),
                ("rice_paddy", 4, 7, 1), ("garden", 4, 9, 1), ("shrine", 4, 11, 1), ("house", 6, 14, 1)
            };
            foreach (var (def, x, z, level) in plan)
            {
                var instance = s.Buildings.PlaceFree(def, new GridPos(x, z), 0);
                if (level > 1)
                {
                    instance.Level = level;
                    ctx.Views.Rebuild(instance);
                }
                styleBuildings.Add(instance.InstanceId);
            }
            foreach (var b in s.Buildings.All) if (b.DefinitionId == s.Definitions.Map.TownHallId) hallId = b.InstanceId;
        }

        /// <summary>Frames a world point at the given orthographic size (zoom first: the pan bounds depend on the zoom level).</summary>
        private IEnumerator CloseUp(Vector3 world, float orthoSize)
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            ctx.Camera.Zoom(0.0001f, center);                 // widest view (clamped to the configured maximum)
            yield return new WaitForSecondsRealtime(0.5f);
            ctx.Camera.Zoom(ctx.Camera.Camera.orthographicSize / orthoSize, center);
            yield return new WaitForSecondsRealtime(1.6f);     // the zoom pivot keeps pulling the focus until the size has settled
            ctx.Camera.FrameOn(world);
            yield return new WaitForSecondsRealtime(0.4f);
        }

        private void PrepareHallWorld()
        {
            var s = ctx.Session;
            foreach (var b in s.Buildings.All) if (b.DefinitionId == s.Definitions.Map.TownHallId) hallId = b.InstanceId;
        }

        /// <summary>Quick Town Hall review (all three looks, close and wide) without rendering the rest of the style showcase.</summary>
        private IEnumerator RunHall()
        {
            ctx.Ui.CloseAllPanels();
            var hall = ctx.Session.Buildings.Get(hallId);
            var hallCenter = new Vector3(hall.Origin.X + 2.6f, 0f, hall.Origin.Z + 2.6f); // the tower rises above its footprint: aim beyond it so the top stays in view
            for (int level = 1; level <= 3; level++)
            {
                hall.Level = level;
                ctx.Views.Rebuild(hall);
                yield return CloseUp(hallCenter, 3.4f);
                yield return Shot($"h{level}_close", 0.6f);
            }
            hall.Level = 2;
            ctx.Views.Rebuild(hall);
            yield return CloseUp(hallCenter, 3.8f);
            yield return Shot("h2_wide", 0.6f);
            yield return CloseUp(hallCenter + new Vector3(2.4f, 0f, 2.4f), 2.6f);
            yield return Shot("h2_top", 0.6f);
        }

        private IEnumerator RunStyle()
        {
            ctx.Ui.CloseAllPanels();
            yield return Shot("s01_overview");
            var hall = ctx.Session.Buildings.Get(hallId);
            var hallCenter = new Vector3(hall.Origin.X + 1.5f, 0f, hall.Origin.Z + 1.5f);
            for (int level = 1; level <= 3; level++)
            {
                hall.Level = level;
                ctx.Views.Rebuild(hall);
                yield return CloseUp(hallCenter, 3.8f);
                yield return Shot($"s02_townhall_l{level}", 0.6f);
            }

            yield return CloseUp(new Vector3(7f, 0f, 5.5f), 3.2f);
            yield return Shot("s03_woodcutters_l1_l2_l3", 0.6f);
            yield return CloseUp(new Vector3(12.5f, 0f, 6f), 3.2f);
            yield return Shot("s04_houses_l1_l2_l3", 0.6f);
            yield return CloseUp(new Vector3(5f, 0f, 8.5f), 3.2f);
            yield return Shot("s05_paddy_garden", 0.6f);
            yield return CloseUp(new Vector3(5f, 0f, 11.5f), 3.2f);
            yield return Shot("s05b_shrine_garden", 0.6f);
            yield return CloseUp(new Vector3(6f, 0f, 14.5f), 3.2f);
            yield return Shot("s05c_sakura_next_to_house", 0.6f);
            yield return CloseUp(new Vector3(2.5f, 0f, 3f), 3.8f);
            yield return Shot("s06_forest_edge", 0.6f);
            yield return CloseUp(new Vector3(17f, 0f, 9.5f), 3.8f);
            yield return Shot("s07_bridge", 0.6f);
            yield return CloseUp(new Vector3(10f, 0f, 9.5f), 6.2f);
            yield return Shot("s08_valley", 0.6f);

            // night variant of the same valley (style light.night) and scene statistics
            long triangles = 0;
            int renderers = 0;
            var countedBatches = new HashSet<Mesh>();
            foreach (var mf in FindObjectsByType<MeshFilter>(FindObjectsSortMode.None))
            {
                var mr = mf.GetComponent<MeshRenderer>();
                if (mr == null || !mr.enabled || mf.sharedMesh == null || !mf.gameObject.activeInHierarchy) continue;
                renderers++;
                // statically batched renderers share one combined mesh: count it once, not once per renderer
                if (mr.isPartOfStaticBatch && !countedBatches.Add(mf.sharedMesh)) continue;
                for (int sub = 0; sub < mf.sharedMesh.subMeshCount; sub++) triangles += mf.sharedMesh.GetIndexCount(sub) / 3; // works on non-readable meshes
            }
            log.Add($"scene: {renderers} mesh renderers, {triangles} triangles in the hierarchy (before culling)");
            yield return CloseUp(new Vector3(10f, 0f, 9.5f), 6.2f);
            var sun = FindFirstObjectByType<Light>();
            ToonStyle.ApplyLighting(sun, true);
            ctx.Camera.Camera.backgroundColor = new Color(0.05f, 0.07f, 0.15f);
            yield return Shot("s10_night", 0.6f);
            ToonStyle.ApplyLighting(sun, false);
            ctx.Camera.Camera.backgroundColor = Palette.Sky;

            // studio view: the same buildings on bare ground, to judge their details without the forest in front
            foreach (var decorName in new[] { "Decor_Animated", "Decor_Static" })
            {
                var decor = GameObject.Find(decorName);
                if (decor != null) decor.SetActive(false);
            }
            yield return CloseUp(new Vector3(11.5f, 0f, 5.5f), 3.2f);
            yield return Shot("t01_houses", 0.5f);
            yield return CloseUp(new Vector3(6.5f, 0f, 5.2f), 3.2f);
            yield return Shot("t02_woodcutters", 0.5f);
            yield return CloseUp(new Vector3(5f, 0f, 8f), 3.2f);
            yield return Shot("t03_paddy_garden", 0.5f);
            yield return CloseUp(new Vector3(5f, 0f, 11.5f), 3.2f);
            yield return Shot("t04_shrine", 0.5f);
            yield return CloseUp(hallCenter, 3.2f);
            yield return Shot("t05_townhall", 0.5f);
            foreach (var decorName in new[] { "Decor_Animated", "Decor_Static" })
                foreach (var t in Resources.FindObjectsOfTypeAll<Transform>())
                    if (t.name == decorName && t.gameObject.scene.IsValid()) t.gameObject.SetActive(true);

            // the construction animation: a new house dropping in
            var fresh = ctx.Session.Buildings.PlaceFree("house", new GridPos(9, 14), 0);
            yield return CloseUp(new Vector3(10f, 0f, 15f), 3.2f);
            BuildInAnimation.SpeedMultiplier = 1f;
            ctx.Views.Rebuild(fresh);
            float last = 0f;
            foreach (float t in new[] { 0.4f, 0.8f, 1.3f, 2.2f, 4.0f })
            {
                yield return Shot($"s09_buildin_{t:0.0}s", t - last);
                last = t;
            }
        }

        // ------------------------------------------------------------------ gameplay walkthrough (supply chain + house upgrades)

        private void PrepareGameplayWorld()
        {
            var s = ctx.Session;
            s.Buildings.PlaceFree("woodcutter", new GridPos(5, 5), 0);
            s.Buildings.PlaceFree("rice_paddy", new GridPos(12, 7), 0);
            houseId = s.Buildings.PlaceFree("house", new GridPos(7, 8), 0).InstanceId;
            foreach (var b in s.Buildings.All) if (b.DefinitionId == s.Definitions.Map.TownHallId) hallId = b.InstanceId;
            s.Wallet.Restore(new Dictionary<CurrencyType, long> { { CurrencyType.Coins, 2000 }, { CurrencyType.Wood, 400 }, { CurrencyType.Diamonds, 18 }, { CurrencyType.Rice, 40 } });
        }

        private void RunCycle(string id)
        {
            var s = ctx.Session;
            var instance = s.Buildings.Get(id);
            string production = s.Buildings.GetDefinition(instance).GetProductionIds(instance.Level)[0];
            var status = s.Production.TryStart(id, production);
            log.Add($"cycle {production}: {status}");
            s.Production.FinishAll();
            s.Production.TryCollect(id);
        }

        private IEnumerator RunGameplay()
        {
            var s = ctx.Session;
            ctx.Ui.CloseAllPanels();
            yield return Shot("g01_world_start");

            ctx.Selection.Select(houseId);
            yield return Shot("g02_house_idle");

            RunCycle(houseId);
            yield return Shot("g03_delivery_fx", 0.3f);
            yield return new WaitForSecondsRealtime(1.5f);
            RunCycle(houseId);
            ctx.Selection.Select(houseId);
            yield return Shot("g04_house_after_two_cycles");
            ctx.Ui.OpenUpgradePage();
            yield return Shot("g05_upgrade_needs_beauty");

            var garden = s.Buildings.PlaceFree("garden", new GridPos(9, 7), 0);
            log.Add($"garden placed {garden.InstanceId}");
            ctx.Ui.CloseAllPanels();
            ctx.Selection.Select(houseId);
            yield return Shot("g06_house_ready_tile");
            ctx.Ui.OpenUpgradePage();
            yield return Shot("g07_upgrade_ready_page");
            ctx.Ui.CloseAllPanels();
            yield return Shot("g08_upgrade_bubble");

            var status = s.Housing.TryUpgrade(houseId);
            log.Add($"upgrade to 2: {status}");
            yield return Shot("g09_level2_world", 0.5f);
            ctx.Selection.Select(houseId);
            yield return Shot("g10_level2_sheet");
            ctx.Ui.CloseAllPanels();

            for (int i = 0; i < 4; i++) RunCycle(houseId);
            s.Buildings.PlaceFree("shrine", new GridPos(5, 8), 0);
            s.Buildings.PlaceFree("garden", new GridPos(7, 10), 0);
            ctx.Selection.Select(houseId);
            ctx.Ui.OpenUpgradePage();
            yield return Shot("g11_level3_page_noise");

            s.Buildings.TryMove(s.Buildings.All[1].InstanceId, new GridPos(13, 12), 0);
            ctx.Ui.CloseAllPanels();
            ctx.Selection.Select(houseId);
            ctx.Ui.OpenUpgradePage();
            yield return Shot("g12_level3_page_ready");
            status = s.Housing.TryUpgrade(houseId);
            log.Add($"upgrade to 3: {status}");
            ctx.Ui.CloseAllPanels();
            yield return Shot("g13_level3_world", 0.6f);
            ctx.Selection.Select(houseId);
            yield return Shot("g14_level3_sheet");
            ctx.Ui.CloseAllPanels();

            ctx.Ui.OpenBuildMenu();
            yield return Shot("g15_build_menu");
            ctx.Ui.CloseAllPanels();
        }

        private IEnumerator RunStates()
        {
            var s = ctx.Session;

            SetBuilding(woodcutterId, ProductionState.Idle);
            ctx.Ui.CloseAllPanels();
            yield return Shot("01_main");

            ctx.Ui.OpenBuildMenu();
            yield return Shot("02_build_menu");
            ctx.Ui.CloseAllPanels();

            ctx.Placement.BeginPlace("woodcutter");
            yield return Shot("03_place_valid");
            if (!Key)
            {
                var hall = s.Buildings.Get(hallId);
                ctx.Placement.HandleTap(ctx.Camera.WorldToScreen(new Vector3(hall.Origin.X + 1.5f, 0f, hall.Origin.Z + 1.5f)));
                yield return Shot("04_place_invalid");
            }
            ctx.Placement.Cancel();

            SetBuilding(woodcutterId, ProductionState.Idle);
            ctx.Selection.Select(woodcutterId);
            yield return Shot("05_sheet_choose_job");

            SetBuilding(woodcutterId, ProductionState.Producing);
            ctx.Selection.Select(woodcutterId);
            yield return Shot("06_sheet_producing");

            if (!Key)
            {
                SetBuilding(woodcutterId, ProductionState.ReadyToCollect);
                ctx.Selection.Select(woodcutterId);
                yield return Shot("07_sheet_ready");

                ctx.Selection.Select(hallId);
                yield return Shot("08_sheet_town_hall");

                ctx.Selection.Select(houseId);
                yield return Shot("09_sheet_house");
            }

            ctx.Ui.CloseAllPanels();
            SetBuilding(woodcutterId, ProductionState.ReadyToCollect);
            yield return Shot("10_world_indicators");

            if (!Key)
            {
                ctx.Ui.Toast.Show(options.Language == Language.German ? "Nicht genug Holz (0/20)" : "Not enough Wood (0/20)", 30f);
                yield return Shot("11_toast");
                ctx.Ui.Toast.Show(string.Empty, 0.01f);

                s.Wallet.Add(CurrencyType.Coins, 98765, CurrencyTransactionReason.DebugGrant);
                yield return new WaitForSecondsRealtime(2f);
                yield return Shot("12_hud_large_numbers");

                ctx.Ui.Fx.PlayCollect(new Vector2(Screen.width * 0.5f, Screen.height * 0.45f), new[] { new CurrencyAmount(CurrencyType.Wood, 30), new CurrencyAmount(CurrencyType.Coins, 25) });
                yield return Shot("13_collect_fx", 0.42f);
                yield return Shot("13b_collect_fx_late", 0.5f);
                yield return new WaitForSecondsRealtime(1.5f);

                ctx.Ui.SetDebugPanel(true);
                yield return Shot("14_debug_menu");
                ctx.Ui.SetDebugPanel(false);
            }
        }

        private void SetBuilding(string id, ProductionState state)
        {
            var production = ctx.Session.Production;
            var instance = ctx.Session.Buildings.Get(id);
            instance.ClearProduction();
            if (state != ProductionState.Idle)
            {
                production.TryStart(id, ctx.Session.Buildings.GetDefinition(instance).ProductionIds[1]);
                if (state == ProductionState.ReadyToCollect) production.FinishAll();
            }
            ctx.Views.RefreshIndicator(instance);
        }

        private IEnumerator Shot(string name, float settleSeconds = 1.0f)
        {
            yield return new WaitForSecondsRealtime(settleSeconds); // let slide/scale animations finish
            yield return new WaitForEndOfFrame();
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            string path = Path.Combine(options.OutputDirectory, $"{tag}_{name}.png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Destroy(texture);
            log.Add($"{path} {Screen.width}x{Screen.height}");
        }
    }
}
