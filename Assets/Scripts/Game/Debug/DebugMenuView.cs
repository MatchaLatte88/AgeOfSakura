using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>Debug panel (gear button, top right). Only created when <see cref="DebugCommands.Available"/>.</summary>
    public sealed class DebugMenuView
    {
        private readonly GameObject panel;
        private readonly GameObject gearButton;

        public DebugMenuView(UiKit kit, RectTransform safeArea, DebugCommands commands, ToastView toast)
        {
            const float gearSize = 84f;
            var gear = kit.IconButton(safeArea, "DebugGear", "gear", gearSize, UiButtonStyle.Dark);
            UiKit.Place(gear.Rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-UiTheme.Gutter, -(24f + (UiTheme.BarHeight - gearSize) * 0.5f)), new Vector2(gearSize, gearSize));
            gearButton = gear.GameObject;

            var window = kit.Glass(safeArea, "DebugPanel", "panel_light", UiTheme.PanelRadius);
            panel = window.gameObject;
            UiKit.AddShadow(window.gameObject, -10f, 0.4f);
            UiKit.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1060f, 560f));

            var column = kit.Column(window.transform, "Content", 14f, TextAnchor.UpperCenter, 36, 28, 36, 28);
            UiKit.Stretch(column);

            var title = kit.Text(column, "Debug (development only)", UiTheme.FontHeading, Palette.Ink, TextAlignmentOptions.Center, FontStyles.Bold, "Title");
            UiKit.Size(title.gameObject, height: 56f);

            var grid = kit.Empty(column, "Buttons");
            var layout = grid.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(310f, 84f);
            layout.spacing = new Vector2(16f, 14f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            UiKit.Size(grid.gameObject, height: 3 * 84f + 2 * 14f, flexHeight: 1f);

            AddAction(kit, grid.transform, "+1000 Coins", () => commands.AddCoins());
            AddAction(kit, grid.transform, "+1000 Wood", () => commands.AddWood());
            AddAction(kit, grid.transform, "+100 Rice", () => commands.AddRice());
            AddAction(kit, grid.transform, "+100 Diamonds", () => commands.AddDiamonds());
            AddAction(kit, grid.transform, "Finish all productions", () => toast.Show($"Finished {commands.FinishAllProductions()} production(s)"));
            AddAction(kit, grid.transform, "Toggle grid", () => commands.ToggleGrid());
            AddAction(kit, grid.transform, "Toggle locked area", () => commands.ToggleLockedArea());
            AddAction(kit, grid.transform, "Language EN/DE", () => commands.ToggleLanguage());
            var reset = AddAction(kit, grid.transform, "Reset save", () => commands.ResetSave());
            reset.SetStyle(UiButtonStyle.Danger);

            var close = kit.Button(column, "Close", "Close", new Vector2(320f, UiTheme.TouchMin), UiButtonStyle.Secondary, null, UiTheme.FontBody);
            close.OnClick(() => panel.SetActive(false));

            gear.OnClick(() => panel.SetActive(!panel.activeSelf));
            panel.SetActive(false);
        }

        private static UiButton AddAction(UiKit kit, Transform parent, string label, System.Action action)
        {
            var b = kit.Button(parent, label, label, new Vector2(310f, 84f), UiButtonStyle.Secondary, null, UiTheme.FontBody - 6);
            b.OnClick(action);
            return b;
        }

        public void SetGearVisible(bool visible) => gearButton.SetActive(visible);

        public void Open() => panel.SetActive(true);

        public void Close() => panel.SetActive(false);
    }
}
