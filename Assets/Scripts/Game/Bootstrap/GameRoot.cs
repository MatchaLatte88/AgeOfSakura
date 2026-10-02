using AgeOfSakura.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Composition root: creates the services and views, wires their events, and drives the few things that
    /// need a heartbeat (input, production refresh, save flush). Gameplay rules live in <see cref="GameSession"/>;
    /// this class contains no game balance.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameRoot : MonoBehaviour
    {
        private const float SlowTickSeconds = 0.25f;

        private GameArt art;
        private GameObject runtimeRoot;
        private GameSession session;
        private IsoCameraController cameraController;
        private InputService input;
        private BuildingViewManager views;
        private GameUi ui;
        [SerializeField] private bool drawGridGizmos = true;

        private float nextSlowTick;
        private bool restartRequested;

        private void Awake()
        {
            GameLog.Sink = UnityLogSink;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            // Landscape only for Prototype 0.01 (both landscape orientations allowed).
            Screen.orientation = ScreenOrientation.AutoRotation;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
        }

        private void Start() => Build();

        private static void UnityLogSink(LogLevel level, LogCategory category, string message)
        {
            string text = $"[{category.ToString().ToUpperInvariant()}] {message}";
            switch (level)
            {
                case LogLevel.Warning: Debug.LogWarning(text); break;
                case LogLevel.Error: Debug.LogError(text); break;
                default: Debug.Log(text); break;
            }
        }

        // ------------------------------------------------------------------ build / teardown

        private void Build()
        {
            var jsonAsset = Resources.Load<TextAsset>("Definitions/game_definitions");
            if (jsonAsset == null) throw new System.IO.FileNotFoundException("Resources/Definitions/game_definitions.json is missing.");

            if (art == null) art = new GameArt();
            var prim = new Prim(art);
            var vfx = new VfxFactory(art);
            var proceduralModels = new ProceduralBuildingModels(art, prim, vfx);

            // Fails loudly (exception) if any definition is broken or a model is not registered.
            var defs = DefinitionLoader.LoadValidated(jsonAsset.text, proceduralModels.Has);

            bool showcase = UiShowcase.TryParse(out var showcaseOptions);
            if (showcase) Loc.Current = showcaseOptions.Language;
            // The UI showcase never reads or writes a real save.
            ISaveStorage storage = showcase ? (ISaveStorage)new InMemorySaveStorage() : new FileSaveStorage();
            session = new GameSession(defs, storage, new SystemTimeProvider(), new NullAnalyticsService());
            var startKind = session.Start();

            runtimeRoot = new GameObject("Runtime");
            runtimeRoot.transform.SetParent(transform, false);
            var animator = runtimeRoot.AddComponent<WorldAnimator>();

            SetUpCamera(defs);
            DisableForeignLights();
            var props = new PropFactory(art, prim, animator, vfx) { CameraRotation = cameraController.Rotation };
            proceduralModels.Props = props; // buildings grow their own trees and shrubs
            IBuildingModelProvider models = proceduralModels;

            var world = new WorldBuilder(art, prim, props, animator).Build(runtimeRoot.transform, session);

            views = new BuildingViewManager(session, world, art, prim, models, vfx, cameraController.Rotation);
            var actors = new ActorFactory(art, prim);
            NpcManager.Create(runtimeRoot.transform, session, actors);
            FishingBoatManager.Create(runtimeRoot.transform, session, art, prim, actors);

            IAudioService audio = new NullAudioService();
            var selection = new SelectionController(session, cameraController, views, audio);
            var placement = new PlacementController(session, cameraController, world, art, prim, models, views, audio, defs.Input);
            placement.BuildingMoved += instance => selection.Select(instance.InstanceId);

            input = new InputService(new InputSystemPointerSource(), defs.Input, new UiHitTester()) { Interceptor = placement };
            WireInput(defs, selection, placement);

            var debug = DebugCommands.Available ? new DebugCommands(session, world, () => restartRequested = true) : null;
            ui = new GameUi(runtimeRoot.transform, session, new UiKit(art.Icons), cameraController, views, placement, selection, audio, debug);

            if (startKind == SessionStartKind.RecoveredFromCorruptSave) ui.Toast.Show(Loc.T("save.corrupt"), 5f);
            else if (startKind == SessionStartKind.SaveTooNew) ui.Toast.Show(Loc.T("save.too_new"), 5f);

            if (showcase)
            {
                UiShowcase.Begin(runtimeRoot, showcaseOptions, new UiShowcase.Context
                {
                    Session = session, Ui = ui, Selection = selection, Placement = placement, Camera = cameraController, Views = views,
                    Art = art, Prim = prim, Props = props, Actors = actors, Models = models
                });
            }

            nextSlowTick = 0f;
            audio.Play(AudioCue.AmbientLoop);
        }

        private void SetUpCamera(GameDefinitions defs)
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
                go.AddComponent<AudioListener>();
            }
            cameraController = cam.GetComponent<IsoCameraController>();
            if (cameraController == null) cameraController = cam.gameObject.AddComponent<IsoCameraController>();
            var grid = session.Grid;
            cameraController.Initialize(cam, defs.Camera, new Vector2(grid.Width * grid.CellSize, grid.Height * grid.CellSize));
        }

        private void DisableForeignLights()
        {
            // The default scene ships its own directional light; the world builds a tuned one.
            foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional && !light.transform.IsChildOf(transform)) light.enabled = false;
            }
        }

        private void WireInput(GameDefinitions defs, SelectionController selection, PlacementController placement)
        {
            // Pressing a ready building collects at once; keep holding and sweep over the others to collect them too (no sheet, no camera pan).
            var sweep = new CollectSweep(selection);
            input.PressInterceptor = sweep;
            input.PressBegan += position =>
            {
                if (!placement.IsActive) sweep.Press(position);
            };

            input.Tapped += position =>
            {
                if (sweep.Armed) return; // the press itself already collected
                if (placement.IsActive) placement.HandleTap(position);
                else selection.HandleTap(position);
            };

            input.LongPressed += position =>
            {
                if (placement.IsActive || sweep.Armed) return;
                if (!selection.TryGetBuildingAt(position, out var view)) return;
                var instance = session.Buildings.Get(view.InstanceId);
                if (!session.Buildings.GetDefinition(instance).Movable) return;
                selection.Deselect();
                placement.BeginMove(view.InstanceId); // the same finger can keep dragging: it continues as a ghost drag
            };

            input.CameraPanBegan += cameraController.BeginPan;
            input.CameraPanned += cameraController.Pan;
            input.CameraPanEnded += cameraController.EndPan;
            input.Pinched += (scale, center) => cameraController.Zoom(Mathf.Pow(scale, defs.Camera.PinchSensitivity), center);
            input.Scrolled += (ticks, position) => cameraController.Zoom(1f + ticks * defs.Camera.ScrollZoomStep, position);
        }

        private void Teardown()
        {
            // Immediate: the old EventSystem must be gone before Build() checks EventSystem.current.
            if (runtimeRoot != null) DestroyImmediate(runtimeRoot);
            runtimeRoot = null;
            session = null;
            input = null;
            views = null;
            ui = null;
        }

        // ------------------------------------------------------------------ heartbeat

        private void Update()
        {
            if (session == null) return;

            input.Update();

            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ui.HandleBack(); // Android back also arrives as Escape

            if (Time.unscaledTime >= nextSlowTick)
            {
                nextSlowTick = Time.unscaledTime + SlowTickSeconds;
                session.Production.Refresh();
                views.Tick();
                ui.Tick();
            }
        }

        private void LateUpdate()
        {
            if (restartRequested)
            {
                restartRequested = false;
                session?.FlushIfDirty(); // a language switch must not lose the last action; after a save reset writes are disabled
                Teardown();
                Build();
                return;
            }
            session?.FlushIfDirty();
        }

