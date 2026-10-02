using System.Collections.Generic;
using AgeOfSakura.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>Speech-bubble style nudge above the Build button ("Build: Woodcutter"). Bobs gently so it draws the eye.</summary>
    public sealed class HintView : MonoBehaviour
    {
        private const float RestY = 232f;
        private RectTransform rect;
        private TMP_Text text;
        private float shownAt;

        public static HintView Create(UiKit kit, RectTransform parent)
        {
            var go = kit.Empty(parent, "Hint");
            var view = go.AddComponent<HintView>();
            view.Build(kit, go.GetComponent<RectTransform>());
            return view;
        }

        private void Build(UiKit kit, RectTransform root)
        {
            rect = root;
            UiKit.Place(rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, RestY), new Vector2(880f, 190f));

            var pill = kit.Sliced(rect, "Bubble", "pill_paper", false);
            UiKit.AddShadow(pill.gameObject, -8f, 0.3f);
            UiKit.Place(pill.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 46f), new Vector2(880f, 130f));
            text = kit.Text(pill.transform, string.Empty, TextStyle.Ink, 42, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold, "Text", wrap: true);
            UiKit.Stretch(text.rectTransform, 34f, 8f, 34f, 12f);

            var tail = kit.Picture(rect, "Tail", "tail_paper", new Vector2(64f, 48f));
            UiKit.Place(tail.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(-150f, 6f), new Vector2(64f, 48f));
            gameObject.SetActive(false);
        }

        public void Show(string message)
        {
            if (!gameObject.activeSelf) shownAt = Time.unscaledTime;
            text.text = message;
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        private void Update()
        {
            float t = Time.unscaledTime - shownAt;
            float pop = Ease.OutBack(Mathf.Clamp01(t / 0.4f));
            rect.localScale = Vector3.one * pop;
            rect.anchoredPosition = new Vector2(-34f, RestY + Mathf.Sin(Time.unscaledTime * 3.2f) * 8f);
        }
    }

    /// <summary>
    /// Builds the whole uGUI tree and decides what is visible when. Exactly one of these is showing at a time:
    /// placement bar / build menu / building sheet / nothing (Build button + hint).
    /// Game logic stays in services; this class only reacts to their events.
    /// </summary>
    public sealed class GameUi
    {
        private readonly GameSession session;
        private readonly UiKit kit;
        private readonly IsoCameraController camera;
        private readonly BuildingViewManager views;
        private readonly PlacementController placement;
        private readonly SelectionController selection;
        private readonly IAudioService audio;

        private readonly BottomSheetView sheet;
        private readonly SheetPresenter presenter;
        private readonly PlacementBarView placementBar;
        private readonly UiButton buildButton;
        private readonly UiPulse buildPulse;
        private readonly HintView hint;
        private readonly DebugMenuView debugMenu;

        private bool buildMenuOpen;
        private string modeKey = string.Empty;

        public ToastView Toast { get; }
        public HudView Hud { get; }
        public UiFx Fx { get; }
        public GameObject Root { get; }

        public GameUi(Transform parent, GameSession session, UiKit kit, IsoCameraController camera, BuildingViewManager views,
            PlacementController placement, SelectionController selection, IAudioService audio, DebugCommands debug)
        {
            this.session = session;
            this.kit = kit;
            this.camera = camera;
            this.views = views;
            this.placement = placement;
            this.selection = selection;
            this.audio = audio;

            EnsureEventSystem(parent);
            kit.ClickFeedback = () => audio.Play(AudioCue.ButtonTap);

            var canvasGo = new GameObject("UI", typeof(RectTransform));
            canvasGo.transform.SetParent(parent, false);
            Root = canvasGo;
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f; // landscape: keep height constant, let width vary with the aspect ratio
            canvasGo.AddComponent<GraphicRaycaster>();
            var canvasRect = canvasGo.GetComponent<RectTransform>();
            kit.Motion = canvasGo.AddComponent<UiMotion>();

            BuildScreenDecoration(canvasRect);

            var safe = kit.Empty(canvasGo.transform, "SafeArea");
            var safeRect = UiKit.Rect(safe);
            UiKit.Stretch(safeRect);
            safe.AddComponent<SafeAreaFitter>();

            Hud = HudView.Create(kit, safeRect, session.Wallet);
            Toast = ToastView.Create(kit, safeRect);
            hint = HintView.Create(kit, safeRect);

            buildButton = kit.Button(safeRect, "BuildButton", Loc.T("build"), new Vector2(372f, 178f), UiButtonStyle.Primary, kit.Icons.Get("hammer"), 64);
            UiKit.Place(buildButton.Rect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 30f), new Vector2(372f, 178f));
            kit.AddShine(buildButton, 4.2f);
            buildPulse = buildButton.GameObject.AddComponent<UiPulse>();
            buildPulse.Target = buildButton.Rect;
            buildPulse.Amount = 0.022f;
            buildPulse.Speed = 3f;
            buildPulse.enabled = false;
            buildButton.OnClick(OpenBuildMenu);

            sheet = BottomSheetView.Create(kit, safeRect);
            presenter = new SheetPresenter(kit, sheet, session, audio, Toast, BeginPlace);
            sheet.CloseRequested += CloseSheet;
            placementBar = PlacementBarView.Create(kit, safeRect, placement);

            Fx = UiFx.Create(kit, canvasRect, Hud);

            if (debug != null) debugMenu = new DebugMenuView(kit, safeRect, debug, Toast);

            Subscribe();
            ApplyMode();
        }

        /// <summary>Soft dark edges (vignette, top and bottom fades) so UI stays legible over bright ground and frames the scene.</summary>
        private void BuildScreenDecoration(RectTransform canvasRect)
        {
            var vignette = kit.Empty(canvasRect, "Vignette").AddComponent<Image>();
            vignette.sprite = kit.Icons.Get("vignette");
            vignette.raycastTarget = false;
            UiKit.Stretch(vignette.rectTransform);

            var top = kit.Empty(canvasRect, "FadeTop").AddComponent<Image>();
            top.sprite = kit.Icons.Get("fade_top");
            top.raycastTarget = false;
            UiKit.Place(top.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 300f));

            var bottom = kit.Empty(canvasRect, "FadeBottom").AddComponent<Image>();
            bottom.sprite = kit.Icons.Get("fade_bottom");
            bottom.raycastTarget = false;
            UiKit.Place(bottom.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 380f));
        }

        private static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            var module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        private void Subscribe()
        {
            selection.SelectionChanged += _ =>
            {
                buildMenuOpen = false;
                ApplyMode();
            };
            placement.StateChanged += ApplyMode;
            placement.Refused += message => Toast.Show(message);
            placement.BuildingPlaced += instance => { if (!session.Buildings.GetDefinition(instance).Chain) selection.Select(instance.InstanceId); };

            session.Wallet.CurrencyChanged += _ =>
            {
                RefreshSheetContent();
                RefreshHint();
            };
            session.Production.ProductionStarted += OnStarted;
            session.Production.ProductionCompleted += _ =>
            {
                RefreshSheetContent();
                RefreshHint();
            };
            session.Production.ProductionCollected += OnCollected;
            session.Buildings.BuildingPlaced += _ => RefreshHint();
            session.Buildings.BuildingMoved += (_, __, ___) => RefreshHint();
            session.Housing.BuildingUpgraded += OnUpgraded;
        }

        private void OnCollected(BuildingInstance instance, IReadOnlyList<CurrencyAmount> rewards)
        {
            Vector3 worldPoint = Vector3.zero;
            if (views.TryGetView(instance.InstanceId, out var view)) worldPoint = view.ProductionIndicatorAnchor.position;
            Fx.PlayCollect(camera.WorldToScreen(worldPoint), rewards);
            RefreshSheetContent();
            RefreshHint();
        }

        /// <summary>A job that consumes goods (houses eating rice): the goods visibly travel from the counter to the building.</summary>
        private void OnStarted(BuildingInstance instance)
        {
            RefreshSheetContent();
            RefreshHint();
            var production = session.Production.GetActiveProduction(instance);
            if (production == null || production.Inputs.Count == 0 || !views.TryGetView(instance.InstanceId, out var view)) return;
            Fx.PlayDelivery(camera.WorldToScreen(view.ProductionIndicatorAnchor.position), production.Inputs);
        }

        private void OnUpgraded(BuildingInstance instance)
        {
            if (views.TryGetView(instance.InstanceId, out var view))
                Fx.PlayCollect(camera.WorldToScreen(view.ProductionIndicatorAnchor.position), new CurrencyAmount[0]);
            RefreshSheetContent();
            RefreshHint();
        }

        // ------------------------------------------------------------------ mode handling

        public void OpenBuildMenu()
        {
            if (placement.IsActive) return;
            selection.Deselect();
            buildMenuOpen = true;
            ApplyMode();
        }

        private void CloseSheet()
        {
            buildMenuOpen = false;
            selection.Deselect();
            ApplyMode();
        }

        private void BeginPlace(string definitionId)
        {
            placement.BeginPlace(definitionId); // refusal (cannot afford) is reported through placement.Refused
        }

        /// <summary>Closes placement, sheet and debug panel (used by the UI showcase and tests).</summary>
        public void CloseAllPanels()
        {
            if (placement.IsActive) placement.Cancel();
            buildMenuOpen = false;
            selection.Deselect();
            debugMenu?.Close();
            ApplyMode();
        }

        public void OpenUpgradePage() => presenter.ShowUpgradePage();

        public void SetDebugPanel(bool open)
        {
            if (debugMenu == null) return;
            if (open) debugMenu.Open(); else debugMenu.Close();
        }

        /// <summary>Android back / Escape: closes the topmost panel. Never required to play.</summary>
        public void HandleBack()
        {
            if (placement.IsActive) placement.Cancel();
            else if (buildMenuOpen || selection.SelectedId != null) CloseSheet();
            else debugMenu?.Close();
        }

        /// <summary>Recomputes what is visible. Content is only rebuilt when the mode actually changes.</summary>
        private void ApplyMode()
        {
            string key;
            if (placement.IsActive) key = "place";
            else if (buildMenuOpen) key = "build";
            else if (selection.SelectedId != null) key = "sel:" + selection.SelectedId;
            else key = "none";

            bool changed = key != modeKey;
            modeKey = key;

            if (key == "place")
            {
                if (changed) presenter.Hide();
                SetBuildButton(false);
                hint.Hide();
                selection.Deselect();
                return;
            }

            if (key == "build")
            {
                if (changed) presenter.ShowBuildMenu();
                SetBuildButton(false);
                hint.Hide();
            }
            else if (key == "none")
            {
                if (changed) presenter.Hide();
                SetBuildButton(true);
                RefreshHint();
            }
            else
            {
                if (changed)
                {
                    presenter.ShowBuilding(selection.SelectedId);
                    RevealSelected();
                }
                SetBuildButton(false);
                hint.Hide();
            }
        }

        private void SetBuildButton(bool visible)
        {
            if (buildButton.GameObject.activeSelf == visible) return;
            buildButton.GameObject.SetActive(visible);
            if (visible && kit.Motion != null) kit.Motion.PopIn(buildButton.Rect, UiKit.Fadeable(buildButton.GameObject), 0.12f, 0.4f, 0.8f);
        }

        private void RevealSelected()
        {
            if (!session.Buildings.TryGet(selection.SelectedId, out var instance) || !views.TryGetView(instance.InstanceId, out var view)) return;
            // keep the building visible above the sheet: glide the camera so it sits in the free upper part of the screen
            camera.RevealAt(view.transform.position + Vector3.up * 1.2f, new Vector2(0.5f, 0.68f));
        }

        private void RefreshSheetContent()
        {
            if (modeKey == "build" || modeKey.StartsWith("sel:")) presenter.Refresh();
        }

        /// <summary>One-line nudge with the next useful step (see <see cref="GoalAdvisor"/>); hidden while a panel is open or nothing is to do.</summary>
        private void RefreshHint()
        {
            if (modeKey != "none") return;
            var goal = GoalAdvisor.Next(session);
            if (goal.Kind == GoalKind.None)
            {
                hint.Hide();
                buildPulse.enabled = false;
                return;
            }
            BuildingDefinition def = null;
            if (goal.BuildingId != null) session.Definitions.TryGetBuilding(goal.BuildingId, out def);
            hint.Show(Loc.GoalText(goal, def));
            buildPulse.enabled = goal.Kind == GoalKind.Build;
        }

        /// <summary>Called a few times per second.</summary>
        public void Tick() => presenter.Tick();

        /// <summary>After the app resumed: bring visuals in line with (possibly changed) state.</summary>
        public void Resync()
        {
            presenter.Refresh();
            RefreshHint();
        }
    }
}
