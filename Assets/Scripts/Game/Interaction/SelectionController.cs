using System;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Tap handling in the world: select a building, collect from a ready one, or deselect on empty terrain.
    /// Only one building is selected at a time; selection opens the contextual bottom sheet (UI listens to the event).
    /// </summary>
    public sealed class SelectionController
    {
        private readonly GameSession session;
        private readonly IsoCameraController camera;
        private readonly BuildingViewManager views;
        private readonly IAudioService audio;

        public string SelectedId { get; private set; }

        public event Action<string> SelectionChanged;

        public SelectionController(GameSession session, IsoCameraController camera, BuildingViewManager views, IAudioService audio)
        {
            this.session = session;
            this.camera = camera;
            this.views = views;
            this.audio = audio;
        }

        public void HandleTap(Vector2 screen)
        {
            if (!TryGetBuildingAt(screen, out var view))
            {
                Deselect();
                return;
            }

            session.Production.Refresh();
            var instance = session.Buildings.Get(view.InstanceId);
            if (instance.State == ProductionState.ReadyToCollect)
            {
                // Tapping a finished building (or its bubble) collects immediately.
                session.Production.TryCollect(instance.InstanceId);
                audio.Play(AudioCue.ProductionCollected);
                Select(instance.InstanceId);
                return;
            }
            audio.Play(AudioCue.ButtonTap);
            Select(instance.InstanceId);
        }

        public bool TryGetBuildingAt(Vector2 screen, out BuildingView view)
        {
            view = null;
            var ray = camera.Camera.ScreenPointToRay(screen);
            if (!Physics.Raycast(ray, out var hit, 500f)) return false;
            view = hit.collider.GetComponentInParent<BuildingView>();
            return view != null;
        }

        public void Select(string instanceId)
        {
            if (SelectedId == instanceId)
            {
                SelectionChanged?.Invoke(SelectedId);
                return;
            }
            SetSelectedVisual(SelectedId, false);
            SelectedId = instanceId;
            SetSelectedVisual(SelectedId, true);
            SelectionChanged?.Invoke(SelectedId);
        }

        public void Deselect()
        {
            if (SelectedId == null) return;
            SetSelectedVisual(SelectedId, false);
            SelectedId = null;
            SelectionChanged?.Invoke(null);
        }

        private void SetSelectedVisual(string id, bool selected)
        {
            if (id != null && views.TryGetView(id, out var view)) view.SetSelected(selected);
        }
    }
}
