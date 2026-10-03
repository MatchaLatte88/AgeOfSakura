using AgeOfSakura.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AgeOfSakura.Game
{
    /// <summary>
    /// Bar shown while placing or moving a building: the same frosted light panel as the sheets, with the building name, price,
    /// a status line (icon + words, so valid/invalid is never colour-only) and round rotate / cancel / confirm buttons.
    /// Confirm stays tappable when invalid so the player is told why.
    /// Layout: [ info column (flexible) ][ rotate ][ cancel ][ confirm ].
    /// </summary>
    public sealed class PlacementBarView : MonoBehaviour
    {
        private const float MaxWidth = 1500f;
        private const float RestY = 16f;
        private const float ButtonSize = 92f;

        private PlacementController placement;
        private UiKit kit;
        private TMP_Text title;
        private TMP_Text message;
        private Image statusIcon;
        private RectTransform priceRow;
        private UiButton confirm;
        private UiButton rotate;
        private UiPulse confirmPulse;
        private RectTransform rect;
        private RectTransform parentRect;
        private bool wasActive;

        public static PlacementBarView Create(UiKit kit, RectTransform parent, PlacementController placement)
        {
            var panel = kit.Glass(parent, "PlacementBar", "panel_light", UiTheme.PanelRadius);
            var view = panel.gameObject.AddComponent<PlacementBarView>();
            view.Build(kit, panel, parent, placement);
            return view;
        }

        private void Build(UiKit kitRef, Image panel, RectTransform parent, PlacementController placementRef)
        {
            kit = kitRef;
            placement = placementRef;
            rect = panel.rectTransform;
            parentRect = parent;
            UiKit.AddShadow(gameObject, 8f, 0.3f);
            UiKit.Place(rect, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, RestY), new Vector2(MaxWidth, 0f));

            var row = UiKit.AddGroup(gameObject, false, 16f, TextAnchor.MiddleLeft, 34, 18, 22, 18);
            row.childForceExpandHeight = false;
            var fitter = gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ---- info column: title, then price + status on one line
            var info = kit.Column(rect, "Info", 6f, TextAnchor.MiddleLeft);
            UiKit.Size(info.gameObject, flexWidth: 1f);

            title = kit.Text(info, string.Empty, UiTheme.FontHeading + 2, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Title");
            UiKit.Size(title.gameObject, height: 54f);

            var line = kit.Row(info, "Line", 18f, TextAnchor.MiddleLeft);
            UiKit.Size(line.gameObject, height: UiTheme.ChipHeight + 4f);
            priceRow = kit.Row(line, "Price", 10f, TextAnchor.MiddleLeft);
            UiKit.Size(priceRow.gameObject, height: UiTheme.ChipHeight + 4f);

            var status = kit.Row(line, "Status", 10f, TextAnchor.MiddleLeft);
            UiKit.Size(status.gameObject, height: UiTheme.ChipHeight + 4f, flexWidth: 1f);
            statusIcon = kit.Picture(status, "StatusIcon", "check", new Vector2(34f, 34f));
            message = kit.Text(status, string.Empty, UiTheme.FontBody - 4, Palette.Ink, TextAlignmentOptions.MidlineLeft, FontStyles.Bold, "Message");
            UiKit.Size(message.gameObject, height: UiTheme.ChipHeight + 4f, flexWidth: 1f);

            // ---- buttons (icons are centred by the button's own layout)
            rotate = kit.IconButton(rect, "Rotate", "rotate", ButtonSize, UiButtonStyle.Secondary);
            rotate.OnClick(() => placement.Rotate());

            var cancel = kit.IconButton(rect, "Cancel", "cross", ButtonSize, UiButtonStyle.Danger);
            cancel.OnClick(() => placement.Cancel());

            confirm = kit.Button(rect, "Confirm", string.Empty, new Vector2(ButtonSize + 72f, ButtonSize), UiButtonStyle.Primary, kit.Icons.Get("check"), iconOnly: true);
            confirm.OnClick(() => placement.Confirm());
            confirmPulse = confirm.GameObject.AddComponent<UiPulse>();
            confirmPulse.Target = confirm.Rect;
            confirmPulse.Amount = 0.02f;
            confirmPulse.Speed = 3.2f;

            placement.StateChanged += Refresh;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            // narrower screens (4:3 tablets): never wider than the safe area
            float width = Mathf.Max(900f, Mathf.Min(MaxWidth, parentRect.rect.width - 2f * UiTheme.Gutter));
            if (!Mathf.Approximately(rect.sizeDelta.x, width)) rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
        }

        public void Refresh()
        {
            bool active = placement.IsActive;
            gameObject.SetActive(active);
            if (!active)
            {
                wasActive = false;
                return;
            }

            var def = placement.Definition;
            bool moving = placement.Mode == PlacementMode.MoveExisting;
            string name = Loc.BuildingName(def);
            title.text = moving ? Loc.T("move_title", name) : name;
            rotate.GameObject.SetActive(def.Rotatable);

            for (int i = priceRow.childCount - 1; i >= 0; i--)
            {
                var c = priceRow.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
            if (!moving)
            {
                foreach (var cost in def.BuildCost)
                    kit.PriceChip(priceRow, kit.Icons.ForCurrency(cost.Currency), cost.Amount.ToString(), ChipTone.Light, !placement.CanAfford, UiTheme.ChipHeight + 4f);
            }
            else
            {
                var free = kit.Text(priceRow, Loc.T("move_free"), UiTheme.FontBody - 4, Palette.InkSoft, TextAlignmentOptions.MidlineLeft, FontStyles.Normal, "Free");
                UiKit.Size(free.gameObject, height: UiTheme.ChipHeight + 4f);
            }

            if (placement.Check != PlacementCheck.Ok) SetStatus(false, PlacementController.Describe(placement.Check));
            else if (!placement.CanAfford) SetStatus(false, placement.NotEnoughMessage(def));
            else SetStatus(true, Loc.T("drag_hint"));

            confirm.SetState(placement.IsValid ? UiButtonState.Normal : UiButtonState.Unaffordable);
            confirmPulse.enabled = placement.IsValid;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);

            if (!wasActive && kit.Motion != null)
            {
                wasActive = true;
                float hidden = -Mathf.Max(rect.rect.height, 140f) - 30f;
                kit.Motion.Cancel(rect);
                kit.Motion.Play(0.36f, Ease.OutBackSoft, t => rect.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(hidden, RestY, t)), 0f,
                    () => rect.anchoredPosition = new Vector2(0f, RestY), rect);
            }
        }

        private void SetStatus(bool ok, string text)
        {
            message.text = text;
            message.color = ok ? Palette.InkSoft : Palette.UiRed;
            statusIcon.sprite = kit.Icons.Get(ok ? "check" : "cross");
            statusIcon.color = ok ? Palette.UiGreen : Palette.UiRed;
        }
    }
}
