using System;
using System.Collections.Generic;
using UnityEngine;

namespace AgeOfSakura.Game
{
    public struct PointerSample
    {
        public int Id;
        public Vector2 Position;
    }

    /// <summary>
    /// Turns raw pointers into tap / long press / drag / pinch. All thresholds are injected (see InputConfig),
    /// so conflict rules live in one place:
    ///  - a press that starts on UI never becomes a world gesture;
    ///  - small movement stays a tap (threshold in density-independent pixels);
    ///  - a second finger cancels any drag/tap and turns into a pinch, which never selects anything;
    ///  - after a pinch the remaining finger is ignored until everything is lifted.
    /// </summary>
    public sealed class GestureRecognizer
    {
        private enum State
        {
            Idle,
            Pressed,
            LongPressed,
            Dragging,
            Pinching,
            Ignoring
        }

        private readonly float dragThresholdPx;
        private readonly float longPressSeconds;
        private readonly Func<Vector2, bool> isOverUi;

        private State state = State.Idle;
        private int trackedId;
        private Vector2 startPosition;
        private Vector2 lastPosition;
        private float startTime;
        private float lastPinchDistance;

        /// <summary>A finger touched down in the world (not on UI); fires before it is known to be a tap, long press or drag.</summary>
        public event Action<Vector2> PressBegan;
        public event Action<Vector2> Tap;
        public event Action<Vector2> LongPress;
        /// <summary>(startPosition, currentPosition, startedAfterLongPress)</summary>
        public event Action<Vector2, Vector2, bool> DragBegin;
        public event Action<Vector2, Vector2> DragMove;
        /// <summary>(position, cancelledByPinch)</summary>
        public event Action<Vector2, bool> DragEnd;
        /// <summary>(scale factor since last frame, centre)</summary>
        public event Action<float, Vector2> Pinch;

        public GestureRecognizer(float dragThresholdPx, float longPressSeconds, Func<Vector2, bool> isOverUi)
        {
            this.dragThresholdPx = dragThresholdPx;
            this.longPressSeconds = longPressSeconds;
            this.isOverUi = isOverUi;
        }

        public void Update(List<PointerSample> pointers, float now)
        {
            int count = pointers.Count;

            if (count == 0)
            {
                Release();
                return;
            }

            if (count >= 2)
            {
                UpdateMultiTouch(pointers);
                return;
            }

            var p = pointers[0];
            switch (state)
            {
                case State.Idle:
                    trackedId = p.Id;
                    startPosition = lastPosition = p.Position;
                    startTime = now;
                    state = isOverUi(p.Position) ? State.Ignoring : State.Pressed;
                    if (state == State.Pressed) PressBegan?.Invoke(p.Position);
                    break;

                case State.Pressed:
                    if (p.Id != trackedId) { state = State.Ignoring; break; }
                    lastPosition = p.Position;
                    if ((p.Position - startPosition).magnitude > dragThresholdPx) BeginDrag(p.Position, false);
                    else if (now - startTime >= longPressSeconds)
                    {
                        state = State.LongPressed;
                        LongPress?.Invoke(p.Position);
                    }
                    break;

                case State.LongPressed:
                    if (p.Id != trackedId) { state = State.Ignoring; break; }
                    if ((p.Position - startPosition).magnitude > dragThresholdPx) BeginDrag(p.Position, true);
                    break;

                case State.Dragging:
                    if (p.Id != trackedId) break;
                    var delta = p.Position - lastPosition;
                    lastPosition = p.Position;
                    if (delta.sqrMagnitude > 0f) DragMove?.Invoke(p.Position, delta);
                    break;

                case State.Pinching:
                    state = State.Ignoring; // one finger left after a pinch
                    break;
            }
        }

        private void BeginDrag(Vector2 position, bool afterLongPress)
        {
            state = State.Dragging;
            lastPosition = position;
            DragBegin?.Invoke(startPosition, position, afterLongPress);
        }

        private void Release()
        {
            switch (state)
            {
                case State.Pressed:
                    Tap?.Invoke(lastPosition);
                    break;
                case State.Dragging:
                    DragEnd?.Invoke(lastPosition, false);
                    break;
            }
            state = State.Idle;
        }

        private void UpdateMultiTouch(List<PointerSample> pointers)
        {
            if (state == State.Ignoring) return;

            if (state != State.Pinching)
            {
                // A finger that lands on UI must not start a world pinch.
                if (isOverUi(pointers[1].Position)) { state = State.Ignoring; return; }
                if (state == State.Dragging) DragEnd?.Invoke(lastPosition, true);
                state = State.Pinching;
                lastPinchDistance = 0f;
            }

            float distance = Vector2.Distance(pointers[0].Position, pointers[1].Position);
            if (lastPinchDistance > 1f && distance > 1f)
            {
                var center = (pointers[0].Position + pointers[1].Position) * 0.5f;
                Pinch?.Invoke(distance / lastPinchDistance, center);
            }
            lastPinchDistance = distance;
        }
    }
}
