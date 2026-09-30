using AgeOfSakura.Core;
using UnityEngine;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Fixed-rotation orthographic camera looking at the ground plane from an isometric-style angle.
    /// Panning uses the "grab the ground" technique (the world point under the finger stays under the finger),
    /// zoom pivots around the pinch centre, and both are clamped. All tuning lives in <see cref="CameraConfig"/>.
    /// </summary>
    public sealed class IsoCameraController : MonoBehaviour
    {
        private const float CameraDistance = 80f;

        private Camera cam;
        private CameraConfig config;
        private Vector2 mapSize;
        private Vector3 focus;
        private Vector3 velocity;
        private bool panning;
        private Vector3 panAnchor;
        private float targetSize;
        private float sizeVelocity;
        private bool zoomPivotActive;
        private Vector2 zoomPivotScreen;
        private Vector3 zoomPivotWorld;
        private readonly Plane ground = new Plane(Vector3.up, Vector3.zero);
        private bool gliding;
        private Vector3 glideFrom;
        private Vector3 glideTo;
        private float glideAge;
        private float glideDuration;

        public Camera Camera => cam;
        public Quaternion Rotation => transform.rotation;

        public void Initialize(Camera camera, CameraConfig cfg, Vector2 worldMapSize)
        {
            cam = camera;
            config = cfg;
            mapSize = worldMapSize;

            cam.orthographic = true;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = CameraDistance * 2.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Sky;
            cam.allowHDR = false;
            cam.allowMSAA = false; // keep mobile cheap; URP asset controls quality

            transform.rotation = Quaternion.Euler(cfg.PitchDegrees, cfg.YawDegrees, 0f);
            targetSize = Mathf.Clamp(cfg.DefaultOrthoSize, cfg.MinOrthoSize, cfg.MaxOrthoSize);
            cam.orthographicSize = targetSize;
            focus = new Vector3(cfg.DefaultFocusX, 0f, cfg.DefaultFocusZ);
            ApplyFocus();
        }

        /// <summary>Ground-plane point (y = 0) under a screen position.</summary>
        public Vector3 ScreenToGround(Vector2 screen)
        {
            var ray = cam.ScreenPointToRay(screen);
            return ground.Raycast(ray, out float t) ? ray.GetPoint(t) : focus;
        }

        public Vector2 WorldToScreen(Vector3 world) => cam.WorldToScreenPoint(world);

        public void BeginPan(Vector2 screen)
        {
            gliding = false;
            panning = true;
            velocity = Vector3.zero;
            panAnchor = ScreenToGround(screen);
        }

        public void Pan(Vector2 screen)
        {
            if (!panning) return;
            var current = ScreenToGround(screen);
            var delta = panAnchor - current;
            delta.y = 0f;
            var before = focus;
            focus += delta;
            ClampFocus();
            ApplyFocus();
            float dt = Mathf.Max(Time.unscaledDeltaTime, 1e-4f);
            velocity = Vector3.Lerp(velocity, (focus - before) / dt, 0.45f);
        }

        public void EndPan()
        {
            panning = false;
            velocity.y = 0f;
            if (velocity.magnitude < 0.6f) velocity = Vector3.zero;
            else velocity = Vector3.ClampMagnitude(velocity, config.MaxInertiaSpeed);
        }

        /// <summary>factor &gt; 1 zooms in, &lt; 1 zooms out; pivot keeps the world point under <paramref name="screenCenter"/> fixed.</summary>
        public void Zoom(float factor, Vector2 screenCenter)
        {
            if (factor <= 0f) return;
            targetSize = Mathf.Clamp(targetSize / factor, config.MinOrthoSize, config.MaxOrthoSize);
            zoomPivotScreen = screenCenter;
            zoomPivotWorld = ScreenToGround(screenCenter);
            zoomPivotActive = true;
        }

        /// <summary>
        /// Smoothly moves the camera so <paramref name="worldPoint"/> appears at <paramref name="screenTarget01"/> (0..1 of the screen).
        /// Used to keep a selected building visible above the bottom sheet.
        /// </summary>
        public void RevealAt(Vector3 worldPoint, Vector2 screenTarget01, float duration = 0.5f)
        {
            var current = (Vector2)cam.WorldToScreenPoint(worldPoint);
            var wanted = new Vector2(screenTarget01.x * cam.pixelWidth, screenTarget01.y * cam.pixelHeight);
            var delta = ScreenToGround(current) - ScreenToGround(wanted);
            delta.y = 0f;
            var previous = focus;
            focus += delta;
            ClampFocus();
            var target = focus;
            focus = previous;
            velocity = Vector3.zero;
            glideFrom = focus;
            glideTo = target;
            glideAge = 0f;
            glideDuration = Mathf.Max(0.05f, duration);
            gliding = (glideTo - glideFrom).sqrMagnitude > 0.0004f;
        }

        public void FrameOn(Vector3 worldPoint)
        {
            focus = new Vector3(worldPoint.x, 0f, worldPoint.z);
            velocity = Vector3.zero;
            ClampFocus();
            ApplyFocus();
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            if (gliding)
            {
                glideAge += dt;
                float t = Mathf.Clamp01(glideAge / glideDuration);
                focus = Vector3.Lerp(glideFrom, glideTo, Ease.OutCubic(t));
                ApplyFocus();
                if (t >= 1f) gliding = false;
            }

            if (!panning && !gliding && velocity.sqrMagnitude > 0.0025f)
            {
                var before = focus;
                focus += velocity * dt;
                ClampFocus();
                // stop against the boundary instead of sliding along it
                if (Mathf.Abs(focus.x - before.x) < Mathf.Abs(velocity.x * dt) * 0.5f) velocity.x = 0f;
                if (Mathf.Abs(focus.z - before.z) < Mathf.Abs(velocity.z * dt) * 0.5f) velocity.z = 0f;
                velocity *= Mathf.Exp(-config.InertiaDamping * dt);
                ApplyFocus();
            }
            else if (!panning)
            {
                velocity = Vector3.zero;
            }

            if (Mathf.Abs(cam.orthographicSize - targetSize) > 0.0005f)
            {
                cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, targetSize, ref sizeVelocity, Mathf.Max(0.0001f, config.ZoomSmoothTime), Mathf.Infinity, dt);
                if (zoomPivotActive)
                {
                    var now = ScreenToGround(zoomPivotScreen);
                    focus += zoomPivotWorld - now;
                    focus.y = 0f;
                }
                ClampFocus();
                ApplyFocus();
            }
            else
            {
                zoomPivotActive = false;
            }
        }

        private void ClampFocus()
        {
            // The further out we are, the less there is to pan: fully zoomed out the whole valley is visible.
            float t = Mathf.InverseLerp(config.MinOrthoSize, config.MaxOrthoSize, cam.orthographicSize);
            float halfX = Mathf.Lerp(mapSize.x * 0.5f + config.BoundsPadding, 1.5f, t);
            float halfZ = Mathf.Lerp(mapSize.y * 0.5f + config.BoundsPadding, 1.5f, t);
            var center = new Vector3(mapSize.x * 0.5f, 0f, mapSize.y * 0.5f);
            focus.x = Mathf.Clamp(focus.x, center.x - halfX, center.x + halfX);
            focus.z = Mathf.Clamp(focus.z, center.z - halfZ, center.z + halfZ);
            focus.y = 0f;
        }

        private void ApplyFocus()
        {
            transform.position = focus - transform.forward * CameraDistance;
        }
    }
}
