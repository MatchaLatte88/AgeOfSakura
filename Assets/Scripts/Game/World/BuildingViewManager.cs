using System;
using System.Collections.Generic;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Keeps one <see cref="BuildingView"/> per building and mirrors production state into world indicators.
    /// Timer progress is refreshed from one central <see cref="Tick"/> instead of per-building Update loops.
    /// </summary>
    public sealed class BuildingViewManager
    {
        private readonly GameSession session;
        private readonly WorldView world;
        private readonly GameArt art;
        private readonly Prim prim;
        private readonly IBuildingModelProvider models;
        private readonly VfxFactory vfx;
        private readonly Quaternion cameraRotation;
        private readonly Dictionary<string, BuildingView> views = new Dictionary<string, BuildingView>();

        public event Action<BuildingView> ViewCreated;

        public BuildingViewManager(GameSession session, WorldView world, GameArt art, Prim prim, IBuildingModelProvider models, VfxFactory vfx, Quaternion cameraRotation)
        {
            this.session = session;
            this.world = world;
            this.art = art;
            this.prim = prim;
            this.models = models;
            this.vfx = vfx;
            this.cameraRotation = cameraRotation;

            foreach (var b in session.Buildings.All) CreateView(b, animate: false);

            session.Buildings.BuildingPlaced += OnPlaced;
            session.Buildings.BuildingMoved += OnMoved;
            session.Housing.BuildingUpgraded += OnUpgraded;
            // upgrade readiness depends on the wallet and on what stands around a house, so those changes refresh the markers
            session.Wallet.CurrencyChanged += _ => RefreshUpgradable();
            session.Buildings.BuildingPlaced += _ => RefreshUpgradable();
            session.Buildings.BuildingMoved += (_, __, ___) => RefreshUpgradable();
            session.Production.ProductionStarted += RefreshIndicator;
            session.Production.ProductionCompleted += RefreshIndicator;
            session.Production.ProductionCollected += (b, _) => RefreshIndicator(b);
        }

        public bool TryGetView(string instanceId, out BuildingView view) => views.TryGetValue(instanceId ?? string.Empty, out view);

        private void OnPlaced(BuildingInstance instance)
        {
            if (views.ContainsKey(instance.InstanceId)) return;
            var view = CreateView(instance, animate: true);
            vfx.PlayDust(view.transform.position, 0.9f);
        }

        /// <summary>The model depends on the level, so an upgraded building gets a fresh view (selection is kept).</summary>
        private void OnUpgraded(BuildingInstance instance)
        {
            bool selected = false;
            if (views.TryGetValue(instance.InstanceId, out var old))
            {
                selected = old.IsSelected;
                views.Remove(instance.InstanceId);
                old.gameObject.SetActive(false);
                UnityEngine.Object.Destroy(old.gameObject);
            }
            var view = CreateView(instance, animate: true);
            if (selected) view.SetSelected(true);
            vfx.PlayDust(view.transform.position, 1.2f);
        }

        /// <summary>Recreates a building's view from its current state (tooling such as the style showcase; gameplay uses upgrades).</summary>
        public void Rebuild(BuildingInstance instance) => OnUpgraded(instance);

        private void RefreshUpgradable()
        {
            foreach (var b in session.Buildings.All)
            {
                if (b.State == ProductionState.Idle && session.Housing.IsUpgradable(b)) RefreshIndicator(b);
            }
        }

        private void OnMoved(BuildingInstance instance, GridPos oldOrigin, int oldRotation)
        {
            if (!views.TryGetValue(instance.InstanceId, out var view)) return;
            view.ApplyPlacement(instance, session.Buildings.GetDefinition(instance), session.Grid);
            view.PlaySettle();
            vfx.PlayDust(view.transform.position, 0.8f);
        }

        private BuildingView CreateView(BuildingInstance instance, bool animate)
        {
            var def = session.Buildings.GetDefinition(instance);
            var view = BuildingView.Create(instance, def, session.Grid, art, prim, models, vfx, cameraRotation, world.BuildingsRoot);
            views[instance.InstanceId] = view;
            RefreshIndicator(instance);
            if (animate) view.PlayBuildIn();
            ViewCreated?.Invoke(view);
            return view;
        }

        /// <summary>Applies the indicator for the building's current production state.</summary>
        public void RefreshIndicator(BuildingInstance instance)
        {
            if (!views.TryGetValue(instance.InstanceId, out var view)) return;
            var production = session.Production.GetActiveProduction(instance);
            Sprite icon = production != null && production.Rewards.Count > 0 ? art.Icons.ForCurrency(production.Rewards[0].Currency) : null;

            switch (instance.State)
            {
                case ProductionState.Producing:
                    view.Indicator.SetMode(IndicatorMode.Producing, icon, session.Production.GetProgress01(instance));
                    view.SetProducing(true);
                    break;
                case ProductionState.ReadyToCollect:
                    view.Indicator.SetMode(IndicatorMode.Ready, icon, 1f);
                    view.SetProducing(false);
                    break;
                default:
                    if (session.Housing.IsUpgradable(instance) && session.Housing.Evaluate(instance).CanUpgrade)
                        view.Indicator.SetMode(IndicatorMode.Upgrade, art.Icons.Get("upgrade"), 1f);
                    else
                        view.Indicator.SetMode(IndicatorMode.Hidden, null, 0f);
                    view.SetProducing(false);
                    break;
            }
        }

        public void RefreshAll()
        {
            foreach (var b in session.Buildings.All) RefreshIndicator(b);
        }

        /// <summary>Call a few times per second: updates progress bars of running productions only.</summary>
        public void Tick()
        {
            var all = session.Buildings.All;
            for (int i = 0; i < all.Count; i++)
            {
                var b = all[i];
                if (b.State != ProductionState.Producing) continue;
                if (views.TryGetValue(b.InstanceId, out var view)) view.Indicator.SetProgress(session.Production.GetProgress01(b));
            }
        }
    }
}
