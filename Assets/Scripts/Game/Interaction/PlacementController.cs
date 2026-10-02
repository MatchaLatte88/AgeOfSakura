using System;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    public enum PlacementMode
    {
        None,
        PlaceNew,
        MoveExisting
    }

    /// <summary>
    /// Building placement and moving: ghost model + footprint marker snapped to the grid, dragged by finger,
    /// confirmed or cancelled explicitly. Nothing is charged or changed until <see cref="Confirm"/> succeeds.
    /// Doubles as the drag interceptor so dragging the ghost never pans the camera.
    /// </summary>
    public sealed class PlacementController : IDragInterceptor
    {
        private readonly GameSession session;
        private readonly IsoCameraController camera;
        private readonly WorldView world;
        private readonly GameArt art;
        private readonly IBuildingModelProvider models;
        private readonly BuildingViewManager views;
        private readonly IAudioService audio;
        private readonly InputConfig inputConfig;
        private readonly FootprintMarker marker;
        private readonly Transform ghostRoot;

        private GameObject ghost;
        private Transform ghostModel;
        private BuildingModel ghostModelInfo;
        private Quaternion ghostBaseRotation;
        private BuildingDefinition definition;
        private string movingInstanceId;
        private GridPos origin;
        private int rotation;
        private Vector3 grabOffset;

        public PlacementMode Mode { get; private set; }
        public bool IsActive => Mode != PlacementMode.None;
        public BuildingDefinition Definition => definition;
        public PlacementCheck Check { get; private set; }
        public bool CanAfford { get; private set; } = true;

        /// <summary>Confirm is allowed: footprint free and (for new buildings) affordable.</summary>
        public bool IsValid => IsActive && Check == PlacementCheck.Ok && CanAfford;

        /// <summary>Raised when mode, position or validity changed (UI refreshes its bar).</summary>
        public event Action StateChanged;
        /// <summary>Raised with a player-facing message when an action was refused.</summary>
        public event Action<string> Refused;
        public event Action<BuildingInstance> BuildingPlaced;
        public event Action<BuildingInstance> BuildingMoved;

        public PlacementController(GameSession session, IsoCameraController camera, WorldView world, GameArt art, Prim prim,
            IBuildingModelProvider models, BuildingViewManager views, IAudioService audio, InputConfig inputConfig)
        {
            this.session = session;
            this.camera = camera;
            this.world = world;
            this.art = art;
            this.models = models;
            this.views = views;
            this.audio = audio;
            this.inputConfig = inputConfig;

            ghostRoot = new GameObject("PlacementGhost").transform;
            ghostRoot.SetParent(world.Root, false);
            marker = new FootprintMarker(art, prim, ghostRoot);
            marker.Hide();
            session.Wallet.CurrencyChanged += _ => { if (IsActive) Revalidate(); };
        }

        // ------------------------------------------------------------------ start / stop

        /// <summary>Enters placement mode. Refuses (without entering a broken state) if the building cannot be afforded.</summary>
        public bool BeginPlace(string definitionId)
        {
            if (IsActive) Cancel();
            var def = session.Definitions.GetBuilding(definitionId);
            if (!session.Wallet.CanAfford(def.BuildCost))
            {
                audio.Play(AudioCue.InvalidAction);
                Refused?.Invoke(NotEnoughMessage(def));
                return false;
            }

            definition = def;
            Mode = PlacementMode.PlaceNew;
            movingInstanceId = null;
            rotation = 0;
            var (w, h) = Size();
            origin = FindStartOrigin(w, h);
            EnterMode();
            return true;
        }

        private static readonly (int dx, int dz)[] ChainDirections = { (1, 0), (0, 1), (-1, 0), (0, -1) };
        private int chainDirection;

        /// <summary>Roads are laid cell by cell: after one is placed the next ghost appears on the free neighbour, going on in the same direction when it can.</summary>
        private void BeginChain(BuildingDefinition def, GridPos last)
        {
            if (!session.Wallet.CanAfford(def.BuildCost)) return;
            for (int i = 0; i < ChainDirections.Length; i++)
            {
                int d = (chainDirection + i) % ChainDirections.Length;
                var next = new GridPos(last.X + ChainDirections[d].dx, last.Z + ChainDirections[d].dz);
                if (session.Grid.CheckPlacement(next, def.FootprintWidth, def.FootprintHeight, null, def.RuleFor(0)) != PlacementCheck.Ok) continue;
                chainDirection = d;
                definition = def;
                Mode = PlacementMode.PlaceNew;
                movingInstanceId = null;
                rotation = 0;
                origin = next;
                EnterMode();
                return;
            }
        }

        public bool BeginMove(string instanceId)
        {
            if (IsActive) Cancel();
            if (!session.Buildings.TryGet(instanceId, out var instance)) return false;
            var def = session.Buildings.GetDefinition(instance);
            if (!def.Movable)
            {
                Refused?.Invoke(Loc.T("cannot_move", Loc.BuildingName(def)));
                return false;
            }

            definition = def;
            Mode = PlacementMode.MoveExisting;
            movingInstanceId = instanceId;
            rotation = instance.Rotation;
            origin = instance.Origin;
            if (views.TryGetView(instanceId, out var view)) view.SetVisible(false);
            EnterMode();
            return true;
        }

        public void Cancel()
        {
            if (!IsActive) return;
            ExitMode();
        }

        public void Rotate()
        {
            if (!IsActive || !definition.Rotatable) return;
            var (oldW, oldH) = Size();
            rotation = Footprint.NormalizeRotation(rotation + 1);
            var (w, h) = Size();
            // keep the footprint centre where it was
            origin = new GridPos(origin.X + (oldW - w) / 2, origin.Z + (oldH - h) / 2);
            Revalidate();
            audio.Play(AudioCue.ButtonTap);
        }

        /// <summary>Validates again and commits. On refusal the mode stays active so the player can adjust.</summary>
        public bool Confirm()
        {
            if (!IsActive) return false;

            if (Mode == PlacementMode.PlaceNew)
            {
                var result = session.Buildings.TryPlace(definition.Id, origin, rotation);
                if (!result.Success)
                {
                    audio.Play(AudioCue.InvalidAction);
                    Refused?.Invoke(result.Status == PlaceStatus.CannotAfford ? NotEnoughMessage(definition) : Describe(result.Check));
                    Revalidate();
                    return false;
                }
                audio.Play(AudioCue.BuildingPlaced);
                var placedDefinition = definition;
                var placedOrigin = origin;
                ExitMode();
                BuildingPlaced?.Invoke(result.Instance);
                if (placedDefinition.Chain) BeginChain(placedDefinition, placedOrigin);
                return true;
            }

            var check = session.Buildings.TryMove(movingInstanceId, origin, rotation);
            if (check != PlacementCheck.Ok)
            {
                audio.Play(AudioCue.InvalidAction);
                Refused?.Invoke(Describe(check));
                Revalidate();
                return false;
            }
            var moved = session.Buildings.Get(movingInstanceId);
            audio.Play(AudioCue.BuildingPlaced);
            ExitMode();
            BuildingMoved?.Invoke(moved);
            return true;
        }

        // ------------------------------------------------------------------ input

        /// <summary>Tap on the ground moves the ghost there (footprint centred on the tapped point).</summary>
        public void HandleTap(Vector2 screen)
        {
            if (!IsActive) return;
            SetCentre(camera.ScreenToGround(screen));
        }

        public bool TryBeginDrag(Vector2 pressScreenPosition, Vector2 currentScreenPosition, bool afterLongPress)
        {
            if (!IsActive) return false;
            var press = camera.ScreenToGround(pressScreenPosition);
            var (w, h) = Size();
            var (cx, cz) = session.Grid.FootprintCenterToWorld(origin, w, h);
            var centre = new Vector3(cx, 0f, cz);

            bool grabbed = afterLongPress && Mode == PlacementMode.MoveExisting;
            if (!grabbed)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(press.x - cx) - w * 0.5f * session.Grid.CellSize);
                float dz = Mathf.Max(0f, Mathf.Abs(press.z - cz) - h * 0.5f * session.Grid.CellSize);
                grabbed = Mathf.Sqrt(dx * dx + dz * dz) <= inputConfig.PlacementGrabRadiusCells * session.Grid.CellSize;
            }
            if (!grabbed) return false; // far from the ghost: let the camera pan

            grabOffset = centre - camera.ScreenToGround(currentScreenPosition);
            grabOffset.y = 0f;
            return true;
        }

        public void Drag(Vector2 screenPosition) => SetCentre(camera.ScreenToGround(screenPosition) + grabOffset);

        public void EndDrag(Vector2 screenPosition, bool cancelled) { }

        // ------------------------------------------------------------------ internals

        private (int w, int h) Size()
        {
            Footprint.Size(definition.FootprintWidth, definition.FootprintHeight, rotation, out int w, out int h);
            return (w, h);
        }

        private void SetCentre(Vector3 worldCentre)
        {
            var (w, h) = Size();
            float cs = session.Grid.CellSize;
            var next = new GridPos(Mathf.RoundToInt(worldCentre.x / cs - w * 0.5f), Mathf.RoundToInt(worldCentre.z / cs - h * 0.5f));
            if (next == origin) return;
            origin = next;
            Revalidate();
        }

        private GridPos FindStartOrigin(int w, int h)
        {
            var screenCentre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var ground = camera.ScreenToGround(screenCentre);
            float cs = session.Grid.CellSize;
            int cx = Mathf.RoundToInt(ground.x / cs - w * 0.5f);
            int cz = Mathf.RoundToInt(ground.z / cs - h * 0.5f);
            for (int r = 0; r <= 8; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        var p = new GridPos(cx + dx, cz + dz);
                        if (session.Grid.CheckPlacement(p, w, h, null, definition.RuleFor(rotation)) == PlacementCheck.Ok) return p;
                    }
                }
            }
            return new GridPos(cx, cz); // nothing free nearby: shown as invalid, the player can still move it
        }

        private void EnterMode()
        {
            ghost = new GameObject("Ghost");
            ghost.transform.SetParent(ghostRoot, false);
            var model = models.Create(definition.VisualId, definition.VisualLevel, definition.FootprintWidth, definition.FootprintHeight);
            model.transform.SetParent(ghost.transform, false);
            ghostModel = model.transform;
            ghostModelInfo = model.GetComponent<BuildingModel>();
            ghostBaseRotation = ghostModel.localRotation;
            MakeGhost(model);

            world.Grid.SetPlacementVisible(true, movingInstanceId);
            Revalidate();
        }

        private void ExitMode()
        {
            if (movingInstanceId != null && views.TryGetView(movingInstanceId, out var view))
            {
                view.SetVisible(true);
                views.RefreshIndicator(session.Buildings.Get(movingInstanceId));
            }
            if (ghost != null) UnityEngine.Object.Destroy(ghost);
            ghost = null;
            ghostModel = null;
            ghostModelInfo = null;
            marker.Hide();
            world.Grid.SetPlacementVisible(false);
            Mode = PlacementMode.None;
            definition = null;
            movingInstanceId = null;
            StateChanged?.Invoke();
        }

        private void MakeGhost(GameObject model)
        {
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++) mats[i] = art.Ghost(mats[i]);
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        private void Revalidate()
        {
            var (w, h) = Size();
            Check = session.Grid.CheckPlacement(origin, w, h, movingInstanceId, definition.RuleFor(rotation));
            CanAfford = Mode != PlacementMode.PlaceNew || session.Wallet.CanAfford(definition.BuildCost);

            var (cx, cz) = session.Grid.FootprintCenterToWorld(origin, w, h);
            ghost.transform.position = new Vector3(cx, 0.02f, cz);
            ghostModel.localRotation = ghostModelInfo != null && ghostModelInfo.IgnorePlacementRotation
                ? ghostBaseRotation
                : ghostBaseRotation * Quaternion.Euler(0f, 90f * rotation, 0f);
            marker.Show(new Vector3(cx, 0f, cz), w, h, Check == PlacementCheck.Ok);
            StateChanged?.Invoke();
        }

        public static string Describe(PlacementCheck check)
        {
            switch (check)
            {
                case PlacementCheck.Ok: return string.Empty;
                case PlacementCheck.OutOfBounds: return Loc.T("place.outside");
                case PlacementCheck.Locked: return Loc.T("place.locked");
                case PlacementCheck.BlockedTerrain: return Loc.T("place.blocked");
                case PlacementCheck.Occupied: return Loc.T("place.occupied");
                case PlacementCheck.NeedsShore: return Loc.T("place.shore");
                default: return check.ToString();
            }
        }

        public string NotEnoughMessage(BuildingDefinition def)
        {
            foreach (var cost in def.BuildCost)
            {
                long have = session.Wallet.GetBalance(cost.Currency);
                if (have < cost.Amount) return Loc.T("not_enough", Loc.Currency(cost.Currency), have, cost.Amount);
            }
            return Loc.T("cannot_afford");
        }
    }
}
