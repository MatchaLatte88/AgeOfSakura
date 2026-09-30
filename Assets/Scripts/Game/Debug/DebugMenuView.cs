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
            var gear = kit.Button(safeArea, "DebugGear", string.Empty, new Vector2(UiTheme.TouchMin + 8f, UiTheme.TouchMin + 8f), UiButtonStyle.Dark, kit.Icons.Get("gear"), iconOnly: true);
            UiKit.Place(gear.Rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -26f), new Vector2(UiTheme.TouchMin + 8f, UiTheme.TouchMin + 8f));
            gearButton = gear.GameObject;

            var window = kit.Sliced(safeArea, "DebugPanel", "panel_lacquer", true);
            panel = window.gameObject;
            UiKit.AddShadow(window.gameObject, -10f, 0.4f);
            UiKit.Place(window.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1140f, 860f));

            var column = kit.Column(window.transform, "Content", 16f, TextAnchor.UpperCenter, 70, 56, 70, 50);
            UiKit.Stretch(column);

            var title = kit.Text(column, "Debug (development only)", TextStyle.Light, 52, Palette.Cream, TextAlignmentOptions.Center, FontStyles.Bold, "Title");
            UiKit.Size(title.gameObject, height: 70f);

            var grid = kit.Empty(column, "Buttons");
            var layout = grid.AddComponent<GridLayoutGroup>();
            layout.cellSize = new Vector2(310f, 110f);
            layout.spacing = new Vector2(24f, 16f);
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 3;
            UiKit.Size(grid.gameObject, height: 3 * 110f + 2 * 16f, flexHeight: 1f);

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

            var close = kit.Button(column, "Close", "Close", new Vector2(380f, UiTheme.TouchMin), UiButtonStyle.Secondary, null, 42);
            close.OnClick(() => panel.SetActive(false));

            gear.OnClick(() => panel.SetActive(!panel.activeSelf));
            panel.SetActive(false);
        }

        private static UiButton AddAction(UiKit kit, Transform parent, string label, System.Action action)
        {
            var b = kit.Button(parent, label, label, new Vector2(470f, 110f), UiButtonStyle.Secondary, null, 36);
            b.OnClick(action);
            return b;
        }

        public void SetGearVisible(bool visible) => gearButton.SetActive(visible);

        public void Open() => panel.SetActive(true);

        public void Close() => panel.SetActive(false);
    }
}
