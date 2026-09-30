using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace AgeOfSakura.Game
{
    /// <summary>Abstraction over raw devices so gesture logic does not depend on a specific input backend.</summary>
    public interface IPointerSource
    {
        /// <summary>Fills <paramref name="buffer"/> with all pointers currently pressed (touches, or the mouse's left button).</summary>
        void Fill(List<PointerSample> buffer);

        /// <summary>Mouse wheel notches this frame (positive = away from the user), 0 if none.</summary>
        float ConsumeScroll(out Vector2 position);
    }

    /// <summary>Unity Input System implementation: Touchscreen first, Mouse as the editor/desktop stand-in.</summary>
    public sealed class InputSystemPointerSource : IPointerSource
    {
        public void Fill(List<PointerSample> buffer)
        {
            buffer.Clear();
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    // a touch that began and ended between two frames still counts for one frame, otherwise very quick taps get lost
                    if (!touch.isInProgress && !touch.press.wasPressedThisFrame) continue;
                    buffer.Add(new PointerSample { Id = touch.touchId.ReadValue(), Position = touch.position.ReadValue() });
                }
            }

            if (buffer.Count == 0)
            {
                var mouse = Mouse.current;
                if (mouse != null && (mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame))
                    buffer.Add(new PointerSample { Id = -1, Position = mouse.position.ReadValue() });
            }
        }

        public float ConsumeScroll(out Vector2 position)
        {
            position = default;
            var mouse = Mouse.current;
            if (mouse == null) return 0f;
            float y = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(y) < 0.01f) return 0f;
            position = mouse.position.ReadValue();
            return Mathf.Sign(y);
        }
    }

    /// <summary>Asks the uGUI event system whether a screen position is over interactive UI, so UI never leaks gestures to the world.</summary>
    public sealed class UiHitTester
    {
        private readonly List<RaycastResult> results = new List<RaycastResult>(8);
        private PointerEventData eventData;

        public bool IsOverUi(Vector2 screenPosition)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            if (eventData == null) eventData = new PointerEventData(system);
            eventData.position = screenPosition;
            results.Clear();
            system.RaycastAll(eventData, results);
            return results.Count > 0;
        }
    }
}
