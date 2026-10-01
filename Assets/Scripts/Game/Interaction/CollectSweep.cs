using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Hold-and-sweep collecting: a press that collected a ready building stays "armed"; while the finger keeps moving it collects every
    /// other ready building it passes over, without panning the camera and without opening any building sheet.
    /// </summary>
    public sealed class CollectSweep : IDragInterceptor
    {
        /// <summary>Distance between probes along the finger's path, so a fast swipe cannot jump over a building.</summary>
        private const float ProbeStepPx = 16f;

        private readonly SelectionController selection;
        private Vector2 last;

        /// <summary>True from a press that collected something until the next press; taps and long presses are swallowed meanwhile.</summary>
        public bool Armed { get; private set; }

        public CollectSweep(SelectionController selection)
        {
            this.selection = selection;
        }

        /// <summary>A new press: collects under the finger and arms the sweep when that worked.</summary>
        public void Press(Vector2 screen)
        {
            Armed = selection.TryCollectAt(screen);
            last = screen;
        }

        public bool TryBeginDrag(Vector2 pressScreenPosition, Vector2 currentScreenPosition, bool afterLongPress)
        {
            if (!Armed) return false;
            Drag(currentScreenPosition);
            return true;
        }

        public void Drag(Vector2 screenPosition)
        {
            float distance = Vector2.Distance(last, screenPosition);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / ProbeStepPx));
            for (int i = 1; i <= steps; i++) selection.TryCollectAt(Vector2.Lerp(last, screenPosition, i / (float)steps));
            last = screenPosition;
        }

        public void EndDrag(Vector2 screenPosition, bool cancelled)
        {
            Armed = false;
        }
    }
}