#if UNITY_EDITOR
        /// <summary>Scene-view visualisation of the logical grid while playing: green = buildable, red = blocked terrain, grey = locked, orange = occupied.</summary>
        private void OnDrawGizmos()
        {
            if (!drawGridGizmos || session == null) return;
            var grid = session.Grid;
            for (int x = 0; x < grid.Width; x++)
            {
                for (int z = 0; z < grid.Height; z++)
                {
                    var cell = grid.GetCell(x, z);
                    Color color;
                    if (!cell.Unlocked) color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
                    else if (cell.IsOccupied) color = new Color(1f, 0.6f, 0.1f, 0.55f);
                    else if (!cell.TerrainBuildable) color = new Color(0.9f, 0.2f, 0.2f, 0.55f);
                    else color = new Color(0.2f, 0.9f, 0.3f, 0.45f);
                    Gizmos.color = color;
                    Gizmos.DrawWireCube(new Vector3((x + 0.5f) * grid.CellSize, 0.02f, (z + 0.5f) * grid.CellSize), new Vector3(grid.CellSize * 0.96f, 0.02f, grid.CellSize * 0.96f));
                }
            }
        }
#endif

        // ------------------------------------------------------------------ app lifecycle

        private void OnApplicationPause(bool paused)
        {
            if (session == null) return;
            if (paused)
            {
                session.SaveNow();
                return;
            }
            // Do not assume the process kept running: recompute production from UTC and refresh the visuals.
            session.Resume();
            views.RefreshAll();
            ui.Resync();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) session?.SaveNow();
        }

        private void OnApplicationQuit()
        {
            session?.SaveNow();
        }
    }

    public static class GameBootstrap
    {
        /// <summary>Starts the game in any scene, so "press Play" works even in an empty scene.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Object.FindFirstObjectByType<GameRoot>() != null) return;
            new GameObject("GameRoot").AddComponent<GameRoot>();
        }
    }
}
