using System;
using System.Collections.Generic;
using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Lets a mode (building placement) claim a drag before the camera gets it.
    /// TryBeginDrag receives the position where the finger first touched down.
    /// </summary>
    public interface IDragInterceptor
    {
        bool TryBeginDrag(Vector2 pressScreenPosition, Vector2 currentScreenPosition, bool afterLongPress);
        void Drag(Vector2 screenPosition);
        void EndDrag(Vector2 screenPosition, bool cancelled);
    }

    /// <summary>
    /// Central input: reads pointers, recognises gestures and routes them. Dragging pans the camera unless the
    /// active <see cref="Interceptor"/> claims it. Everything is ticked from GameRoot (no polling scattered around).
    /// </summary>
    public sealed class InputService
    {
        private readonly IPointerSource source;
        private readonly GestureRecognizer recognizer;
        private readonly List<PointerSample> pointers = new List<PointerSample>(4);
        private IDragInterceptor activeDrag;

        public IDragInterceptor Interceptor { get; set; }

        public event Action<Vector2> Tapped;
        public event Action<Vector2> LongPressed;
        public event Action<Vector2> CameraPanBegan;
        public event Action<Vector2> CameraPanned;
        public event Action CameraPanEnded;
        public event Action<float, Vector2> Pinched;
        public event Action<float, Vector2> Scrolled;

        public InputService(IPointerSource source, InputConfig config, UiHitTester uiHitTester)
        {
            this.source = source;
            float dpi = Screen.dpi > 1f ? Screen.dpi : 160f;
            float thresholdPx = config.DragThresholdDp * dpi / 160f;
            recognizer = new GestureRecognizer(thresholdPx, config.LongPressSeconds, uiHitTester.IsOverUi);

            recognizer.Tap += p => Tapped?.Invoke(p);
            recognizer.LongPress += p => LongPressed?.Invoke(p);
            recognizer.DragBegin += OnDragBegin;
            recognizer.DragMove += (pos, delta) =>
            {
                if (activeDrag != null) activeDrag.Drag(pos);
                else CameraPanned?.Invoke(pos);
            };
            recognizer.DragEnd += (pos, cancelled) =>
            {
                if (activeDrag != null)
                {
                    activeDrag.EndDrag(pos, cancelled);
                    activeDrag = null;
                }
                else CameraPanEnded?.Invoke();
            };
            recognizer.Pinch += (scale, center) => Pinched?.Invoke(scale, center);
            uiHit = uiHitTester;
        }

        private readonly UiHitTester uiHit;

        private void OnDragBegin(Vector2 press, Vector2 current, bool afterLongPress)
        {
            var interceptor = Interceptor;
            if (interceptor != null && interceptor.TryBeginDrag(press, current, afterLongPress))
            {
                activeDrag = interceptor;
                return;
            }
            activeDrag = null;
            CameraPanBegan?.Invoke(current);
        }

        public void Update()
        {
            source.Fill(pointers);
            recognizer.Update(pointers, Time.unscaledTime);

            float scroll = source.ConsumeScroll(out var scrollPosition);
            if (scroll != 0f && !uiHit.IsOverUi(scrollPosition)) Scrolled?.Invoke(scroll, scrollPosition);
        }
    }
}
