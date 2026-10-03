using UnityEngine;

namespace AgeOfSakura.Game
{
    public enum IndicatorMode
    {
        Hidden,
        Producing,
        Ready,
        /// <summary>Idle building that can be upgraded right now: a calm bubble with an arrow.</summary>
        Upgrade
    }

    /// <summary>
    /// Reusable world-space marker above a building: a small progress capsule while producing, a cream bubble with a soft glow
    /// and a bouncing resource icon when ready. The camera never rotates, so the sprites are oriented once and need no per-frame
    /// billboarding. Update() is enabled only while the ready bubble animates, so idle buildings cost nothing.
    /// </summary>
    public sealed class WorldIndicator : MonoBehaviour
    {
        private const float BubbleSize = 1.05f;
        private const float BarWidth = 1.25f;
        private const float BarHeight = 0.36f;

        private SpriteRenderer glow;
        private SpriteRenderer bubble;
        private SpriteRenderer icon;
        private SpriteRenderer miniBubble;
        private SpriteRenderer trough;
        private SpriteRenderer fill;
        private BoxCollider tapCollider;
        private Transform visualRoot;
        private float phase;

        public IndicatorMode Mode { get; private set; } = IndicatorMode.Hidden;

        public static WorldIndicator Create(Transform parent, GameArt art, Quaternion cameraRotation)
        {
            var go = new GameObject("WorldIndicator");
            go.transform.SetParent(parent, false);
            go.transform.rotation = cameraRotation;
            var indicator = go.AddComponent<WorldIndicator>();
            indicator.Build(art);
            return indicator;
        }

        private void Build(GameArt art)
        {
            var root = new GameObject("Visual").transform;
            root.SetParent(transform, false);
            visualRoot = root;

            glow = MakeSprite("Glow", root, art.Icons.Get("glow"), 7, 1.7f);
            glow.color = new Color(1f, 0.85f, 0.4f, 0.55f);
            bubble = MakeSprite("Bubble", root, art.Icons.Get("bubble"), 8, BubbleSize);
            icon = MakeSprite("Icon", root, art.Icons.Get("coin"), 9, 0.6f);

            miniBubble = MakeSprite("MiniBubble", root, art.Icons.Get("bubble"), 8, 0.62f);
            trough = MakeSliced("Trough", root, art.Icons.Get("trough"), 8);
            trough.size = new Vector2(BarWidth, BarHeight);
            fill = MakeSliced("Fill", root, art.Icons.Get("bar_green"), 9);

            tapCollider = gameObject.AddComponent<BoxCollider>();
            tapCollider.size = new Vector2(1.35f, 1.35f) + new Vector2(0f, 0f);
            tapCollider.size = new Vector3(1.35f, 1.35f, 0.5f); // generous: fingers are not precise
            tapCollider.enabled = false;
            enabled = false;
            SetMode(IndicatorMode.Hidden, null, 0f);
        }

        private static SpriteRenderer MakeSprite(string name, Transform parent, Sprite sprite, int order, float worldSize)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            go.transform.localScale = Vector3.one * (worldSize / (sprite.rect.width / sprite.pixelsPerUnit));
            return sr;
        }

        private static SpriteRenderer MakeSliced(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Sliced;
            return sr;
        }

        private static void FitIcon(SpriteRenderer target, float worldSize)
        {
            target.transform.localScale = Vector3.one * (worldSize / (target.sprite.rect.width / target.sprite.pixelsPerUnit));
        }

        public void SetMode(IndicatorMode mode, Sprite resourceIcon, float progress)
        {
            Mode = mode;
            bool ready = mode == IndicatorMode.Ready;
            bool upgrade = mode == IndicatorMode.Upgrade;
            bool producing = mode == IndicatorMode.Producing;

            glow.enabled = ready || upgrade;
            bubble.enabled = ready || upgrade;
            icon.enabled = ready || upgrade || producing;
            miniBubble.enabled = producing;
            trough.enabled = producing;
            fill.enabled = producing;
            tapCollider.enabled = ready || upgrade;
            enabled = ready || upgrade;
            glow.color = upgrade ? new Color(0.55f, 0.9f, 0.4f, 0.5f) : new Color(1f, 0.85f, 0.4f, 0.55f);
            bubble.color = Color.white;

            if (resourceIcon != null) icon.sprite = resourceIcon;
            icon.color = upgrade ? Palette.UiGreen : Color.white; // the upgrade arrow is a flat white glyph: tinted, or it vanishes on the cream bubble
            visualRoot.localPosition = Vector3.zero;
            visualRoot.localScale = Vector3.one;
            if (ready || upgrade)
            {
                FitIcon(icon, 0.62f);
                icon.transform.localPosition = new Vector3(0f, 0.02f, -0.001f);
            }
            else if (producing)
            {
                FitIcon(icon, 0.44f);
                float iconX = -BarWidth * 0.5f - 0.3f;
                icon.transform.localPosition = new Vector3(iconX, 0f, -0.001f);
                miniBubble.transform.localPosition = new Vector3(iconX, 0f, 0f);
                trough.transform.localPosition = Vector3.zero;
                SetProgress(progress);
            }
        }

        public void SetProgress(float progress)
        {
            if (Mode != IndicatorMode.Producing) return;
            progress = Mathf.Clamp01(progress);
            float inner = BarWidth - 0.1f;
            float width = Mathf.Max(0.12f, inner * progress);
            fill.size = new Vector2(width, BarHeight - 0.1f);
            fill.transform.localPosition = new Vector3(-inner * 0.5f + width * 0.5f, 0f, -0.001f);
        }

        private void Update()
        {
            phase += Time.unscaledDeltaTime;
            float bounce = Mathf.Abs(Mathf.Sin(phase * 3.4f)) * (Mode == IndicatorMode.Upgrade ? 0.07f : 0.14f);
            visualRoot.localPosition = new Vector3(0f, bounce, 0f);
            float pulse = 1f + Mathf.Sin(phase * 6.8f) * 0.03f;
            visualRoot.localScale = new Vector3(pulse, pulse, 1f);
            var c = glow.color;
            c.a = 0.42f + Mathf.Sin(phase * 3.4f) * 0.14f;
            glow.color = c;
        }
    }
}
